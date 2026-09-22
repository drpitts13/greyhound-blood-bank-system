using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class ActionDefinitionAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ActionDefinition);

    private readonly IRepository<ActionDefinition> _rows;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public ActionDefinitionAdminService(
        IRepository<ActionDefinition> rows,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _rows = rows;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<ActionDefinitionDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _rows.ListAsync(ct)
            : await _rows.ListAsync(r => r.IsActive, ct);
        return list.OrderBy(r => r.Code).Select(ActionDefinitionDto.From).ToList();
    }

    public async Task<ActionDefinitionDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _rows.GetByIdAsync(id, ct);
        return entity is null ? null : ActionDefinitionDto.From(entity);
    }

    public async Task<EvaluationResult<ActionDefinitionDto>> CreateAsync(SaveActionDefinitionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, ActionDefinitionAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new ActionDefinition { IsActive = true };
        Apply(entity, request);

        var duplicate = await _rows.AnyAsync(r => r.Code == entity.Code, ct);
        var validation = ActionDefinitionValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ActionDefinitionDto>.Blocked(validation);
        }

        await _rows.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = ActionDefinitionDto.From(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ActionDefinitionDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ActionDefinitionDto>> UpdateAsync(long id, SaveActionDefinitionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, ActionDefinitionAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ActionDefinitionDto>.Fail("Action not found.");
        }

        var old = ActionDefinitionDto.From(entity);
        Apply(entity, request);
        entity.Version++;

        var duplicate = await _rows.AnyAsync(r => r.Code == entity.Code && r.Id != id, ct);
        var validation = ActionDefinitionValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ActionDefinitionDto>.Blocked(validation);
        }

        _rows.Update(entity);
        var dto = ActionDefinitionDto.From(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ActionDefinitionDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ActionDefinitionDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate,
            active ? ActionDefinitionAuthorizationRule.EvaluateActivate : ActionDefinitionAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ActionDefinitionDto>.Fail("Action not found.");
        }

        var old = ActionDefinitionDto.From(entity);
        entity.IsActive = active;
        _rows.Update(entity);
        var dto = ActionDefinitionDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, entity.Version, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ActionDefinitionDto>.Ok(dto, new RuleEvaluation([]));
    }

    private static void Apply(ActionDefinition entity, SaveActionDefinitionRequest request)
    {
        entity.Code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        entity.Name = request.Name?.Trim() ?? string.Empty;
        entity.Category = request.Category;
        entity.ModificationType = request.Category == ActionCategory.Modification
            ? request.ModificationType
            : null;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
    }

    private async Task<EvaluationResult<ActionDefinitionDto>?> RejectUnauthorizedAsync(
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
            ? EvaluationResult<ActionDefinitionDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
