using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class SpecimenRejectionReasonAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(SpecimenRejectionReason);

    private readonly IRepository<SpecimenRejectionReason> _reasons;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public SpecimenRejectionReasonAdminService(
        IRepository<SpecimenRejectionReason> reasons,
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

    public async Task<IReadOnlyList<SpecimenRejectionReasonDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var rows = includeInactive
            ? await _reasons.ListAsync(ct)
            : await _reasons.ListAsync(r => r.IsActive, ct);
        return rows
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .Select(SpecimenRejectionReasonDto.From)
            .ToList();
    }

    public async Task<SpecimenRejectionReasonDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _reasons.GetByIdAsync(id, ct);
        return entity is null ? null : SpecimenRejectionReasonDto.From(entity);
    }

    public async Task<EvaluationResult<SpecimenRejectionReasonDto>> CreateAsync(
        SaveSpecimenRejectionReasonRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, SpecimenRejectionReasonAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new SpecimenRejectionReason
        {
            Code = Normalize(request.Code),
            Text = request.Text?.Trim() ?? string.Empty,
            RequiresComment = request.RequiresComment,
            SortOrder = request.SortOrder,
            IsActive = true
        };
        var duplicate = await _reasons.AnyAsync(r => r.Code == entity.Code, ct);
        var validation = SpecimenRejectionReasonValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<SpecimenRejectionReasonDto>.Blocked(validation);
        }

        await _reasons.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = SpecimenRejectionReasonDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<SpecimenRejectionReasonDto>.Ok(dto);
    }

    public async Task<EvaluationResult<SpecimenRejectionReasonDto>> UpdateAsync(
        long id, SaveSpecimenRejectionReasonRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, SpecimenRejectionReasonAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _reasons.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<SpecimenRejectionReasonDto>.Fail("Rejection reason not found.");
        }

        var old = SpecimenRejectionReasonDto.From(entity);
        entity.Code = Normalize(request.Code);
        entity.Text = request.Text?.Trim() ?? string.Empty;
        entity.RequiresComment = request.RequiresComment;
        entity.SortOrder = request.SortOrder;
        var duplicate = await _reasons.AnyAsync(r => r.Id != id && r.Code == entity.Code, ct);
        var validation = SpecimenRejectionReasonValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<SpecimenRejectionReasonDto>.Blocked(validation);
        }

        _reasons.Update(entity);
        var dto = SpecimenRejectionReasonDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<SpecimenRejectionReasonDto>.Ok(dto);
    }

    public async Task<EvaluationResult<SpecimenRejectionReasonDto>> SetActiveAsync(
        long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate,
            active
                ? SpecimenRejectionReasonAuthorizationRule.EvaluateActivate
                : SpecimenRejectionReasonAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _reasons.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<SpecimenRejectionReasonDto>.Fail("Rejection reason not found.");
        }

        var old = SpecimenRejectionReasonDto.From(entity);
        entity.IsActive = active;
        _reasons.Update(entity);
        var dto = SpecimenRejectionReasonDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        var audit = active ? AuditEventType.Activate : AuditEventType.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, audit, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<SpecimenRejectionReasonDto>.Ok(dto);
    }

    private static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private async Task<EvaluationResult<SpecimenRejectionReasonDto>?> RejectUnauthorizedAsync(
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
            ? EvaluationResult<SpecimenRejectionReasonDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
