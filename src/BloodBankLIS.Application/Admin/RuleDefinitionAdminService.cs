using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Domain.Rules.Engine;

namespace BloodBankLIS.Application.Admin;

public sealed class RuleDefinitionAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(RuleDefinition);

    private readonly IRepository<RuleDefinition> _repo;
    private readonly IRepository<TestDefinition> _testRepo;
    private readonly IRepository<TestGrouper> _grouperRepo;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public RuleDefinitionAdminService(
        IRepository<RuleDefinition> repo,
        IRepository<TestDefinition> testRepo,
        IRepository<TestGrouper> grouperRepo,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _repo = repo;
        _testRepo = testRepo;
        _grouperRepo = grouperRepo;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<RuleDefinitionDto>> ListAsync(
        bool includeInactive,
        RuleLevel? level = null,
        CancellationToken ct = default)
    {
        var items = includeInactive
            ? await _repo.ListAsync(ct)
            : await _repo.ListAsync(r => r.IsActive, ct);

        return items
            .Where(r => level is null || r.Level == level)
            .OrderBy(r => r.Level)
            .ThenBy(r => r.Priority)
            .ThenBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .Select(Map)
            .ToList();
    }

    public async Task<RuleDefinitionDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<EvaluationResult<RuleDefinitionDto>> CreateAsync(
        SaveRuleDefinitionRequest req,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminTestsManage, RuleDefinitionAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new RuleDefinition { IsDraft = true, IsActive = false, Version = 1 };
        Apply(entity, req);

        var evaluation = await ValidateEntityAsync(entity, 0, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<RuleDefinitionDto>.Blocked(evaluation);
        }

        await _repo.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Configure,
            oldValue: null, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<RuleDefinitionDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<RuleDefinitionDto>> UpdateAsync(
        long id,
        SaveRuleDefinitionRequest req,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminTestsManage, RuleDefinitionAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<RuleDefinitionDto>.Fail("Rule not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<RuleDefinitionDto>.Fail("A change reason is required to edit an active rule.");
        }

        var before = Map(entity);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var evaluation = await ValidateEntityAsync(entity, entity.Id, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<RuleDefinitionDto>.Blocked(evaluation);
        }

        _repo.Update(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Configure,
            oldValue: before, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<RuleDefinitionDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<RuleDefinitionDto>> ActivateAsync(
        long id,
        string? reason,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminConfigActivate, RuleDefinitionAuthorizationRule.EvaluateActivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<RuleDefinitionDto>.Fail("Rule not found.");
        }

        var evaluation = await ValidateEntityAsync(entity, entity.Id, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<RuleDefinitionDto>.Blocked(evaluation);
        }

        entity.IsActive = true;
        entity.IsDraft = false;
        entity.RetiredUtc = null;
        entity.EffectiveUtc ??= Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Activate, AuditEventType.Activate,
            oldValue: null, newValue: Map(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<RuleDefinitionDto>.Ok(Map(entity), evaluation);
    }

    public async Task<OperationResult<RuleDefinitionDto>> DeactivateAsync(
        long id,
        string? reason,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate, RuleDefinitionAuthorizationRule.EvaluateDeactivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<RuleDefinitionDto>.Fail("Rule not found.");
        }

        entity.IsActive = false;
        entity.RetiredUtc = Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: Map(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<RuleDefinitionDto>.Ok(Map(entity));
    }

    /// <summary>
    /// Dry-runs the validator against unsaved expressions so the authoring UI can report
    /// syntax and attribute problems before the rule is stored.
    /// </summary>
    public async Task<RuleValidationDto> ValidateAsync(ValidateRuleRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var probe = new RuleDefinition
        {
            Code = "PREVIEW",
            Name = "Preview",
            Level = req.Level,
            ConditionExpression = req.ConditionExpression?.Trim() ?? string.Empty,
            ActionExpression = req.ActionExpression?.Trim() ?? string.Empty
        };

        var evaluation = RuleDefinitionValidator.Validate(probe, duplicateActiveCode: false, await LoadKnownTestCodesAsync(ct));
        var actions = RuleActionParser.TryParse(probe.ActionExpression, probe.Level, out var parsed, out _)
            ? parsed.Select(a => a.ToString()).ToList()
            : new List<string>();

        return new RuleValidationDto(
            !evaluation.IsHardStopped,
            evaluation.HardStops.Select(ToMessage).ToList(),
            evaluation.Warnings.Select(ToMessage).ToList(),
            actions);
    }

    public static RuleVocabularyDto Vocabulary(RuleLevel level) => new(
        level,
        RuleAttributeCatalog.Attributes(level).Select(ToDto).ToList(),
        RuleAttributeCatalog.Functions(level).Select(ToDto).ToList(),
        RuleActionParser.For(level).Select(ToDto).ToList());

    public static RuleHelpDto Help() => new(
        RuleAttributeCatalog.AllAttributesForHelp().Select(ToDto).ToList(),
        RuleAttributeCatalog.AllFunctionsForHelp().Select(ToDto).ToList(),
        RuleAttributeCatalog.Operators
            .Select(o => new RuleOperatorDto(o.Symbol, o.Description, o.Example))
            .ToList(),
        RuleActionParser.Descriptors.Select(ToDto).ToList());

    private static RuleAttributeDto ToDto(RuleAttributeDescriptor a) =>
        new(a.Path, a.Kind.ToString(), a.Description, a.Example, AvailabilityOf(a.MinimumLevel));

    private static RuleFunctionDto ToDto(RuleFunctionDescriptor f) =>
        new(f.Name, f.ReturnKind.ToString(), f.Description, f.Example, AvailabilityOf(f.MinimumLevel));

    private static RuleActionDto ToDto(RuleActionDescriptor a) => new(
        a.Name,
        a.Description,
        a.Example,
        a.RestrictedTo is null ? BothLevels : $"{a.RestrictedTo} rules only");

    /// <summary>Order-level attributes are also in scope for test rules; test-level ones are not shared back.</summary>
    private static string AvailabilityOf(RuleLevel minimumLevel) =>
        minimumLevel == RuleLevel.Order ? BothLevels : $"{RuleLevel.Test} rules only";

    private const string BothLevels = "Order and Test rules";

    private async Task<RuleEvaluation> ValidateEntityAsync(RuleDefinition entity, long selfId, CancellationToken ct)
    {
        var duplicateCode = await HasActiveDuplicateCodeAsync(entity.Code, selfId, ct);
        var knownTestCodes = await LoadKnownTestCodesAsync(ct);
        return RuleDefinitionValidator.Validate(entity, duplicateCode, knownTestCodes);
    }

    /// <summary>Active test definition codes plus grouper codes, which are also orderable.</summary>
    private async Task<HashSet<string>> LoadKnownTestCodesAsync(CancellationToken ct)
    {
        var tests = await _testRepo.ListAsync(t => t.IsActive && !t.IsDraft, ct);
        var groupers = await _grouperRepo.ListAsync(g => g.IsActive && !g.IsDraft, ct);

        return tests.Select(t => t.Code)
            .Concat(groupers.Select(g => g.Code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> HasActiveDuplicateCodeAsync(string code, long selfId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim().ToUpperInvariant();
        return await _repo.AnyAsync(r => r.IsActive && r.Id != selfId && r.Code == normalized, ct);
    }

    private static RuleMessageDto ToMessage(RuleResult result) =>
        new(result.Code, result.Severity.ToString(), result.Message);

    private static void Apply(RuleDefinition e, SaveRuleDefinitionRequest req)
    {
        e.Code = (req.Code ?? string.Empty).Trim().ToUpperInvariant();
        e.Name = req.Name?.Trim() ?? string.Empty;
        e.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        e.Level = req.Level;
        e.Priority = req.Priority;
        e.StopOnMatch = req.StopOnMatch;
        e.ConditionExpression = req.ConditionExpression?.Trim() ?? string.Empty;
        e.ActionExpression = req.ActionExpression?.Trim() ?? string.Empty;
        e.ChangeReason = req.ChangeReason;
    }

    private async Task<EvaluationResult<RuleDefinitionDto>?> RejectUnauthorizedEvalAsync(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(
            CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<RuleDefinitionDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<OperationResult<RuleDefinitionDto>?> RejectUnauthorizedAsync(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(
            CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<RuleDefinitionDto>.Fail(auth.Message)
            : null;
    }

    private static RuleDefinitionDto Map(RuleDefinition r) => new(
        r.Id,
        r.Code,
        r.Name,
        r.Description,
        r.Level,
        r.Priority,
        r.StopOnMatch,
        r.ConditionExpression,
        r.ActionExpression,
        r.Version,
        r.IsActive,
        r.IsDraft,
        r.EffectiveUtc,
        r.RetiredUtc,
        r.ChangeReason);
}
