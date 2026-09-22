using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class BloodSupplierAdminService : ConfigAdminServiceBase
{
    private readonly IRepository<BloodSupplier> _suppliers;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public BloodSupplierAdminService(
        IRepository<BloodSupplier> suppliers,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _suppliers = suppliers;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<BloodSupplierDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _suppliers.ListAsync(ct)
            : await _suppliers.ListAsync(s => s.IsActive, ct);
        return list.OrderBy(s => s.Code).Select(BloodSupplierDto.From).ToList();
    }

    public async Task<BloodSupplierDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _suppliers.GetByIdAsync(id, ct);
        return entity is null ? null : BloodSupplierDto.From(entity);
    }

    public async Task<EvaluationResult<BloodSupplierDto>> CreateAsync(SaveBloodSupplierRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, BloodSupplierAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var code = NormalizeCode(request.Code);
        if (await _suppliers.AnyAsync(s => s.Code == code, ct))
        {
            return EvaluationResult<BloodSupplierDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop("SUPPLIER.CODE.DUPLICATE", $"Supplier code '{code}' already exists.")]));
        }

        var entity = BuildEntity(code, request);
        var validation = BloodSupplierValidator.Validate(entity, duplicateCode: false);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<BloodSupplierDto>.Blocked(validation);
        }

        await _suppliers.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = BloodSupplierDto.From(entity);
        RecordChange("BloodSupplier", entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<BloodSupplierDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<BloodSupplierDto>> UpdateAsync(long id, SaveBloodSupplierRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, BloodSupplierAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _suppliers.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<BloodSupplierDto>.Fail("Blood supplier not found.");
        }

        var code = NormalizeCode(request.Code);
        var duplicate = await _suppliers.AnyAsync(s => s.Code == code && s.Id != id, ct);
        var old = BloodSupplierDto.From(entity);

        Apply(entity, code, request);

        var validation = BloodSupplierValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<BloodSupplierDto>.Blocked(validation);
        }

        _suppliers.Update(entity);
        var dto = BloodSupplierDto.From(entity);
        RecordChange("BloodSupplier", entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<BloodSupplierDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<BloodSupplierDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate,
            active ? BloodSupplierAuthorizationRule.EvaluateActivate : BloodSupplierAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _suppliers.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<BloodSupplierDto>.Fail("Blood supplier not found.");
        }

        var old = BloodSupplierDto.From(entity);
        entity.IsActive = active;
        _suppliers.Update(entity);
        var dto = BloodSupplierDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange("BloodSupplier", entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<BloodSupplierDto>.Ok(dto, new RuleEvaluation([]));
    }

    private static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private static BloodSupplier BuildEntity(string code, SaveBloodSupplierRequest request)
    {
        var entity = new BloodSupplier { Code = code, IsActive = true };
        Apply(entity, code, request);
        return entity;
    }

    private static void Apply(BloodSupplier entity, string code, SaveBloodSupplierRequest request)
    {
        entity.Code = code;
        entity.Name = request.Name?.Trim() ?? string.Empty;
        entity.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        entity.Registration = request.Registration?.Trim() ?? string.Empty;
    }

    private async Task<EvaluationResult<BloodSupplierDto>?> RejectUnauthorizedAsync(
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
            ? EvaluationResult<BloodSupplierDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
