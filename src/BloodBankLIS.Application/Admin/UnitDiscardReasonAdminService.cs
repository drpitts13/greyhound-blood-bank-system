using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class UnitDiscardReasonAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(UnitDiscardReason);

    private readonly IRepository<UnitDiscardReason> _reasons;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public UnitDiscardReasonAdminService(
        IRepository<UnitDiscardReason> reasons,
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

    public async Task<IReadOnlyList<UnitDiscardReasonDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var rows = includeInactive
            ? await _reasons.ListAsync(ct)
            : await _reasons.ListAsync(r => r.IsActive, ct);
        return rows
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .Select(UnitDiscardReasonDto.From)
            .ToList();
    }

    public async Task<UnitDiscardReasonDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _reasons.GetByIdAsync(id, ct);
        return entity is null ? null : UnitDiscardReasonDto.From(entity);
    }

    public async Task<EvaluationResult<UnitDiscardReasonDto>> CreateAsync(
        SaveUnitDiscardReasonRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, UnitDiscardReasonAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new UnitDiscardReason
        {
            Code = Normalize(request.Code),
            Text = request.Text?.Trim() ?? string.Empty,
            RequiresComment = request.RequiresComment,
            SortOrder = request.SortOrder,
            IsActive = true
        };
        var duplicate = await _reasons.AnyAsync(r => r.Code == entity.Code, ct);
        var validation = UnitDiscardReasonValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<UnitDiscardReasonDto>.Blocked(validation);
        }

        await _reasons.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = UnitDiscardReasonDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<UnitDiscardReasonDto>.Ok(dto);
    }

    public async Task<EvaluationResult<UnitDiscardReasonDto>> UpdateAsync(
        long id, SaveUnitDiscardReasonRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, UnitDiscardReasonAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _reasons.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<UnitDiscardReasonDto>.Fail("Discard reason not found.");
        }

        var old = UnitDiscardReasonDto.From(entity);
        entity.Code = Normalize(request.Code);
        entity.Text = request.Text?.Trim() ?? string.Empty;
        entity.RequiresComment = request.RequiresComment;
        entity.SortOrder = request.SortOrder;
        var duplicate = await _reasons.AnyAsync(r => r.Id != id && r.Code == entity.Code, ct);
        var validation = UnitDiscardReasonValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<UnitDiscardReasonDto>.Blocked(validation);
        }

        _reasons.Update(entity);
        var dto = UnitDiscardReasonDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<UnitDiscardReasonDto>.Ok(dto);
    }

    public async Task<EvaluationResult<UnitDiscardReasonDto>> SetActiveAsync(
        long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate,
            active
                ? UnitDiscardReasonAuthorizationRule.EvaluateActivate
                : UnitDiscardReasonAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _reasons.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<UnitDiscardReasonDto>.Fail("Discard reason not found.");
        }

        var old = UnitDiscardReasonDto.From(entity);
        entity.IsActive = active;
        _reasons.Update(entity);
        var dto = UnitDiscardReasonDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        var audit = active ? AuditEventType.Activate : AuditEventType.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, audit, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<UnitDiscardReasonDto>.Ok(dto);
    }

    private static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private async Task<EvaluationResult<UnitDiscardReasonDto>?> RejectUnauthorizedAsync(
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
            ? EvaluationResult<UnitDiscardReasonDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
