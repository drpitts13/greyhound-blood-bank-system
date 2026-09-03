using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Records front-type ABO/Rh retypes on inventory units and releases matching
/// units from Received to Available (or Quarantine on discrepancy).
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

    public ProductRetypeService(
        IInventoryRepository inventory,
        IRepository<ProductRetypeResult> results,
        IRepository<TestDefinition> tests,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser)
    {
        _inventory = inventory;
        _results = results;
        _tests = tests;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProductRetypeWorkItemDto>> ListPendingAsync(CancellationToken ct = default)
    {
        var units = await _inventory.ListPendingRetypeAsync(ct);
        return units.Select(ProductRetypeWorkItemDto.From).ToList();
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
        var canRecord = requires && unit.Status == UnitStatus.Received;
        string? blockReason = null;
        if (!requires)
        {
            blockReason = "This product does not require an ABO/Rh retype.";
        }
        else if (unit.Status != UnitStatus.Received)
        {
            blockReason = $"Retype can only be recorded while the unit is Received (current status: {unit.Status}).";
        }

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
            blockReason,
            antiDRequired,
            BuildSubtests(antiDRequired),
            GradeChoices,
            latest is null ? null : ProductRetypeResultDto.From(latest));
    }

    public async Task<EvaluationResult<ProductRetypeDetailDto>> RecordAsync(
        long unitId,
        RecordProductRetypeRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

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

        var test = await _tests.FirstOrDefaultAsync(
            t => t.Code == AboRhRetypeRule.TestCode && t.IsActive, ct);
        if (test is null)
        {
            return EvaluationResult<ProductRetypeDetailDto>.Fail("ABORH-RETYPE test definition is not configured.");
        }

        var now = _clock.UtcNow;
        var panel = new AboRhPanelResult(
            outcome.InterpretedAbo,
            outcome.InterpretedRh ?? RhType.Unknown,
            subtests);
        var stored = AboRhResultValue.FormatPanel(panel);

        var result = new ProductRetypeResult
        {
            BloodProductId = unit.Id,
            TestDefinitionId = test.Id,
            TestCode = test.Code,
            Value = stored,
            InterpretedAbo = outcome.InterpretedAbo,
            InterpretedRh = outcome.InterpretedRh,
            MatchesLabel = outcome.MatchesLabel,
            DiscrepancyDetail = outcome.DiscrepancyDetail,
            Status = ResultStatus.Verified,
            EnteredBy = _currentUser.UserName,
            EnteredUtc = now,
            VerifiedBy = _currentUser.UserName,
            VerifiedUtc = now
        };
        await _results.AddAsync(result, ct);

        if (outcome.MatchesLabel)
        {
            ApplyStatus(unit, UnitStatus.Available, "ABO/Rh retype confirmed");
        }
        else
        {
            var reason = outcome.DiscrepancyDetail ?? "ABO/Rh retype discrepancy";
            unit.QuarantineReason = reason;
            unit.QuarantineReasonCode = UnitQuarantineReason.RetypeDiscrepancy;
            ApplyStatus(unit, UnitStatus.Quarantine, reason);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var detail = await GetForUnitAsync(unitId, ct);
        return EvaluationResult<ProductRetypeDetailDto>.Ok(detail!);
    }

    private async Task<ProductRetypeResult?> LatestForUnitAsync(long unitId, CancellationToken ct) =>
        (await _results.ListAsync(r => r.BloodProductId == unitId, ct))
            .OrderByDescending(r => r.EnteredUtc)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();

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

    private static IReadOnlyList<ProductRetypeSubtestDto> BuildSubtests(bool antiDRequired) =>
    [
        new(AboRhPanelSubtestCodes.AntiA, "Anti-A", true),
        new(AboRhPanelSubtestCodes.AntiB, "Anti-B", true),
        new(AboRhPanelSubtestCodes.AntiD, "Anti-D", antiDRequired)
    ];
}
