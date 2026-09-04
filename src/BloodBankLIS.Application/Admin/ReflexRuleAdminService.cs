using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.Admin;

public sealed class ReflexRuleAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ReflexRule);

    private readonly IRepository<ReflexRule> _repo;
    private readonly IRepository<TestDefinition> _testRepo;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public ReflexRuleAdminService(
        IRepository<ReflexRule> repo,
        IRepository<TestDefinition> testRepo,
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
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<ReflexRuleDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = includeInactive
            ? await _repo.ListAsync(ct)
            : await _repo.ListAsync(r => r.IsActive, ct);
        return items.OrderBy(r => r.Code).Select(Map).ToList();
    }

    public async Task<ReflexRuleDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<EvaluationResult<ReflexRuleDto>> CreateAsync(SaveReflexRuleRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminTestsManage, ReflexRuleAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new ReflexRule { IsDraft = true, IsActive = false, Version = 1 };
        Apply(entity, req);

        var duplicateCode = await HasActiveDuplicateCodeAsync(entity.Code, 0, ct);
        var duplicateTriple = await HasActiveDuplicateTripleAsync(entity, 0, ct);
        var activeTests = await LoadActiveTestCodesAsync(ct);
        var evaluation = ReflexRuleValidator.Validate(entity, duplicateCode, duplicateTriple, activeTests);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ReflexRuleDto>.Blocked(evaluation);
        }

        await _repo.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Create,
            oldValue: null, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ReflexRuleDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<ReflexRuleDto>> UpdateAsync(long id, SaveReflexRuleRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminTestsManage, ReflexRuleAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ReflexRuleDto>.Fail("Reflex rule not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<ReflexRuleDto>.Fail("A change reason is required to edit an active reflex rule.");
        }

        var before = Map(entity);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var duplicateCode = await HasActiveDuplicateCodeAsync(entity.Code, entity.Id, ct);
        var duplicateTriple = await HasActiveDuplicateTripleAsync(entity, entity.Id, ct);
        var activeTests = await LoadActiveTestCodesAsync(ct);
        var evaluation = ReflexRuleValidator.Validate(entity, duplicateCode, duplicateTriple, activeTests);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ReflexRuleDto>.Blocked(evaluation);
        }

        _repo.Update(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Update,
            oldValue: before, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ReflexRuleDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<ReflexRuleDto>> ActivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminConfigActivate, ReflexRuleAuthorizationRule.EvaluateActivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ReflexRuleDto>.Fail("Reflex rule not found.");
        }

        var duplicateCode = await HasActiveDuplicateCodeAsync(entity.Code, entity.Id, ct);
        var duplicateTriple = await HasActiveDuplicateTripleAsync(entity, entity.Id, ct);
        var activeTests = await LoadActiveTestCodesAsync(ct);
        var evaluation = ReflexRuleValidator.Validate(entity, duplicateCode, duplicateTriple, activeTests);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ReflexRuleDto>.Blocked(evaluation);
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

        return EvaluationResult<ReflexRuleDto>.Ok(Map(entity), evaluation);
    }

    public async Task<OperationResult<ReflexRuleDto>> DeactivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate, ReflexRuleAuthorizationRule.EvaluateDeactivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<ReflexRuleDto>.Fail("Reflex rule not found.");
        }

        entity.IsActive = false;
        entity.RetiredUtc = Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: Map(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<ReflexRuleDto>.Ok(Map(entity));
    }

    private async Task<HashSet<string>> LoadActiveTestCodesAsync(CancellationToken ct)
    {
        var tests = await _testRepo.ListAsync(t => t.IsActive && !t.IsDraft, ct);
        return tests.Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
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

    private async Task<bool> HasActiveDuplicateTripleAsync(ReflexRule entity, long selfId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entity.TriggerTestCode)
            || string.IsNullOrWhiteSpace(entity.TriggerResultValue)
            || string.IsNullOrWhiteSpace(entity.ReflexTestCode))
        {
            return false;
        }

        var trigger = entity.TriggerTestCode.Trim().ToUpperInvariant();
        var value = entity.TriggerResultValue.Trim();
        var reflex = entity.ReflexTestCode.Trim().ToUpperInvariant();

        var candidates = await _repo.ListAsync(
            r => r.IsActive && r.Id != selfId
                 && r.TriggerTestCode == trigger
                 && r.ReflexTestCode == reflex,
            ct);

        return candidates.Any(r =>
            string.Equals(r.TriggerResultValue.Trim(), value, StringComparison.OrdinalIgnoreCase));
    }

    private static void Apply(ReflexRule e, SaveReflexRuleRequest req)
    {
        e.Code = (req.Code ?? string.Empty).Trim().ToUpperInvariant();
        e.Name = req.Name?.Trim() ?? string.Empty;
        e.TriggerTestCode = (req.TriggerTestCode ?? string.Empty).Trim().ToUpperInvariant();
        e.TriggerResultValue = req.TriggerResultValue?.Trim() ?? string.Empty;
        e.ReflexTestCode = (req.ReflexTestCode ?? string.Empty).Trim().ToUpperInvariant();
        e.ChangeReason = req.ChangeReason;
    }

    private async Task<EvaluationResult<ReflexRuleDto>?> RejectUnauthorizedEvalAsync(
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
            ? EvaluationResult<ReflexRuleDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<OperationResult<ReflexRuleDto>?> RejectUnauthorizedAsync(
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
            ? OperationResult<ReflexRuleDto>.Fail(auth.Message)
            : null;
    }

    private static ReflexRuleDto Map(ReflexRule r) => new(
        r.Id,
        r.Code,
        r.Name,
        r.TriggerTestCode,
        r.TriggerResultValue,
        r.ReflexTestCode,
        r.Version,
        r.IsActive,
        r.IsDraft,
        r.EffectiveUtc,
        r.RetiredUtc,
        r.ChangeReason);
}
