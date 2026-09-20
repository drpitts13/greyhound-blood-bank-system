using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class ActionBillingAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ActionBilling);

    private readonly IRepository<ActionBilling> _rows;
    private readonly IRepository<ActionDefinition> _actions;
    private readonly IRepository<ChargeCode> _codes;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public ActionBillingAdminService(
        IRepository<ActionBilling> rows,
        IRepository<ActionDefinition> actions,
        IRepository<ChargeCode> codes,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _rows = rows;
        _actions = actions;
        _codes = codes;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<ActionBillingDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _rows.ListAsync(ct)
            : await _rows.ListAsync(r => r.IsActive, ct);
        var actions = await LoadActionMapAsync(ct);
        var codes = await LoadCodeMapAsync(ct);
        return list
            .OrderBy(r => actions.GetValueOrDefault(r.ActionDefinitionId)?.Code ?? string.Empty)
            .ThenBy(r => codes.GetValueOrDefault(r.ChargeCodeId)?.Code ?? string.Empty)
            .Select(r => ActionBillingDto.From(
                r,
                actions.GetValueOrDefault(r.ActionDefinitionId),
                codes.GetValueOrDefault(r.ChargeCodeId)))
            .ToList();
    }

    public async Task<ActionBillingDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return null;
        }

        var action = await _actions.GetByIdAsync(entity.ActionDefinitionId, ct);
        var code = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
        return ActionBillingDto.From(entity, action, code);
    }

    public async Task<EvaluationResult<ActionBillingDto>> CreateAsync(SaveActionBillingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, ActionBillingAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new ActionBilling { IsActive = true };
        Apply(entity, request);

        var action = await _actions.GetByIdAsync(entity.ActionDefinitionId, ct);
        var code = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
        var duplicate = await IsDuplicateAsync(entity, excludeId: null, ct);
        var validation = ActionBillingValidator.Validate(entity, code is null, action is null, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ActionBillingDto>.Blocked(validation);
        }

        await _rows.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = ActionBillingDto.From(entity, action, code);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ActionBillingDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ActionBillingDto>> UpdateAsync(long id, SaveActionBillingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, ActionBillingAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ActionBillingDto>.Fail("Action billing row not found.");
        }

        var oldAction = await _actions.GetByIdAsync(entity.ActionDefinitionId, ct);
        var oldCode = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
        var old = ActionBillingDto.From(entity, oldAction, oldCode);
        Apply(entity, request);

        var action = await _actions.GetByIdAsync(entity.ActionDefinitionId, ct);
        var code = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
        var duplicate = await IsDuplicateAsync(entity, id, ct);
        var validation = ActionBillingValidator.Validate(entity, code is null, action is null, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ActionBillingDto>.Blocked(validation);
        }

        _rows.Update(entity);
        var dto = ActionBillingDto.From(entity, action, code);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ActionBillingDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ActionBillingDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate,
            active ? ActionBillingAuthorizationRule.EvaluateActivate : ActionBillingAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ActionBillingDto>.Fail("Action billing row not found.");
        }

        if (active)
        {
            var action = await _actions.GetByIdAsync(entity.ActionDefinitionId, ct);
            var code = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
            var duplicate = await IsDuplicateAsync(entity, id, ct);
            var validation = ActionBillingValidator.Validate(entity, code is null, action is null, duplicate);
            if (validation.IsHardStopped)
            {
                return EvaluationResult<ActionBillingDto>.Blocked(validation);
            }
        }

        var oldAction = await _actions.GetByIdAsync(entity.ActionDefinitionId, ct);
        var oldCode = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
        var old = ActionBillingDto.From(entity, oldAction, oldCode);
        entity.IsActive = active;
        _rows.Update(entity);
        var dto = ActionBillingDto.From(entity, oldAction, oldCode);
        var actionKind = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, 1, actionKind, ToAuditType(actionKind), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ActionBillingDto>.Ok(dto, new RuleEvaluation([]));
    }

    private async Task<bool> IsDuplicateAsync(ActionBilling entity, long? excludeId, CancellationToken ct) =>
        await _rows.AnyAsync(r =>
            r.IsActive
            && r.Trigger == entity.Trigger
            && r.ActionDefinitionId == entity.ActionDefinitionId
            && r.ChargeCodeId == entity.ChargeCodeId
            && (excludeId == null || r.Id != excludeId), ct);

    private async Task<Dictionary<long, ActionDefinition>> LoadActionMapAsync(CancellationToken ct)
    {
        var actions = await _actions.ListAsync(ct);
        return actions.ToDictionary(a => a.Id);
    }

    private async Task<Dictionary<long, ChargeCode>> LoadCodeMapAsync(CancellationToken ct)
    {
        var codes = await _codes.ListAsync(ct);
        return codes.ToDictionary(c => c.Id);
    }

    private static void Apply(ActionBilling entity, SaveActionBillingRequest request)
    {
        entity.ActionDefinitionId = request.ActionDefinitionId;
        entity.ChargeCodeId = request.ChargeCodeId;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Trigger = request.Trigger;
    }

    private async Task<EvaluationResult<ActionBillingDto>?> RejectUnauthorizedAsync(
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
            ? EvaluationResult<ActionBillingDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
