using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Records front-type ABO/Rh retypes on inventory units. The retype is the
/// second confirmation of the supplier type, so a matching record moves
/// Received to Available immediately. A discrepancy moves to Quarantine.
/// </summary>
public sealed class ProductRetypeService
{
    private static readonly IReadOnlyList<string> GradeChoices =
        SubtestChoiceDefinitions.DefaultGradedReaction()
            .Where(c => !string.Equals(c.Code, "NT", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Code)
            .ToList();

    private readonly IInventoryRepository _inventory;
    private readonly IRepository<ProductRetypeResult> _results;
    private readonly IRepository<TestDefinition> _tests;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public ProductRetypeService(
        IInventoryRepository inventory,
        IRepository<ProductRetypeResult> results,
        IRepository<TestDefinition> tests,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _inventory = inventory;
        _results = results;
        _tests = tests;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<ProductRetypeWorkItemDto>> ListPendingAsync(CancellationToken ct = default)
    {
        var units = await _inventory.ListPendingRetypeAsync(ct);
        if (units.Count == 0)
        {
            return [];
        }

        var unitIds = units.Select(u => u.Id).ToList();
        var results = await _results.ListAsync(r => unitIds.Contains(r.BloodProductId), ct);
        var latestByUnit = results
            .GroupBy(r => r.BloodProductId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.EnteredUtc).ThenByDescending(r => r.Id).First());

        var testIds = latestByUnit.Values.Select(r => r.TestDefinitionId).Distinct().ToList();
        var tests = testIds.Count == 0
            ? new Dictionary<long, TestDefinition>()
            : (await _tests.ListAsync(t => testIds.Contains(t.Id), ct)).ToDictionary(t => t.Id);

        return units.Select(unit =>
        {
            latestByUnit.TryGetValue(unit.Id, out var latest);
            string? testName = null;
            if (latest is not null && tests.TryGetValue(latest.TestDefinitionId, out var test))
            {
                testName = test.Name;
            }

            return ProductRetypeWorkItemDto.From(unit, latest, testName);
        }).ToList();
    }

    /// <summary>
    /// Adds the applicable Rh+ / Rh− catalog test as a Pending unit retype when a
    /// retype-required product is received. Idempotent if a Pending or Entered row exists.
    /// </summary>
    public async Task<EvaluationResult<ProductRetypeResult>> EnsurePendingOnIntakeAsync(
        BloodUnit unit,
        ProductType product,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(product);

        if (!product.RequiresRetype)
        {
            return EvaluationResult<ProductRetypeResult>.Fail("This product does not require an ABO/Rh retype.");
        }

        var gate = ProductRetypeAssignment.Evaluate(product, unit.RhD);
        if (gate is { Severity: RuleSeverity.HardStop })
        {
            return EvaluationResult<ProductRetypeResult>.Blocked(new RuleEvaluation([gate]));
        }

        if (unit.Id != 0)
        {
            var existing = await LatestForUnitAsync(unit.Id, ct);
            if (existing is { Status: ResultStatus.Pending or ResultStatus.Entered })
            {
                return EvaluationResult<ProductRetypeResult>.Ok(existing);
            }
        }

        var test = await ResolveAssignedTestAsync(unit, product, pending: null, ct);
        if (test is null)
        {
            return EvaluationResult<ProductRetypeResult>.Fail("The applicable Rh retype test is not configured.");
        }

        var pending = new ProductRetypeResult
        {
            BloodProductId = unit.Id,
            Unit = unit.Id == 0 ? unit : null,
            TestDefinitionId = test.Id,
            TestCode = test.Code,
            Value = string.Empty,
            Status = ResultStatus.Pending,
            EnteredBy = _currentUser.UserName,
            EnteredUtc = _clock.UtcNow
        };
        await _results.AddAsync(pending, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ProductRetypeResult>.Ok(pending);
    }

    public async Task<ProductRetypeDetailDto?> GetForUnitAsync(long unitId, CancellationToken ct = default)
    {
        var unit = await _inventory.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return null;
        }

        var latest = await LatestForUnitAsync(unitId, ct);
        var requires = unit.ProductType?.RequiresRetype == true;
        var antiDRequired = unit.RhD == RhType.Negative;
        var awaitingConfirm = latest?.Status == ResultStatus.Entered;
        var canRecord = requires && unit.Status == UnitStatus.Received;
        var canVerify = canRecord && awaitingConfirm;
        string? blockReason = null;
        if (!requires)
        {
            blockReason = "This product does not require an ABO/Rh retype.";
        }
        else if (unit.Status != UnitStatus.Received)
        {
            blockReason = $"Retype can only be recorded while the unit is Received (current status: {unit.Status}).";
        }

        var assigned = unit.ProductType is null
            ? null
            : await ResolveAssignedTestAsync(unit, unit.ProductType, latest, ct);

        return new ProductRetypeDetailDto(
            unit.Id,
            unit.UnitNumber,
            unit.ProductType?.ProductCode ?? string.Empty,
            unit.ProductType?.Name ?? string.Empty,
            requires,
            unit.Abo,
            unit.RhD,
            unit.BloodType.ToString(),
            unit.Status,
            canRecord,
            canVerify,
            blockReason,
            antiDRequired,
            BuildSubtestsFromTest(assigned, antiDRequired),
            GradeChoices,
            latest is null ? null : ProductRetypeResultDto.From(latest),
            assigned?.Code,
            assigned?.Name);
    }

    public async Task<EvaluationResult<ProductRetypeDetailDto>> RecordAsync(
        long unitId,
        RecordProductRetypeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedEnterAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var unit = await _inventory.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Fail("Unit not found.");
        }

        if (unit.ProductType?.RequiresRetype != true)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Fail("This product does not require an ABO/Rh retype.");
        }

        if (unit.Status != UnitStatus.Received)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Fail(
                $"Retype can only be recorded while the unit is Received (current status: {unit.Status}).");
        }

        var subtests = request.Subtests ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var outcome = AboRhRetypeRule.Evaluate(unit.Abo, unit.RhD, subtests);
        if (!outcome.CanRecord)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Blocked(outcome.Validation);
        }

        if (request.InterpretedAbo != outcome.InterpretedAbo)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Blocked(new RuleEvaluation(
            [
                RuleResult.HardStop(
                    "RETYPE.INTERPRETATION.ABO",
                    $"Interpreted ABO {request.InterpretedAbo} does not match the Anti-A/Anti-B pattern ({outcome.InterpretedAbo}).")
            ]));
        }

        if (unit.RhD == RhType.Negative
            && request.InterpretedRh is not null
            && request.InterpretedRh != outcome.InterpretedRh)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Blocked(new RuleEvaluation(
            [
                RuleResult.HardStop(
                    "RETYPE.INTERPRETATION.RH",
                    $"Interpreted Rh(D) {request.InterpretedRh} does not match the Anti-D pattern ({outcome.InterpretedRh}).")
            ]));
        }

        var pending = await LatestOpenTrackedAsync(unit.Id, ct);
        var test = unit.ProductType is null
            ? null
            : await ResolveAssignedTestAsync(unit, unit.ProductType, pending, ct);
        if (test is null)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Fail("The applicable Rh retype test is not configured.");
        }

        var now = _clock.UtcNow;
        var panel = new AboRhPanelResult(
            outcome.InterpretedAbo,
            outcome.InterpretedRh ?? RhType.Unknown,
            subtests);
        var stored = AboRhResultValue.FormatPanel(panel);
        var wasUpdate = pending is { Status: ResultStatus.Entered };
        object? oldValue = pending is null
            ? null
            : new
            {
                pending.InterpretedAbo,
                pending.InterpretedRh,
                pending.Value,
                pending.MatchesLabel,
                pending.Status
            };
        ProductRetypeResult row;
        if (pending is not null)
        {
            pending.TestDefinitionId = test.Id;
            pending.TestCode = test.Code;
            pending.Value = stored;
            pending.InterpretedAbo = outcome.InterpretedAbo;
            pending.InterpretedRh = outcome.InterpretedRh;
            pending.MatchesLabel = outcome.MatchesLabel;
            pending.DiscrepancyDetail = outcome.DiscrepancyDetail;
            pending.EnteredBy = _currentUser.UserName;
            pending.EnteredUtc = now;
            row = pending;
        }
        else
        {
            row = new ProductRetypeResult
            {
                BloodProductId = unit.Id,
                TestDefinitionId = test.Id,
                TestCode = test.Code,
                Value = stored,
                InterpretedAbo = outcome.InterpretedAbo,
                InterpretedRh = outcome.InterpretedRh,
                MatchesLabel = outcome.MatchesLabel,
                DiscrepancyDetail = outcome.DiscrepancyDetail,
                EnteredBy = _currentUser.UserName,
                EnteredUtc = now
            };
            await _results.AddAsync(row, ct);
        }

        var fromStatus = unit.Status;
        var statusReason = ConfirmRetype(unit, row, now);
        await _unitOfWork.SaveChangesAsync(ct);

        _audit.Record(
            AuditEventType.Result,
            nameof(ProductRetypeResult),
            row.Id,
            oldValue: oldValue,
            newValue: new
            {
                unit.Id,
                row.TestCode,
                row.Value,
                row.InterpretedAbo,
                row.InterpretedRh,
                row.MatchesLabel,
                row.Status
            },
            reason: wasUpdate ? "Unit ABO/Rh retype updated." : "Unit ABO/Rh retype entered.");
        WriteConfirmationAudits(unit, row, fromStatus, statusReason);
        await _unitOfWork.SaveChangesAsync(ct);

        var detail = await GetForUnitAsync(unitId, ct);
        return EvaluationResult<ProductRetypeDetailDto>.Ok(detail!);
    }

    public async Task<EvaluationResult<ProductRetypeDetailDto>> VerifyAsync(
        long unitId,
        long resultId,
        CancellationToken ct = default)
    {
        var unit = await _inventory.GetUnitAsync(unitId, ct);
        if (unit is null)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Fail("Unit not found.");
        }

        var denied = await RejectUnauthorizedVerifyAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        if (unit.Status != UnitStatus.Received)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Fail(
                $"Retype can only be verified while the unit is Received (current status: {unit.Status}).");
        }

        var result = await _results.GetByIdAsync(resultId, ct);
        if (result is null || result.BloodProductId != unit.Id)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Fail("Retype result not found.");
        }

        if (result.Status != ResultStatus.Entered)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Fail(
                $"A retype with status {result.Status} cannot be verified.");
        }

        var now = _clock.UtcNow;
        var fromStatus = unit.Status;
        var statusReason = ConfirmRetype(unit, result, now);
        WriteConfirmationAudits(unit, result, fromStatus, statusReason);
        await _unitOfWork.SaveChangesAsync(ct);

        var detail = await GetForUnitAsync(unitId, ct);
        return EvaluationResult<ProductRetypeDetailDto>.Ok(detail!);
    }

    private async Task<EvaluationResult<ProductRetypeDetailDto>?> RejectUnauthorizedVerifyAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.ResultVerify, ct);
        var auth = ResultAuthorizationRule.EvaluateVerify(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<ProductRetypeDetailDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<EvaluationResult<ProductRetypeDetailDto>?> RejectUnauthorizedEnterAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.ResultEnter, ct);
        var auth = ResultAuthorizationRule.EvaluateEnter(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<ProductRetypeDetailDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<ProductRetypeResult?> LatestForUnitAsync(long unitId, CancellationToken ct) =>
        (await _results.ListAsync(r => r.BloodProductId == unitId, ct))
            .OrderByDescending(r => r.EnteredUtc)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();

    private async Task<ProductRetypeResult?> LatestOpenTrackedAsync(long unitId, CancellationToken ct)
    {
        var latest = await LatestForUnitAsync(unitId, ct);
        if (latest is null || latest.Status is not (ResultStatus.Pending or ResultStatus.Entered))
        {
            return null;
        }

        return await _results.GetByIdAsync(latest.Id, ct);
    }

    private async Task<TestDefinition?> ResolveAssignedTestAsync(
        BloodUnit unit,
        ProductType product,
        ProductRetypeResult? pending,
        CancellationToken ct)
    {
        if (pending is not null)
        {
            var fromPending = await _tests.GetByIdAsync(pending.TestDefinitionId, ct);
            if (fromPending is { IsActive: true })
            {
                return fromPending;
            }
        }

        var testId = ProductRetypeAssignment.ResolveTestId(product, unit.RhD);
        if (testId is long id)
        {
            var assigned = await _tests.GetByIdAsync(id, ct);
            if (assigned is { IsActive: true })
            {
                return assigned;
            }
        }

        return await _tests.FirstOrDefaultAsync(t => t.Code == AboRhRetypeRule.TestCode && t.IsActive, ct);
    }

    private string ConfirmRetype(BloodUnit unit, ProductRetypeResult result, DateTime now)
    {
        result.Status = ResultStatus.Verified;
        result.VerifiedBy = _currentUser.UserName;
        result.VerifiedUtc = now;

        if (result.MatchesLabel)
        {
            const string confirmed = "ABO/Rh retype confirmed";
            ApplyStatus(unit, UnitStatus.Available, confirmed);
            return confirmed;
        }

        var statusReason = result.DiscrepancyDetail ?? "ABO/Rh retype discrepancy";
        unit.QuarantineReason = statusReason;
        unit.QuarantineReasonCode = UnitQuarantineReason.RetypeDiscrepancy;
        ApplyStatus(unit, UnitStatus.Quarantine, statusReason);
        return statusReason;
    }

    private void WriteConfirmationAudits(
        BloodUnit unit,
        ProductRetypeResult result,
        UnitStatus fromStatus,
        string statusReason)
    {
        _audit.Record(
            AuditEventType.Verify,
            nameof(ProductRetypeResult),
            result.Id,
            oldValue: new
            {
                Status = ResultStatus.Entered,
                result.InterpretedAbo,
                result.InterpretedRh,
                result.Value,
                result.MatchesLabel
            },
            newValue: new
            {
                result.Status,
                result.VerifiedBy,
                result.InterpretedAbo,
                result.InterpretedRh,
                result.Value,
                result.MatchesLabel
            },
            reason: statusReason);
        _audit.Record(
            AuditEventType.ProductStatus,
            nameof(BloodUnit),
            unit.Id,
            oldValue: new { Status = fromStatus },
            newValue: new { unit.Status },
            reason: statusReason);
    }

    private void ApplyStatus(BloodUnit unit, UnitStatus toStatus, string reason)
    {
        var transition = InventoryStatusTransition.Evaluate(unit.Status, toStatus);
        if (transition.Severity == RuleSeverity.HardStop)
        {
            throw new InvalidOperationException(transition.Message);
        }

        var fromStatus = unit.Status;
        unit.Status = toStatus;
        _inventory.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = reason,
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });
    }

    private static IReadOnlyList<ProductRetypeSubtestDto> BuildSubtestsFromTest(
        TestDefinition? test,
        bool antiDRequired)
    {
        var assignments = PanelSubtestAssignments.Parse(test?.PanelSubtestsJson);
        if (assignments.Count > 0)
        {
            var labels = PanelSubtestDefinitions.DefaultAboRh()
                .Concat(PanelSubtestDefinitions.DefaultAboRhRetype())
                .GroupBy(s => s.Code)
                .ToDictionary(g => g.Key, g => g.First().Label);
            return assignments
                .Select(a => new ProductRetypeSubtestDto(
                    a.SubtestCode,
                    labels.TryGetValue(a.SubtestCode, out var label) ? label : a.SubtestCode,
                    a.Required))
                .ToList();
        }

        return BuildSubtests(antiDRequired);
    }

    private static IReadOnlyList<ProductRetypeSubtestDto> BuildSubtests(bool antiDRequired) =>
    [
        new(AboRhPanelSubtestCodes.AntiA, "Anti-A", true),
        new(AboRhPanelSubtestCodes.AntiB, "Anti-B", true),
        new(AboRhPanelSubtestCodes.AntiD, "Anti-D", antiDRequired)
    ];
}
