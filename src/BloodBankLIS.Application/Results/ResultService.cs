using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Rules;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Results;

/// <summary>
/// Test result entry, verification, and the versioned correction workflow. Verified
/// results are immutable; corrections create a new version and supersede the prior
/// row (see docs/safety-rules.md sections 6-7). Verifying an ABO/Rh result appends
/// to the patient's blood-type history and runs a delta check. Verifying a
/// free-text/coded test marked <c>ContributesToAntibodyHistory</c> (ABID) posts
/// identified specificities to <see cref="AntibodyHistory"/>.
/// </summary>
public sealed class ResultService
{
    public const string AboRhTestCode = "ABORH";
    public const string CrossmatchTestCode = "XM";

    private readonly IRepository<TestResult> _results;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<Order>? _orders;
    private readonly IRepository<OrderLine>? _orderLines;
    private readonly IInventoryRepository? _inventory;
    private readonly CompatibilityService? _compatibility;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IRepository<TestDefinition>? _testDefinitions;
    private readonly IRepository<SubtestDefinition>? _subtestDefinitions;
    private readonly IRepository<PhaseDefinition>? _phaseDefinitions;
    private readonly IRepository<AntibodyHistory>? _antibodies;
    private readonly IRepository<AntigenProfile>? _antigenProfiles;
    private readonly IRepository<BloodAttributeDefinition>? _bloodAttributes;
    private readonly IRepository<UnitBloodAttribute>? _unitBloodAttributes;
    private readonly IRepository<SpecimenTypeDefinition>? _specimenTypes;
    private readonly IRepository<ExceptionDefinition>? _exceptionDefinitions;
    private readonly IRepository<Override>? _overrides;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<ReflexRule>? _reflexRules;
    private readonly RuleEngineService? _ruleEngine;
    private readonly IRepository<Allocation>? _allocations;
    private readonly FacilityPolicyService? _policy;
    private readonly IRepository<Patient>? _patients;

    public ResultService(
        IRepository<TestResult> results,
        IRepository<Specimen> specimens,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IRepository<TestDefinition>? testDefinitions = null,
        IRepository<SubtestDefinition>? subtestDefinitions = null,
        IRepository<Order>? orders = null,
        IRepository<OrderLine>? orderLines = null,
        IInventoryRepository? inventory = null,
        CompatibilityService? compatibility = null,
        IRepository<AntibodyHistory>? antibodies = null,
        IRepository<AntigenProfile>? antigenProfiles = null,
        IRepository<BloodAttributeDefinition>? bloodAttributes = null,
        IRepository<UnitBloodAttribute>? unitBloodAttributes = null,
        IRepository<SpecimenTypeDefinition>? specimenTypes = null,
        IRepository<ExceptionDefinition>? exceptionDefinitions = null,
        IRepository<Override>? overrides = null,
        IPermissionEvaluator? permissions = null,
        IRepository<ReflexRule>? reflexRules = null,
        RuleEngineService? ruleEngine = null,
        IRepository<Allocation>? allocations = null,
        FacilityPolicyService? policy = null,
        IRepository<PhaseDefinition>? phaseDefinitions = null,
        IRepository<Patient>? patients = null)
    {
        _ruleEngine = ruleEngine;
        _allocations = allocations;
        _policy = policy;
        _patients = patients;
        _results = results;
        _specimens = specimens;
        _bloodTypes = bloodTypes;
        _orders = orders;
        _orderLines = orderLines;
        _inventory = inventory;
        _compatibility = compatibility;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _testDefinitions = testDefinitions;
        _subtestDefinitions = subtestDefinitions;
        _phaseDefinitions = phaseDefinitions;
        _antibodies = antibodies;
        _antigenProfiles = antigenProfiles;
        _bloodAttributes = bloodAttributes;
        _unitBloodAttributes = unitBloodAttributes;
        _specimenTypes = specimenTypes;
        _exceptionDefinitions = exceptionDefinitions;
        _overrides = overrides;
        _permissions = permissions;
        _reflexRules = reflexRules;
    }

    public Task<TestResult?> GetAsync(long id, CancellationToken ct = default) =>
        _results.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<TestResult>> GetBySpecimenAsync(long specimenId, CancellationToken ct = default) =>
        _results.ListAsync(r => r.SpecimenId == specimenId, ct);

    public async Task<EvaluationResult<TestResult>> SaveTestResultAsync(SaveTestResultRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var gate = await ValidateSpecimenForEntryAsync(request.SpecimenId, request.TestCode, ct);
        if (!gate.Succeeded)
        {
            return EvaluationResult<TestResult>.Fail(gate.Error!);
        }

        var specimen = gate.Value!;
        var normalizedCode = request.TestCode.Trim().ToUpperInvariant();

        if (_orderLines is not null)
        {
            var line = await _orderLines.GetByIdAsync(request.OrderLineId, ct);
            if (line is null || line.OrderId != request.OrderId || !string.Equals(line.TestCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
            {
                return EvaluationResult<TestResult>.Fail("Order line does not match the requested test.");
            }
        }

        var build = await BuildValueFromRequestAsync(request, normalizedCode, specimen.PatientId, ct);
        if (!build.Succeeded)
        {
            return EvaluationResult<TestResult>.Fail(build.Error!);
        }

        var (value, units, interpretation) = build.Value!;
        var allWarnings = new List<RuleResult>();

        var current = await GetCurrentResultAsync(request.OrderId, normalizedCode, request.SpecimenId, ct);
        OperationResult<TestResult> saveResult;

        if (current is null)
        {
            saveResult = await EnterInternalAsync(request.SpecimenId, request.OrderId, normalizedCode, value, units, interpretation, ct, skipSpecimenCheck: true);
        }
        else if (current.Status == ResultStatus.Verified)
        {
            if (string.IsNullOrWhiteSpace(request.CorrectionReason))
            {
                return EvaluationResult<TestResult>.Fail("A correction reason is required when changing a completed result.");
            }

            saveResult = await CorrectResultAsync(current.Id, value, request.CorrectionReason.Trim(), ct);
            if (saveResult.Succeeded && saveResult.Value is not null)
            {
                saveResult.Value.Units = units;
                saveResult.Value.Interpretation = interpretation;
                _results.Update(saveResult.Value);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }
        else if (current.Status is ResultStatus.Entered or ResultStatus.Corrected)
        {
            saveResult = await UpdateResultInPlaceAsync(current, value, units, interpretation, ct);
        }
        else
        {
            saveResult = await EnterInternalAsync(request.SpecimenId, request.OrderId, normalizedCode, value, units, interpretation, ct, skipSpecimenCheck: true);
        }

        if (!saveResult.Succeeded || saveResult.Value is null)
        {
            return EvaluationResult<TestResult>.Fail(saveResult.Error ?? "Save failed.");
        }

        if (saveResult.Warnings is { Count: > 0 })
        {
            allWarnings.AddRange(saveResult.Warnings);
        }

        var savedValueType = await ResolveResultValueTypeAsync(normalizedCode, ct);
        if (TestDefinitionValidator.IsCrossmatchResultType(savedValueType)
            || string.Equals(normalizedCode, CrossmatchTestCode, StringComparison.OrdinalIgnoreCase))
        {
            var xm = await RecordCrossmatchForResultAsync(request, specimen, saveResult.Value, ct);
            if (!xm.Succeeded)
            {
                return EvaluationResult<TestResult>.Fail(xm.Error!);
            }
        }

        await SyncOrderLineStatusAsync(request.OrderLineId, saveResult.Value, ct);

        if (request.MarkComplete)
        {
            var verifyRequest = new VerifyResultRequest(
                request.OverrideReason,
                request.AuthorizedBy,
                request.HistoryResolution,
                request.SignatureId);
            var verified = await VerifyResultAsync(saveResult.Value.Id, verifyRequest, ct);
            if (!verified.Succeeded)
            {
                return verified;
            }

            if (verified.Evaluation?.Warnings is { Count: > 0 })
            {
                allWarnings.AddRange(verified.Evaluation.Warnings);
            }

            await SyncOrderLineStatusAsync(request.OrderLineId, verified.Value!, ct);
            return EvaluationResult<TestResult>.Ok(verified.Value!, new RuleEvaluation(allWarnings));
        }

        return EvaluationResult<TestResult>.Ok(saveResult.Value, new RuleEvaluation(allWarnings));
    }

    public async Task<TestResult?> GetCurrentResultAsync(long orderId, string testCode, long specimenId, CancellationToken ct = default)
    {
        var normalized = testCode.ToUpperInvariant();
        var candidates = await _results.ListAsync(
            r => r.OrderId == orderId && r.SpecimenId == specimenId && r.TestCode == normalized && r.SupersededByResultId == null,
            ct);
        return candidates.OrderByDescending(r => r.Version).FirstOrDefault();
    }

    public Task<OperationResult<TestResult>> EnterResultAsync(EnterResultRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnterInternalAsync(request.SpecimenId, request.OrderId, request.TestCode, request.Value, request.Units, request.Interpretation, ct);
    }

    public async Task<OperationResult<TestResult>> EnterAboRhAsync(EnterAboRhRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string value;
        IReadOnlyList<RuleResult> panelWarnings = Array.Empty<RuleResult>();

        if (request.Subtests is { Count: > 0 })
        {
            var panel = new AboRhPanelResult(request.Abo, request.RhD, request.Subtests);
            var configured = await GetPanelSubtestsForCodeAsync(AboRhTestCode, ct);
            var evaluation = AboRhPanelValidator.Validate(panel, configured);
            if (evaluation.IsHardStopped)
            {
                return OperationResult<TestResult>.Fail(
                    string.Join("; ", evaluation.HardStops.Select(h => h.Message)));
            }

            var logicEval = await ValidateInterpretationLogicAsync(AboRhTestCode, InterpretationLogicDefinitions.BuildAboRhKey(request.Abo, request.RhD), request.Subtests, ct);
            if (logicEval.IsHardStopped)
            {
                return OperationResult<TestResult>.Fail(
                    string.Join("; ", logicEval.HardStops.Select(h => h.Message)));
            }

            panelWarnings = evaluation.Warnings.Concat(logicEval.Warnings).ToList();
            value = AboRhResultValue.FormatPanel(panel);
        }
        else
        {
            value = AboRhResultValue.Format(request.Abo, request.RhD);
        }

        var result = await EnterInternalAsync(request.SpecimenId, request.OrderId, AboRhTestCode, value, null, null, ct);
        if (!result.Succeeded)
        {
            return result;
        }

        if (panelWarnings.Count == 0)
        {
            return result;
        }

        var merged = result.Warnings?.Concat(panelWarnings).ToList() ?? panelWarnings.ToList();
        return OperationResult<TestResult>.Ok(result.Value, merged);
    }

    private async Task<OperationResult<TestResult>> EnterInternalAsync(
        long specimenId, long? orderId, string testCode, string value, string? units, string? interpretation, CancellationToken ct,
        bool skipSpecimenCheck = false)
    {
        if (string.IsNullOrWhiteSpace(testCode))
        {
            return OperationResult<TestResult>.Fail("Test code is required.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return OperationResult<TestResult>.Fail("Result value is required.");
        }

        if (!skipSpecimenCheck)
        {
            var gate = await ValidateSpecimenForEntryAsync(specimenId, testCode, ct);
            if (!gate.Succeeded)
            {
                return OperationResult<TestResult>.Fail(gate.Error!);
            }
        }

        var specimen = await _specimens.GetByIdAsync(specimenId, ct);
        if (specimen is null)
        {
            return OperationResult<TestResult>.Fail("Specimen not found.");
        }

        var normalizedCode = testCode.ToUpperInvariant();
        var warnings = await ValidateAgainstCatalogAsync(normalizedCode, value, ct);

        var result = new TestResult
        {
            SpecimenId = specimen.Id,
            PatientId = specimen.PatientId,
            OrderId = orderId,
            TestCode = normalizedCode,
            Version = 1,
            Value = value,
            Units = units,
            Interpretation = interpretation,
            Status = ResultStatus.Entered,
            EnteredBy = _currentUser.UserName,
            EnteredUtc = _clock.UtcNow
        };

        await _results.AddAsync(result, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<TestResult>.Ok(result, warnings);
    }

    /// <summary>
    /// Consumes the admin <see cref="TestDefinition"/> catalog when present: warns (never
    /// hard-blocks) if the code has no active definition or the value is outside the
    /// configured allowed set. Falls back to permissive behavior when no catalog is wired
    /// or seeded, so existing workflows keep running.
    /// </summary>
    private async Task<IReadOnlyList<RuleResult>> ValidateAgainstCatalogAsync(string code, string value, CancellationToken ct)
    {
        if (_testDefinitions is null)
        {
            return Array.Empty<RuleResult>();
        }

        var def = await _testDefinitions.FirstOrDefaultAsync(d => d.IsActive && d.Code == code, ct);
        if (def is null)
        {
            var catalogPopulated = await _testDefinitions.AnyAsync(d => d.IsActive, ct);
            return catalogPopulated
                ? new[] { RuleResult.Warning("RESULT.TESTCODE.UNKNOWN", $"No active test definition exists for code '{code}'.") }
                : Array.Empty<RuleResult>();
        }

        if (def.ResultValueType == ResultValueType.Coded && !string.IsNullOrWhiteSpace(def.AllowedResultValues))
        {
            var allowed = def.AllowedResultValues
                .Split(new[] { '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (allowed.Length > 0 && !allowed.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase)))
            {
                return new[] { RuleResult.Warning("RESULT.VALUE.NOTALLOWED", $"Value '{value}' is not in the allowed set for '{code}'.") };
            }
        }

        return Array.Empty<RuleResult>();
    }

    public async Task<IReadOnlyList<PanelSubtestDefinition>> GetPanelSubtestsForCodeAsync(string testCode, CancellationToken ct = default)
    {
        if (_testDefinitions is null)
        {
            return PanelSubtestDefinitions.DefaultAboRh();
        }

        var def = await _testDefinitions.FirstOrDefaultAsync(d => d.IsActive && d.Code == testCode, ct);
        if (def is null)
        {
            return string.Equals(testCode, AboRhTestCode, StringComparison.OrdinalIgnoreCase)
                ? PanelSubtestDefinitions.DefaultAboRh()
                : Array.Empty<PanelSubtestDefinition>();
        }

        var assignments = PanelSubtestAssignments.Parse(def.PanelSubtestsJson);
        if (assignments.Count == 0 && def.ResultValueType == ResultValueType.AboRh)
        {
            assignments = PanelSubtestDefinitions.DefaultAboRh()
                .Select(s => new PanelSubtestAssignment(s.Code, s.Required, s.SortOrder))
                .ToList();
        }

        return assignments
            .Select(a => new PanelSubtestDefinition(a.SubtestCode, a.SubtestCode, a.Required, a.SortOrder))
            .ToList();
    }

    private async Task<RuleEvaluation> ValidateInterpretationLogicAsync(
        string testCode,
        string interpretationKey,
        IReadOnlyDictionary<string, string> subtests,
        CancellationToken ct)
    {
        if (_testDefinitions is null || _subtestDefinitions is null)
        {
            return new RuleEvaluation(Array.Empty<RuleResult>());
        }

        var def = await _testDefinitions.FirstOrDefaultAsync(d => d.IsActive && d.Code == testCode, ct);
        if (def is null || string.IsNullOrWhiteSpace(def.InterpretationLogicJson))
        {
            return new RuleEvaluation(Array.Empty<RuleResult>());
        }

        var logicRows = InterpretationLogicDefinitions.Parse(def.InterpretationLogicJson);
        var catalog = await LoadActiveSubtestCatalogAsync(ct);
        var phases = await LoadActivePhasesAsync(ct);

        return InterpretationLogicValidator.Validate(logicRows, catalog, interpretationKey, subtests, phases);
    }

    private async Task<Dictionary<string, SubtestDefinition>> LoadActiveSubtestCatalogAsync(CancellationToken ct)
    {
        if (_subtestDefinitions is null)
        {
            return new Dictionary<string, SubtestDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        var items = await _subtestDefinitions.ListAsync(s => s.IsActive && !s.IsDraft, ct);
        return items
            .GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Version).First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, PhaseDefinition>> LoadActivePhasesAsync(CancellationToken ct)
    {
        if (_phaseDefinitions is null)
        {
            return new Dictionary<string, PhaseDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        var items = await _phaseDefinitions.ListAsync(p => p.IsActive && !p.IsDraft, ct);
        return items
            .GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Version).First(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies an entered/corrected result. For ABO/Rh, a historical discrepancy is a
    /// blocking Warning until an authorized override supplies Retain or Replace.
    /// </summary>
    public async Task<EvaluationResult<TestResult>> VerifyResultAsync(
        long resultId,
        VerifyResultRequest? request = null,
        CancellationToken ct = default)
    {
        request ??= new VerifyResultRequest();

        var result = await _results.GetByIdAsync(resultId, ct);
        if (result is null)
        {
            return EvaluationResult<TestResult>.Fail("Result not found.");
        }

        var specimenGate = await ValidateSpecimenForEntryAsync(result.SpecimenId, result.TestCode, ct);
        if (!specimenGate.Succeeded)
        {
            return EvaluationResult<TestResult>.Fail(specimenGate.Error!);
        }

        var selfVerify = SelfVerifyRule.Evaluate(result.EnteredBy ?? string.Empty, _currentUser.UserName, blockSelfVerify: false);
        if (_policy is not null)
        {
            var block = await _policy.GetBlockSelfVerifyAsync(ct);
            selfVerify = SelfVerifyRule.Evaluate(result.EnteredBy ?? string.Empty, _currentUser.UserName, block);
        }

        if (selfVerify.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TestResult>.Blocked(new RuleEvaluation([selfVerify]));
        }

        if (result.Status is not (ResultStatus.Entered or ResultStatus.Corrected))
        {
            return EvaluationResult<TestResult>.Fail($"A result with status {result.Status} cannot be verified.");
        }

        var now = _clock.UtcNow;
        var warnings = new List<RuleResult>();
        Override? authorizedOverride = null;
        AboRhHistoryResolution? historyResolution = null;

        if (result.TestCode == AboRhTestCode && AboRhResultValue.TryParse(result.Value, out var aboRh))
        {
            var current = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == result.PatientId && h.IsCurrent, ct);
            var delta = AboRhDeltaRule.Evaluate(current?.BloodType, aboRh);

            if (delta.Severity == RuleSeverity.Warning)
            {
                var evaluation = new RuleEvaluation([delta]);
                var overrideAttempt = IsDeltaOverrideAttempt(request);

                if (!overrideAttempt)
                {
                    return EvaluationResult<TestResult>.Blocked(evaluation);
                }

                if (string.IsNullOrWhiteSpace(request.OverrideReason) || string.IsNullOrWhiteSpace(request.AuthorizedBy))
                {
                    return EvaluationResult<TestResult>.Blocked(evaluation);
                }

                if (request.HistoryResolution is null)
                {
                    return EvaluationResult<TestResult>.Blocked(evaluation);
                }

                ExceptionDefinition? definition = null;
                if (_exceptionDefinitions is not null)
                {
                    definition = await _exceptionDefinitions.FirstOrDefaultAsync(
                        e => e.RuleCode == AboRhDeltaRule.DeltaCode && e.IsActive, ct);
                }

                var userLevel = _permissions is null
                    ? 0
                    : await _permissions.GetMaxSecurityLevelAsync(_currentUser.UserName, ct);
                var access = ExceptionOverridePolicy.EvaluateAccess(userLevel, definition, AboRhDeltaRule.DeltaCode);
                if (access.Severity == RuleSeverity.HardStop)
                {
                    return EvaluationResult<TestResult>.Blocked(new RuleEvaluation([access, delta]));
                }

                historyResolution = request.HistoryResolution;
                authorizedOverride = new Override
                {
                    Action = OverrideAction.WarningOverride,
                    ContextType = nameof(TestResult),
                    ContextId = result.Id,
                    RuleCode = AboRhDeltaRule.DeltaCode,
                    Reason = request.OverrideReason.Trim(),
                    AuthorizedBy = request.AuthorizedBy.Trim(),
                    SignatureId = request.SignatureId,
                    OverriddenUtc = now,
                    Resolution = historyResolution.Value.ToString()
                };

                if (_overrides is not null)
                {
                    await _overrides.AddAsync(authorizedOverride, ct);
                }

                warnings.Add(delta);
            }

            result.Status = ResultStatus.Verified;
            result.VerifiedBy = _currentUser.UserName;
            result.VerifiedUtc = now;

            if (historyResolution == AboRhHistoryResolution.Retain)
            {
                // Verified panel value stays on TestResult; historical IsCurrent unchanged.
            }
            else
            {
                // Replace (or first type / matching type): append and flip current.
                await AppendBloodTypeFromResultAsync(result, aboRh, ct, flipCurrent: true);
            }
        }
        else
        {
            result.Status = ResultStatus.Verified;
            result.VerifiedBy = _currentUser.UserName;
            result.VerifiedUtc = now;
            await ApplyBloodAttributeResultAsync(result, ct);
            warnings.AddRange(await ApplyAntibodyIdentificationResultAsync(result, ct));
        }

        if (authorizedOverride is not null)
        {
            _audit.Record(
                AuditEventType.Override,
                nameof(TestResult),
                result.Id,
                newValue: new
                {
                    authorizedOverride.RuleCode,
                    authorizedOverride.Action,
                    authorizedOverride.Resolution,
                    authorizedOverride.AuthorizedBy
                },
                reason: authorizedOverride.Reason);
        }

        await ApplyReflexRulesAsync(result, ct);

        if (_ruleEngine is not null)
        {
            var ruleOutcome = await _ruleEngine.ApplyTestRulesAsync(result, ct);
            warnings.AddRange(ruleOutcome.Warnings);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TestResult>.Ok(result, warnings.Count > 0 ? new RuleEvaluation(warnings) : null);
    }

    private async Task ApplyReflexRulesAsync(TestResult result, CancellationToken ct)
    {
        if (_reflexRules is null || _orderLines is null || result.OrderId is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(result.TestCode))
        {
            return;
        }

        var triggerCode = result.TestCode.Trim().ToUpperInvariant();
        var resultValue = result.Value?.Trim() ?? string.Empty;
        var interpretation = ResultInterpretation.Resolve(result.Interpretation, result.Value)?.Trim() ?? string.Empty;

        var rules = await _reflexRules.ListAsync(
            r => r.IsActive && !r.IsDraft && r.TriggerTestCode == triggerCode,
            ct);

        var matches = rules
            .Where(r =>
            {
                var trigger = r.TriggerResultValue.Trim();
                return string.Equals(trigger, interpretation, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trigger, resultValue, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
        if (matches.Count == 0)
        {
            return;
        }

        var orderId = result.OrderId.Value;
        var existingLines = (await _orderLines.ListAsync(l => l.OrderId == orderId && l.IsActive, ct)).ToList();
        var existingTestCodes = existingLines
            .Where(l => l.LineCategory == OrderCategory.Test && !string.IsNullOrWhiteSpace(l.TestCode))
            .Select(l => l.TestCode!.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nextLineNumber = existingLines.Count == 0 ? 1 : existingLines.Max(l => l.LineNumber) + 1;
        var addedLines = new List<OrderLine>();

        foreach (var rule in matches)
        {
            var reflexCode = rule.ReflexTestCode.Trim().ToUpperInvariant();
            if (!existingTestCodes.Add(reflexCode))
            {
                continue;
            }

            var lineName = await ResolveReflexTestNameAsync(reflexCode, ct);
            var line = new OrderLine
            {
                OrderId = orderId,
                LineNumber = nextLineNumber++,
                LineCategory = OrderCategory.Test,
                LineName = lineName,
                TestCode = reflexCode,
                OrderType = OrderLineBuilder.MapTestOrderType(reflexCode),
                ResultStatus = ResultStatus.Pending,
                IsActive = true
            };
            await _orderLines.AddAsync(line, ct);
            addedLines.Add(line);

            _audit.Record(
                AuditEventType.Create,
                nameof(OrderLine),
                orderId,
                newValue: new
                {
                    TriggerResultId = result.Id,
                    TriggerTestCode = triggerCode,
                    TriggerResultValue = resultValue,
                    ReflexTestCode = reflexCode,
                    ReflexRuleCode = rule.Code
                },
                reason: $"Reflex from verified {triggerCode}={resultValue}");
        }

        if (addedLines.Count > 0 && _orders is not null)
        {
            var order = await _orders.GetByIdAsync(orderId, ct);
            if (order is not null)
            {
                var allLines = existingLines.Concat(addedLines).ToList();
                OrderLineBuilder.ApplyHeaderFromLines(order, allLines);
                if (order.ResultStatus == ResultStatus.Verified)
                {
                    order.ResultStatus = ResultStatus.Entered;
                }

                _orders.Update(order);
            }
        }
    }

    private async Task<string> ResolveReflexTestNameAsync(string testCode, CancellationToken ct)
    {
        if (_testDefinitions is not null)
        {
            var def = await _testDefinitions.FirstOrDefaultAsync(
                d => d.IsActive && !d.IsDraft && d.Code == testCode, ct);
            if (def is not null)
            {
                return def.Name;
            }
        }

        return testCode;
    }

    private static bool IsDeltaOverrideAttempt(VerifyResultRequest request) =>
        !string.IsNullOrWhiteSpace(request.OverrideReason)
        || !string.IsNullOrWhiteSpace(request.AuthorizedBy)
        || request.HistoryResolution is not null
        || request.SignatureId is not null;

    private async Task AppendBloodTypeFromResultAsync(
        TestResult result,
        AboRh aboRh,
        CancellationToken ct,
        bool flipCurrent)
    {
        if (!flipCurrent)
        {
            return;
        }

        var current = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == result.PatientId && h.IsCurrent, ct);
        if (current is not null)
        {
            current.IsCurrent = false;
            _bloodTypes.Update(current);
        }

        await _bloodTypes.AddAsync(new PatientBloodTypeHistory
        {
            PatientId = result.PatientId,
            Abo = aboRh.Abo,
            RhD = aboRh.Rh,
            Source = BloodTypeSource.TestResult,
            SourceResultId = result.Id,
            IsCurrent = true
        }, ct);
    }

    /// <summary>
    /// Dangerous action: correcting a verified result. Creates a new version that
    /// supersedes the original and requires its own verification; the original
    /// clinical value is preserved. Records a named Correct audit event with reason.
    /// </summary>
    public async Task<OperationResult<TestResult>> CorrectResultAsync(long resultId, string newValue, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<TestResult>.Fail("A reason is required to correct a result.");
        }

        if (string.IsNullOrWhiteSpace(newValue))
        {
            return OperationResult<TestResult>.Fail("A new value is required to correct a result.");
        }

        var original = await _results.GetByIdAsync(resultId, ct);
        if (original is null)
        {
            return OperationResult<TestResult>.Fail("Result not found.");
        }

        if (original.Status != ResultStatus.Verified)
        {
            return OperationResult<TestResult>.Fail("Only a verified result can be corrected.");
        }

        var merged = await RejectMergedPatientMessageAsync(original.PatientId, ct);
        if (merged is not null)
        {
            return OperationResult<TestResult>.Fail(merged);
        }

        var correction = new TestResult
        {
            SpecimenId = original.SpecimenId,
            PatientId = original.PatientId,
            OrderId = original.OrderId,
            TestCode = original.TestCode,
            Version = original.Version + 1,
            Value = newValue,
            Units = original.Units,
            Status = ResultStatus.Corrected,
            EnteredBy = _currentUser.UserName,
            EnteredUtc = _clock.UtcNow,
            CorrectionReason = reason
        };

        // Navigation link fixes up SupersededByResultId after the new row's Id is
        // generated, keeping the supersede + insert in one atomic save.
        original.SupersededByResult = correction;

        await _results.AddAsync(correction, ct);

        _audit.Record(
            AuditEventType.Correct,
            nameof(TestResult),
            original.Id,
            oldValue: new { original.Value, original.Status, original.Version },
            newValue: new { correction.Value, NewVersion = correction.Version },
            reason: reason);

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<TestResult>.Ok(correction);
    }

    private async Task<OperationResult<Specimen>> ValidateSpecimenForEntryAsync(long specimenId, string testCode, CancellationToken ct)
    {
        var specimen = await _specimens.GetByIdAsync(specimenId, ct);
        if (specimen is null)
        {
            return OperationResult<Specimen>.Fail("Specimen not found.");
        }

        if (specimen.Status != SpecimenStatus.Accepted)
        {
            return OperationResult<Specimen>.Fail($"Specimen {specimen.AccessionNumber} is {specimen.Status}; only Accepted specimens allow result entry.");
        }

        if (_patients is not null)
        {
            var patient = await _patients.GetByIdAsync(specimen.PatientId, ct);
            if (patient is not null)
            {
                var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
                if (clinical.Severity == RuleSeverity.HardStop)
                {
                    return OperationResult<Specimen>.Fail(clinical.Message);
                }
            }
        }

        if (specimen.ExpiresUtc.HasValue && specimen.ExpiresUtc.Value <= _clock.UtcNow)
        {
            return OperationResult<Specimen>.Fail($"Specimen {specimen.AccessionNumber} has expired.");
        }

        if (_testDefinitions is not null)
        {
            var normalized = testCode.Trim().ToUpperInvariant();
            var def = await _testDefinitions.FirstOrDefaultAsync(d => d.IsActive && d.Code == normalized, ct);
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_specimenTypes is not null)
            {
                var typeDef = await _specimenTypes.FirstOrDefaultAsync(
                    t => t.IsActive && !t.IsDraft && t.Code == specimen.SpecimenType, ct);
                if (typeDef is not null)
                {
                    excluded = SpecimenTypeExcludedTests.Parse(typeDef.ExcludedTestCodesJson)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
            }

            var compatibility = SpecimenTypeCompatibilityRule.Evaluate(
                specimen.SpecimenType,
                normalized,
                def?.RequiredSpecimenType,
                excluded);
            if (compatibility.IsHardStopped)
            {
                return OperationResult<Specimen>.Fail(compatibility.HardStops.First().Message);
            }
        }

        return OperationResult<Specimen>.Ok(specimen);
    }

    private async Task<OperationResult<(string Value, string? Units, string? Interpretation)>> BuildValueFromRequestAsync(
        SaveTestResultRequest request, string normalizedCode, long patientId, CancellationToken ct)
    {
        var valueType = await ResolveResultValueTypeAsync(normalizedCode, ct);

        if (TestDefinitionValidator.IsCrossmatchResultType(valueType)
            || string.Equals(normalizedCode, CrossmatchTestCode, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.UnitNumber))
            {
                return OperationResult<(string, string?, string?)>.Fail("A unit number is required for crossmatch results.");
            }

            if (request.CrossmatchMethod is null || request.CrossmatchResult is null)
            {
                return OperationResult<(string, string?, string?)>.Fail("Crossmatch method and result are required.");
            }

            if (request.CrossmatchResult is not (CrossmatchResult.Compatible or CrossmatchResult.Incompatible))
            {
                return OperationResult<(string, string?, string?)>.Fail(
                    "Crossmatch result must be Compatible or Incompatible.");
            }

            var reservation = await FindUnitReservationErrorAsync(request.UnitNumber, patientId, ct);
            if (reservation is not null)
            {
                return OperationResult<(string, string?, string?)>.Fail(reservation);
            }

            var interpretation = $"Unit: {request.UnitNumber.Trim()}";
            string value;
            if (request.Subtests is { Count: > 0 })
            {
                var configured = await GetPanelSubtestsForCodeAsync(normalizedCode, ct);
                var missing = configured
                    .Where(c => c.Required && !request.Subtests.ContainsKey(c.Code))
                    .Select(c => c.Code)
                    .ToList();
                if (missing.Count > 0)
                {
                    return OperationResult<(string, string?, string?)>.Fail(
                        $"Required cell reaction subtests missing: {string.Join(", ", missing)}.");
                }

                value = PanelResultValue.Format(request.Subtests);
                interpretation = $"{interpretation}; {request.CrossmatchResult.Value}";
            }
            else
            {
                value = request.CrossmatchResult.Value.ToString();
            }

            return OperationResult<(string, string?, string?)>.Ok((value, null, interpretation));
        }

        if (valueType == ResultValueType.AboRh)
        {
            if (request.Abo is null || request.RhD is null)
            {
                return OperationResult<(string, string?, string?)>.Fail("Interpreted ABO and Rh(D) are required.");
            }

            string value;
            if (request.Subtests is { Count: > 0 })
            {
                var panel = new AboRhPanelResult(request.Abo.Value, request.RhD.Value, request.Subtests);
                var configured = await GetPanelSubtestsForCodeAsync(normalizedCode, ct);
                var evaluation = AboRhPanelValidator.Validate(panel, configured);
                if (evaluation.IsHardStopped)
                {
                    return OperationResult<(string, string?, string?)>.Fail(
                        string.Join("; ", evaluation.HardStops.Select(h => h.Message)));
                }

                var logicEval = await ValidateInterpretationLogicAsync(
                    normalizedCode,
                    InterpretationLogicDefinitions.BuildAboRhKey(request.Abo.Value, request.RhD.Value),
                    request.Subtests,
                    ct);
                if (logicEval.IsHardStopped)
                {
                    return OperationResult<(string, string?, string?)>.Fail(
                        string.Join("; ", logicEval.HardStops.Select(h => h.Message)));
                }

                value = AboRhResultValue.FormatPanel(panel);
            }
            else
            {
                value = AboRhResultValue.Format(request.Abo.Value, request.RhD.Value);
            }

            return OperationResult<(string, string?, string?)>.Ok((value, null, request.Interpretation));
        }

        if (valueType == ResultValueType.BloodAttribute)
        {
            if (!BloodAttributeResultValue.TryParse(request.Value, out var rows) || rows.Count == 0)
            {
                return OperationResult<(string, string?, string?)>.Fail("Blood attribute results are required.");
            }

            var def = _testDefinitions is null
                ? null
                : await _testDefinitions.FirstOrDefaultAsync(d => d.IsActive && d.Code == normalizedCode, ct);
            string? interpretation = request.Interpretation;
            if (def?.ContributesToUnitBloodAttributes == true)
            {
                if (string.IsNullOrWhiteSpace(request.UnitNumber))
                {
                    return OperationResult<(string, string?, string?)>.Fail("A unit number is required for unit blood attribute results.");
                }

                interpretation = $"Unit: {request.UnitNumber.Trim()}";
            }

            return OperationResult<(string, string?, string?)>.Ok((request.Value.Trim(), null, interpretation));
        }

        if (valueType == ResultValueType.Subtest)
        {
            if (request.Subtests is not { Count: > 0 })
            {
                return OperationResult<(string, string?, string?)>.Fail("Panel subtest results are required.");
            }

            var subtestDef = _testDefinitions is null
                ? null
                : await _testDefinitions.FirstOrDefaultAsync(d => d.IsActive && d.Code == normalizedCode, ct);
            var hasLogic = subtestDef is not null
                && !string.IsNullOrWhiteSpace(subtestDef.InterpretationLogicJson)
                && InterpretationLogicDefinitions.Parse(subtestDef.InterpretationLogicJson).Count > 0;

            if (hasLogic && string.IsNullOrWhiteSpace(request.Interpretation))
            {
                return OperationResult<(string, string?, string?)>.Fail("Interpretation is required for this subtest panel.");
            }

            if (subtestDef is not null)
            {
                var assignments = PanelSubtestAssignments.Parse(subtestDef.PanelSubtestsJson);
                var phases = await LoadActivePhasesAsync(ct);
                var requiredEval = PanelPhaseEntryValidator.ValidateRequired(assignments, phases, request.Subtests);
                if (requiredEval.IsHardStopped)
                {
                    return OperationResult<(string, string?, string?)>.Fail(
                        string.Join("; ", requiredEval.HardStops.Select(h => h.Message)));
                }

                if (_subtestDefinitions is not null)
                {
                    var catalog = await LoadActiveSubtestCatalogAsync(ct);
                    var qcEval = CheckCellQcValidator.Validate(assignments, phases, catalog, request.Subtests);
                    if (qcEval.IsHardStopped)
                    {
                        return OperationResult<(string, string?, string?)>.Fail(
                            string.Join("; ", qcEval.HardStops.Select(h => h.Message)));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Interpretation))
            {
                var logicEval = await ValidateInterpretationLogicAsync(
                    normalizedCode, request.Interpretation.Trim(), request.Subtests, ct);
                if (logicEval.IsHardStopped)
                {
                    return OperationResult<(string, string?, string?)>.Fail(
                        string.Join("; ", logicEval.HardStops.Select(h => h.Message)));
                }
            }

            var interpretationLabel = request.Interpretation;
            if (hasLogic && subtestDef is not null && !string.IsNullOrWhiteSpace(request.Interpretation))
            {
                var logicRow = InterpretationLogicDefinitions.Parse(subtestDef.InterpretationLogicJson)
                    .FirstOrDefault(r => string.Equals(r.InterpretationKey, request.Interpretation.Trim(), StringComparison.OrdinalIgnoreCase));
                interpretationLabel = logicRow?.Label ?? request.Interpretation;
            }

            return OperationResult<(string, string?, string?)>.Ok(
                (PanelResultValue.Format(request.Subtests), null, interpretationLabel));
        }

        if (string.IsNullOrWhiteSpace(request.Value))
        {
            return OperationResult<(string, string?, string?)>.Fail("Result value is required.");
        }

        return OperationResult<(string, string?, string?)>.Ok((request.Value.Trim(), request.Units, request.Interpretation));
    }

    private async Task<ResultValueType> ResolveResultValueTypeAsync(string testCode, CancellationToken ct)
    {
        if (_testDefinitions is null)
        {
            return string.Equals(testCode, AboRhTestCode, StringComparison.OrdinalIgnoreCase)
                ? ResultValueType.AboRh
                : ResultValueType.Coded;
        }

        var def = await _testDefinitions.FirstOrDefaultAsync(d => d.IsActive && d.Code == testCode, ct);
        return def?.ResultValueType ?? ResultValueType.Coded;
    }

    private async Task<OperationResult<TestResult>> UpdateResultInPlaceAsync(
        TestResult existing, string value, string? units, string? interpretation, CancellationToken ct)
    {
        existing.Value = value;
        existing.Units = units;
        existing.Interpretation = interpretation;
        existing.EnteredBy = _currentUser.UserName;
        existing.EnteredUtc = _clock.UtcNow;
        _results.Update(existing);
        await _unitOfWork.SaveChangesAsync(ct);
        var warnings = await ValidateAgainstCatalogAsync(existing.TestCode, value, ct);
        return OperationResult<TestResult>.Ok(existing, warnings);
    }

    /// <summary>
    /// A crossmatch may only be recorded against a unit allocated to this patient, so a stale or
    /// mistyped unit number cannot attach a result to another patient's blood. Any allocation state
    /// counts, because a crossmatch still needs correcting after the unit has been issued and its
    /// reservation consumed. Returns the reason the unit is unusable, or null when it is allowed.
    /// </summary>
    private async Task<string?> FindUnitReservationErrorAsync(string unitNumber, long patientId, CancellationToken ct)
    {
        if (_inventory is null || _allocations is null)
        {
            return null;
        }

        var trimmed = unitNumber.Trim();
        var units = await _inventory.SearchAsync(new InventorySearchCriteria(UnitNumber: trimmed), ct);
        var unit = units.FirstOrDefault();
        if (unit is null)
        {
            return $"Unit '{trimmed}' was not found.";
        }

        var allocated = await _allocations.FirstOrDefaultAsync(
            a => a.PatientId == patientId && a.BloodProductId == unit.Id, ct);

        return allocated is null
            ? $"Unit {trimmed} is not reserved for this patient. Reserve the unit for the patient before crossmatching it."
            : null;
    }

    private async Task<OperationResult<TestResult>> RecordCrossmatchForResultAsync(
        SaveTestResultRequest request, Specimen specimen, TestResult result, CancellationToken ct)
    {
        if (_inventory is null || _compatibility is null)
        {
            return OperationResult<TestResult>.Fail("Crossmatch recording is not configured.");
        }

        var units = await _inventory.SearchAsync(new InventorySearchCriteria(UnitNumber: request.UnitNumber!.Trim()), ct);
        var unit = units.FirstOrDefault();
        if (unit is null)
        {
            return OperationResult<TestResult>.Fail($"Unit '{request.UnitNumber}' was not found.");
        }

        var xm = await _compatibility.RecordCrossmatchAsync(new RecordCrossmatchRequest(
            unit.Id,
            specimen.PatientId,
            specimen.Id,
            request.CrossmatchMethod!.Value,
            request.CrossmatchResult!.Value,
            request.AntibodyScreenNegative ?? true,
            result.Interpretation), ct);

        if (!xm.Succeeded)
        {
            return OperationResult<TestResult>.Fail(xm.Error ?? "Crossmatch could not be recorded.");
        }

        return OperationResult<TestResult>.Ok(result);
    }

    /// <summary>
    /// SoftBank/SafeTrace post identified antibodies to the patient record when ABID
    /// is verified. Catalog matches reuse the blood-attribute history path; unmatched
    /// anti-* tokens are stored as free-text history and surface <c>RES-ABID-UNMATCHED</c>.
    /// Historical antibodies are never removed by a later negative or different ID.
    /// </summary>
    private async Task<IReadOnlyList<RuleResult>> ApplyAntibodyIdentificationResultAsync(
        TestResult result, CancellationToken ct)
    {
        if (_testDefinitions is null || _antibodies is null)
        {
            return Array.Empty<RuleResult>();
        }

        var def = await _testDefinitions.FirstOrDefaultAsync(
            d => d.IsActive && d.Code == result.TestCode, ct);
        if (def is null
            || !def.ContributesToAntibodyHistory
            || def.ResultValueType is not (ResultValueType.FreeText or ResultValueType.Coded))
        {
            return Array.Empty<RuleResult>();
        }

        var catalogEntities = _bloodAttributes is null
            ? []
            : await _bloodAttributes.ListAsync(d => d.IsActive, ct);
        var catalog = catalogEntities
            .Select(d => new AntibodyCatalogItem(d.Id, d.Code, d.Name, d.AntibodyName))
            .ToList();
        var hits = AntibodyIdentificationParser.Resolve(result.Value, catalog);
        if (hits.Count == 0)
        {
            return Array.Empty<RuleResult>();
        }

        var posted = new List<string>();
        var unmatched = new List<string>();

        foreach (var hit in hits)
        {
            if (hit.CatalogItem is { } item)
            {
                var attrDef = catalogEntities.First(d => d.Id == item.Id);
                await ApplyPatientAntibodyResultAsync(
                    result.PatientId, attrDef, AntigenResult.Positive, result.Id, ct);
                posted.Add(attrDef.AntibodyName);
                continue;
            }

            if (!AntibodyIdentificationParser.LooksLikeAntibodyToken(hit.Token))
            {
                continue;
            }

            await ApplyFreeTextAntibodyResultAsync(result.PatientId, hit.Token, result.Id, ct);
            posted.Add(hit.Token);
            unmatched.Add(hit.Token);
        }

        if (posted.Count == 0)
        {
            return Array.Empty<RuleResult>();
        }

        _audit.Record(
            AuditEventType.Update,
            nameof(AntibodyHistory),
            result.PatientId,
            newValue: new
            {
                SourceResultId = result.Id,
                TestCode = result.TestCode,
                Specificities = posted
            },
            reason: $"Identified on verified {result.TestCode}.");

        return unmatched.Count == 0
            ? Array.Empty<RuleResult>()
            : new[]
            {
                RuleResult.Warning(
                    AntibodyIdentificationParser.UnmatchedRuleCode,
                    $"Antibody identification includes specificities not in the catalog: {string.Join(", ", unmatched)}. Posted as free-text history.")
            };
    }

    private async Task ApplyFreeTextAntibodyResultAsync(
        long patientId, string specificity, long sourceResultId, CancellationToken ct)
    {
        var existing = await _antibodies!.FirstOrDefaultAsync(
            a => a.PatientId == patientId
                 && a.BloodAttributeDefinitionId == null
                 && a.IsActive
                 && a.AntibodySpecificity == specificity, ct);
        if (existing is not null)
        {
            existing.Status = AntibodyStatus.Identified;
            existing.SourceResultId = sourceResultId;
            _antibodies.Update(existing);
            return;
        }

        await _antibodies.AddAsync(new AntibodyHistory
        {
            PatientId = patientId,
            AntibodySpecificity = specificity,
            Status = AntibodyStatus.Identified,
            IsActive = true,
            SourceResultId = sourceResultId,
            Comment = "Posted from verified antibody identification."
        }, ct);
    }

    private async Task ApplyBloodAttributeResultAsync(TestResult result, CancellationToken ct)
    {
        if (_testDefinitions is null || _bloodAttributes is null)
        {
            return;
        }

        var def = await _testDefinitions.FirstOrDefaultAsync(
            d => d.IsActive && d.Code == result.TestCode, ct);
        if (def is null || def.ResultValueType != ResultValueType.BloodAttribute)
        {
            return;
        }

        if (!BloodAttributeResultValue.TryParse(result.Value, out var rows))
        {
            return;
        }

        var kind = def.BloodAttributeScopeKind ?? BloodAttributeKind.Antigen;
        var catalog = await _bloodAttributes.ListAsync(d => d.IsActive, ct);
        // Case-sensitive: catalog includes distinct Rh C / c (and similar) codes.
        var byCode = catalog.ToDictionary(d => d.Code, StringComparer.Ordinal);

        var updatesUnit = def.ContributesToUnitBloodAttributes;
        long? unitId = null;
        if (updatesUnit && _inventory is not null)
        {
            var unitNumber = ParseUnitNumberFromInterpretation(result.Interpretation);
            if (!string.IsNullOrWhiteSpace(unitNumber))
            {
                var units = await _inventory.SearchAsync(new InventorySearchCriteria(UnitNumber: unitNumber), ct);
                unitId = units.FirstOrDefault()?.Id;
            }
        }

        foreach (var row in rows)
        {
            if (!byCode.TryGetValue(row.Code, out var attrDef))
            {
                continue;
            }

            if (updatesUnit)
            {
                if (unitId is not null && _unitBloodAttributes is not null)
                {
                    await UpsertUnitBloodAttributeAsync(unitId.Value, attrDef.Id, kind, row.Result, result.Id, ct);
                }

                continue;
            }

            if (kind == BloodAttributeKind.Antigen && _antigenProfiles is not null)
            {
                await UpsertAntigenProfileAsync(result.PatientId, attrDef.Id, row.Result, result.Id, ct);
            }
            else if (kind == BloodAttributeKind.Antibody && _antibodies is not null)
            {
                await ApplyPatientAntibodyResultAsync(result.PatientId, attrDef, row.Result, result.Id, ct);
            }
        }
    }

    private static string? ParseUnitNumberFromInterpretation(string? interpretation)
    {
        if (string.IsNullOrWhiteSpace(interpretation))
        {
            return null;
        }

        const string prefix = "Unit: ";
        return interpretation.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? interpretation[prefix.Length..].Trim()
            : null;
    }

    private async Task UpsertAntigenProfileAsync(
        long patientId, long attributeDefinitionId, AntigenResult resultValue, long sourceResultId, CancellationToken ct)
    {
        var existing = await _antigenProfiles!.FirstOrDefaultAsync(
            p => p.PatientId == patientId && p.BloodAttributeDefinitionId == attributeDefinitionId, ct);
        if (existing is null)
        {
            await _antigenProfiles.AddAsync(new AntigenProfile
            {
                PatientId = patientId,
                BloodAttributeDefinitionId = attributeDefinitionId,
                Result = resultValue,
                TestedUtc = _clock.UtcNow,
                TestedBy = _currentUser.UserName,
                SourceResultId = sourceResultId
            }, ct);
        }
        else
        {
            existing.Result = resultValue;
            existing.TestedUtc = _clock.UtcNow;
            existing.TestedBy = _currentUser.UserName;
            existing.SourceResultId = sourceResultId;
            _antigenProfiles.Update(existing);
        }
    }

    private async Task ApplyPatientAntibodyResultAsync(
        long patientId, BloodAttributeDefinition attrDef, AntigenResult resultValue, long sourceResultId, CancellationToken ct)
    {
        if (resultValue == AntigenResult.Positive)
        {
            var existing = await _antibodies!.FirstOrDefaultAsync(
                a => a.PatientId == patientId
                     && a.BloodAttributeDefinitionId == attrDef.Id
                     && a.IsActive, ct);
            if (existing is null)
            {
                await _antibodies.AddAsync(new AntibodyHistory
                {
                    PatientId = patientId,
                    BloodAttributeDefinitionId = attrDef.Id,
                    AntibodySpecificity = attrDef.AntibodyName,
                    Status = AntibodyStatus.Identified,
                    IsActive = true,
                    SourceResultId = sourceResultId
                }, ct);
            }
            else
            {
                existing.AntibodySpecificity = attrDef.AntibodyName;
                existing.Status = AntibodyStatus.Identified;
                existing.SourceResultId = sourceResultId;
                _antibodies.Update(existing);
            }

            return;
        }

        if (resultValue == AntigenResult.Negative)
        {
            var active = await _antibodies!.FirstOrDefaultAsync(
                a => a.PatientId == patientId
                     && a.BloodAttributeDefinitionId == attrDef.Id
                     && a.IsActive, ct);
            if (active is not null)
            {
                active.IsActive = false;
                active.DeactivationReason = "Negative on verified blood attribute test result.";
                active.SourceResultId = sourceResultId;
                _antibodies.Update(active);
            }
        }
    }

    private async Task UpsertUnitBloodAttributeAsync(
        long unitId, long attributeDefinitionId, BloodAttributeKind kind, AntigenResult resultValue, long sourceResultId, CancellationToken ct)
    {
        var existing = await _unitBloodAttributes!.FirstOrDefaultAsync(
            a => a.BloodProductId == unitId
                 && a.BloodAttributeDefinitionId == attributeDefinitionId
                 && a.AttributeKind == kind, ct);
        if (existing is null)
        {
            await _unitBloodAttributes.AddAsync(new UnitBloodAttribute
            {
                BloodProductId = unitId,
                BloodAttributeDefinitionId = attributeDefinitionId,
                AttributeKind = kind,
                Result = resultValue,
                SourceResultId = sourceResultId
            }, ct);
        }
        else
        {
            existing.Result = resultValue;
            existing.SourceResultId = sourceResultId;
            _unitBloodAttributes.Update(existing);
        }
    }

    private async Task SyncOrderLineStatusAsync(long orderLineId, TestResult result, CancellationToken ct)
    {
        if (_orderLines is null || _orders is null)
        {
            return;
        }

        var line = await _orderLines.GetByIdAsync(orderLineId, ct);
        if (line is null)
        {
            return;
        }

        line.ResultStatus = result.Status switch
        {
            ResultStatus.Verified => ResultStatus.Verified,
            ResultStatus.Corrected => ResultStatus.Corrected,
            ResultStatus.Entered => ResultStatus.Entered,
            _ => ResultStatus.Pending
        };
        _orderLines.Update(line);

        var order = await _orders.GetByIdAsync(line.OrderId, ct);
        if (order is null)
        {
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var testLines = await _orderLines.ListAsync(
            l => l.OrderId == line.OrderId && l.IsActive && l.LineCategory == OrderCategory.Test, ct);
        if (testLines.Count == 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var lineStatuses = testLines
            .Select(l => l.Id == line.Id ? line.ResultStatus : l.ResultStatus)
            .ToList();
        order.ResultStatus = lineStatuses.All(s => s == ResultStatus.Verified)
            ? ResultStatus.Verified
            : lineStatuses.Any(s => s is ResultStatus.Entered or ResultStatus.Corrected or ResultStatus.Verified)
                ? ResultStatus.Entered
                : ResultStatus.Pending;
        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<string?> RejectMergedPatientMessageAsync(long patientId, CancellationToken ct)
    {
        if (_patients is null)
        {
            return null;
        }

        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return null;
        }

        var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
        return clinical.Severity == RuleSeverity.HardStop ? clinical.Message : null;
    }
}
