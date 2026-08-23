using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Admin management of expiration modification codes: a numeric offset applied from
/// either the modification date/time or the collection date/time. Validation gates
/// activation; every change is audited and snapshotted.
/// </summary>
public sealed class ExpirationModificationCodeAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ExpirationModificationCode);

    private readonly IRepository<ExpirationModificationCode> _codes;
    private readonly IRepository<ModificationRule> _rules;

    public ExpirationModificationCodeAdminService(
        IRepository<ExpirationModificationCode> codes,
        IRepository<ModificationRule> rules,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _codes = codes;
        _rules = rules;
    }

    public async Task<IReadOnlyList<ExpirationModificationCodeDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var codes = includeInactive ? await _codes.ListAsync(ct) : await _codes.ListAsync(c => c.IsActive, ct);
        return codes.OrderBy(c => c.Code).Select(Map).ToList();
    }

    public async Task<ExpirationModificationCodeDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var code = await _codes.GetByIdAsync(id, ct);
        return code is null ? null : Map(code);
    }

    public async Task<EvaluationResult<ExpirationModificationCodeDto>> CreateAsync(
        SaveExpirationModificationCodeRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = new ExpirationModificationCode { IsActive = false, Version = 1 };
        Apply(entity, req);

        var evaluation = await ValidateAsync(entity, 0, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ExpirationModificationCodeDto>.Blocked(evaluation);
        }

        await _codes.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = Map(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Create,
            oldValue: null, newValue: dto, reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ExpirationModificationCodeDto>.Ok(dto, evaluation);
    }

    public async Task<EvaluationResult<ExpirationModificationCodeDto>> UpdateAsync(
        long id, SaveExpirationModificationCodeRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = await _codes.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ExpirationModificationCodeDto>.Fail("Expiration modification code not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<ExpirationModificationCodeDto>.Fail(
                "A change reason is required to edit an active expiration modification code.");
        }

        var before = Map(entity);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var evaluation = await ValidateAsync(entity, entity.Id, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ExpirationModificationCodeDto>.Blocked(evaluation);
        }

        _codes.Update(entity);

        var after = Map(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Update,
            oldValue: before, newValue: after, reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ExpirationModificationCodeDto>.Ok(after, evaluation);
    }

    public async Task<EvaluationResult<ExpirationModificationCodeDto>> ActivateAsync(
        long id, string? reason, CancellationToken ct = default)
    {
        var entity = await _codes.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ExpirationModificationCodeDto>.Fail("Expiration modification code not found.");
        }

        var evaluation = await ValidateAsync(entity, entity.Id, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ExpirationModificationCodeDto>.Blocked(evaluation);
        }

        entity.IsActive = true;
        _codes.Update(entity);

        var dto = Map(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Activate, AuditEventType.Activate,
            oldValue: null, newValue: dto, reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ExpirationModificationCodeDto>.Ok(dto, evaluation);
    }

    public async Task<OperationResult<ExpirationModificationCodeDto>> DeactivateAsync(
        long id, string? reason, CancellationToken ct = default)
    {
        var entity = await _codes.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<ExpirationModificationCodeDto>.Fail("Expiration modification code not found.");
        }

        if (await _rules.AnyAsync(r => r.IsActive && r.ExpirationModificationCodeId == id, ct))
        {
            return OperationResult<ExpirationModificationCodeDto>.Fail(
                "This expiration code is used by an active modification rule and cannot be deactivated.");
        }

        entity.IsActive = false;
        _codes.Update(entity);

        var dto = Map(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: dto, reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<ExpirationModificationCodeDto>.Ok(dto);
    }

    private async Task<RuleEvaluation> ValidateAsync(
        ExpirationModificationCode entity, long selfId, CancellationToken ct)
    {
        var duplicate = await _codes.AnyAsync(c =>
            c.Id != selfId && c.Code == entity.Code, ct);
        return ExpirationModificationCodeValidator.Validate(entity, duplicate);
    }

    private static void Apply(ExpirationModificationCode e, SaveExpirationModificationCodeRequest req)
    {
        e.Code = (req.Code ?? string.Empty).Trim().ToUpperInvariant();
        e.OffsetAmount = req.OffsetAmount;
        e.OffsetUnit = req.OffsetUnit;
        e.RelativeTo = req.RelativeTo;
        e.Description = req.Description?.Trim();
    }

    private static ExpirationModificationCodeDto Map(ExpirationModificationCode c) =>
        new(c.Id, c.Code, c.OffsetAmount, c.OffsetUnit, c.RelativeTo, c.Description, c.Version, c.IsActive);
}
