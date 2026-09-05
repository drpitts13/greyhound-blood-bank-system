using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.Admin;

public sealed class PhaseDefinitionAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(PhaseDefinition);

    private readonly IRepository<PhaseDefinition> _repo;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public PhaseDefinitionAdminService(
        IRepository<PhaseDefinition> repo,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _repo = repo;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<PhaseDefinitionDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = includeInactive
            ? await _repo.ListAsync(ct)
            : await _repo.ListAsync(s => s.IsActive, ct);
        return items.OrderBy(s => s.SortOrder).ThenBy(s => s.Code).Select(Map).ToList();
    }

    public async Task<PhaseDefinitionDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<EvaluationResult<PhaseDefinitionDto>> CreateAsync(SavePhaseDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminTestsManage, PhaseCatalogAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new PhaseDefinition { IsDraft = true, IsActive = false, Version = 1 };
        Apply(entity, req);

        var duplicate = await HasActiveDuplicateAsync(entity.Code, 0, ct);
        var evaluation = PhaseDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<PhaseDefinitionDto>.Blocked(evaluation);
        }

        await _repo.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.TestChange,
            oldValue: null, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<PhaseDefinitionDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<PhaseDefinitionDto>> UpdateAsync(long id, SavePhaseDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminTestsManage, PhaseCatalogAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<PhaseDefinitionDto>.Fail("Phase definition not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<PhaseDefinitionDto>.Fail("A change reason is required to edit an active phase definition.");
        }

        var before = Map(entity);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);
        var evaluation = PhaseDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<PhaseDefinitionDto>.Blocked(evaluation);
        }

        _repo.Update(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.TestChange,
            oldValue: before, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<PhaseDefinitionDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<PhaseDefinitionDto>> ActivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminConfigActivate, PhaseCatalogAuthorizationRule.EvaluateActivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<PhaseDefinitionDto>.Fail("Phase definition not found.");
        }

        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);
        var evaluation = PhaseDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<PhaseDefinitionDto>.Blocked(evaluation);
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

        return EvaluationResult<PhaseDefinitionDto>.Ok(Map(entity), evaluation);
    }

    public async Task<OperationResult<PhaseDefinitionDto>> DeactivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate, PhaseCatalogAuthorizationRule.EvaluateDeactivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<PhaseDefinitionDto>.Fail("Phase definition not found.");
        }

        entity.IsActive = false;
        entity.RetiredUtc = Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: Map(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<PhaseDefinitionDto>.Ok(Map(entity));
    }

    private async Task<bool> HasActiveDuplicateAsync(string code, long selfId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();
        return await _repo.AnyAsync(s => s.IsActive && s.Id != selfId && s.Code == normalized, ct);
    }

    private static void Apply(PhaseDefinition e, SavePhaseDefinitionRequest req)
    {
        e.Code = (req.Code ?? string.Empty).Trim();
        e.Name = req.Name?.Trim() ?? string.Empty;
        e.SortOrder = req.SortOrder;
        e.IncludeInInterpretation = req.IsCheckCell ? false : req.IncludeInInterpretation;
        e.IsCheckCell = req.IsCheckCell;
        e.ValidatesPhaseCode = string.IsNullOrWhiteSpace(req.ValidatesPhaseCode)
            ? null
            : req.ValidatesPhaseCode.Trim();
        e.ChangeReason = req.ChangeReason;
    }

    private async Task<EvaluationResult<PhaseDefinitionDto>?> RejectUnauthorizedEvalAsync(
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
            ? EvaluationResult<PhaseDefinitionDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<OperationResult<PhaseDefinitionDto>?> RejectUnauthorizedAsync(
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
            ? OperationResult<PhaseDefinitionDto>.Fail(auth.Message)
            : null;
    }

    private static PhaseDefinitionDto Map(PhaseDefinition p) => new(
        p.Id,
        p.Code,
        p.Name,
        p.SortOrder,
        p.IncludeInInterpretation,
        p.IsCheckCell,
        p.ValidatesPhaseCode,
        p.Version,
        p.IsActive,
        p.IsDraft,
        p.EffectiveUtc,
        p.RetiredUtc,
        p.ChangeReason);
}
