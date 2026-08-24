using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class ChargeRuleAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ChargeRule);

    private readonly IRepository<ChargeRule> _rules;
    private readonly IRepository<ChargeCode> _codes;

    public ChargeRuleAdminService(
        IRepository<ChargeRule> rules,
        IRepository<ChargeCode> codes,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _rules = rules;
        _codes = codes;
    }

    public async Task<IReadOnlyList<ChargeRuleDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var rules = includeInactive
            ? await _rules.ListAsync(ct)
            : await _rules.ListAsync(r => r.IsActive, ct);
        var codes = await LoadCodeMapAsync(ct);
        return rules
            .OrderBy(r => r.TriggerType)
            .ThenBy(r => r.TriggerKey ?? string.Empty)
            .Select(r => ChargeRuleDto.From(r, codes.GetValueOrDefault(r.ChargeCodeId)))
            .ToList();
    }

    public async Task<ChargeRuleDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _rules.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return null;
        }

        var code = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
        return ChargeRuleDto.From(entity, code);
    }

    public async Task<EvaluationResult<ChargeRuleDto>> CreateAsync(SaveChargeRuleRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new ChargeRule { IsActive = true };
        Apply(entity, request);

        var code = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
        var duplicate = await HasActiveDuplicateAsync(entity, 0, ct);
        var validation = ChargeRuleValidator.Validate(entity, chargeCodeMissing: code is null, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ChargeRuleDto>.Blocked(validation);
        }

        await _rules.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = ChargeRuleDto.From(entity, code);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Create, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ChargeRuleDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ChargeRuleDto>> UpdateAsync(long id, SaveChargeRuleRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await _rules.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ChargeRuleDto>.Fail("Charge rule not found.");
        }

        var oldCode = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
        var old = ChargeRuleDto.From(entity, oldCode);
        Apply(entity, request);

        var code = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
        var duplicate = entity.IsActive && await HasActiveDuplicateAsync(entity, entity.Id, ct);
        var validation = ChargeRuleValidator.Validate(entity, chargeCodeMissing: code is null, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ChargeRuleDto>.Blocked(validation);
        }

        _rules.Update(entity);
        var dto = ChargeRuleDto.From(entity, code);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Update, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ChargeRuleDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ChargeRuleDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var entity = await _rules.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ChargeRuleDto>.Fail("Charge rule not found.");
        }

        if (active)
        {
            var code = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
            var duplicate = await HasActiveDuplicateAsync(entity, entity.Id, ct);
            var validation = ChargeRuleValidator.Validate(entity, chargeCodeMissing: code is null, duplicate);
            if (validation.IsHardStopped)
            {
                return EvaluationResult<ChargeRuleDto>.Blocked(validation);
            }
        }

        var oldCode = await _codes.GetByIdAsync(entity.ChargeCodeId, ct);
        var old = ChargeRuleDto.From(entity, oldCode);
        entity.IsActive = active;
        _rules.Update(entity);
        var dto = ChargeRuleDto.From(entity, oldCode);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ChargeRuleDto>.Ok(dto, new RuleEvaluation([]));
    }

    private async Task<bool> HasActiveDuplicateAsync(ChargeRule entity, long selfId, CancellationToken ct)
    {
        var key = entity.TriggerKey;
        return await _rules.AnyAsync(
            r => r.IsActive
                 && r.Id != selfId
                 && r.TriggerType == entity.TriggerType
                 && r.ChargeCodeId == entity.ChargeCodeId
                 && r.TriggerKey == key,
            ct);
    }

    private async Task<Dictionary<long, ChargeCode>> LoadCodeMapAsync(CancellationToken ct)
    {
        var codes = await _codes.ListAsync(ct);
        return codes.ToDictionary(c => c.Id);
    }

    private static void Apply(ChargeRule entity, SaveChargeRuleRequest request)
    {
        entity.TriggerType = request.TriggerType;
        entity.TriggerKey = string.IsNullOrWhiteSpace(request.TriggerKey) ? null : request.TriggerKey.Trim();
        entity.ChargeCodeId = request.ChargeCodeId;
    }
}
