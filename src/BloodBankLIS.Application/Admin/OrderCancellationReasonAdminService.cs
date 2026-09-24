using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class OrderCancellationReasonAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(OrderCancellationReason);

    private readonly IRepository<OrderCancellationReason> _reasons;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public OrderCancellationReasonAdminService(
        IRepository<OrderCancellationReason> reasons,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _reasons = reasons;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<OrderCancellationReasonDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var rows = includeInactive
            ? await _reasons.ListAsync(ct)
            : await _reasons.ListAsync(r => r.IsActive, ct);
        return rows
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .Select(OrderCancellationReasonDto.From)
            .ToList();
    }

    public async Task<OrderCancellationReasonDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _reasons.GetByIdAsync(id, ct);
        return entity is null ? null : OrderCancellationReasonDto.From(entity);
    }

    public async Task<EvaluationResult<OrderCancellationReasonDto>> CreateAsync(
        SaveOrderCancellationReasonRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, OrderCancellationReasonAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new OrderCancellationReason
        {
            Code = Normalize(request.Code),
            Text = request.Text?.Trim() ?? string.Empty,
            SortOrder = request.SortOrder,
            IsActive = true
        };
        var duplicate = await _reasons.AnyAsync(r => r.Code == entity.Code, ct);
        var validation = OrderCancellationReasonValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<OrderCancellationReasonDto>.Blocked(validation);
        }

        await _reasons.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = OrderCancellationReasonDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<OrderCancellationReasonDto>.Ok(dto);
    }

    public async Task<EvaluationResult<OrderCancellationReasonDto>> UpdateAsync(
        long id, SaveOrderCancellationReasonRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, OrderCancellationReasonAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _reasons.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<OrderCancellationReasonDto>.Fail("Cancellation reason not found.");
        }

        var old = OrderCancellationReasonDto.From(entity);
        entity.Code = Normalize(request.Code);
        entity.Text = request.Text?.Trim() ?? string.Empty;
        entity.SortOrder = request.SortOrder;
        var duplicate = await _reasons.AnyAsync(r => r.Id != id && r.Code == entity.Code, ct);
        var validation = OrderCancellationReasonValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<OrderCancellationReasonDto>.Blocked(validation);
        }

        _reasons.Update(entity);
        var dto = OrderCancellationReasonDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<OrderCancellationReasonDto>.Ok(dto);
    }

    public async Task<EvaluationResult<OrderCancellationReasonDto>> SetActiveAsync(
        long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate,
            active
                ? OrderCancellationReasonAuthorizationRule.EvaluateActivate
                : OrderCancellationReasonAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _reasons.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<OrderCancellationReasonDto>.Fail("Cancellation reason not found.");
        }

        var old = OrderCancellationReasonDto.From(entity);
        entity.IsActive = active;
        _reasons.Update(entity);
        var dto = OrderCancellationReasonDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        var audit = active ? AuditEventType.Activate : AuditEventType.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, audit, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<OrderCancellationReasonDto>.Ok(dto);
    }

    private static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private async Task<EvaluationResult<OrderCancellationReasonDto>?> RejectUnauthorizedAsync(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<OrderCancellationReasonDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
