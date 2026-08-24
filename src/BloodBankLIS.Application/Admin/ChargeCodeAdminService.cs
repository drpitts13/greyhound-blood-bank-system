using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class ChargeCodeAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ChargeCode);

    private readonly IRepository<ChargeCode> _codes;

    public ChargeCodeAdminService(
        IRepository<ChargeCode> codes,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _codes = codes;
    }

    public async Task<IReadOnlyList<ChargeCodeDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _codes.ListAsync(ct)
            : await _codes.ListAsync(c => c.IsActive, ct);
        return list.OrderBy(c => c.Code).Select(ChargeCodeDto.From).ToList();
    }

    public async Task<ChargeCodeDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _codes.GetByIdAsync(id, ct);
        return entity is null ? null : ChargeCodeDto.From(entity);
    }

    public async Task<EvaluationResult<ChargeCodeDto>> CreateAsync(SaveChargeCodeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new ChargeCode { IsActive = true };
        Apply(entity, request);

        var duplicate = await _codes.AnyAsync(c => c.Code == entity.Code, ct);
        var validation = ChargeCodeValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ChargeCodeDto>.Blocked(validation);
        }

        await _codes.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = ChargeCodeDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Create, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ChargeCodeDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ChargeCodeDto>> UpdateAsync(long id, SaveChargeCodeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await _codes.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ChargeCodeDto>.Fail("Charge code not found.");
        }

        var old = ChargeCodeDto.From(entity);
        Apply(entity, request);

        var duplicate = await _codes.AnyAsync(c => c.Code == entity.Code && c.Id != id, ct);
        var validation = ChargeCodeValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ChargeCodeDto>.Blocked(validation);
        }

        _codes.Update(entity);
        var dto = ChargeCodeDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Update, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ChargeCodeDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ChargeCodeDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var entity = await _codes.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ChargeCodeDto>.Fail("Charge code not found.");
        }

        var old = ChargeCodeDto.From(entity);
        entity.IsActive = active;
        _codes.Update(entity);
        var dto = ChargeCodeDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ChargeCodeDto>.Ok(dto, new RuleEvaluation([]));
    }

    private static void Apply(ChargeCode entity, SaveChargeCodeRequest request)
    {
        entity.Code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        entity.Description = request.Description?.Trim() ?? string.Empty;
        entity.DefaultAmount = request.DefaultAmount;
        entity.CptCode = string.IsNullOrWhiteSpace(request.CptCode) ? null : request.CptCode.Trim();
    }
}
