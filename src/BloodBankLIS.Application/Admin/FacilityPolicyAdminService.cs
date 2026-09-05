using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class FacilityPolicyAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(SystemSetting);

    private readonly IRepository<SystemSetting> _settings;

    public FacilityPolicyAdminService(
        IRepository<SystemSetting> settings,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _settings = settings;
    }

    public async Task<IReadOnlyList<FacilityPolicyDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _settings.ListAsync(ct);
        var byKey = rows.ToDictionary(s => s.Key, StringComparer.Ordinal);
        var list = new List<FacilityPolicyDto>();

        foreach (var definition in FacilityPolicyCatalog.All)
        {
            if (!byKey.TryGetValue(definition.Key, out var setting))
            {
                setting = new SystemSetting
                {
                    Key = definition.Key,
                    Value = definition.DefaultValue,
                    Category = definition.Category,
                    Description = definition.Description
                };
                await _settings.AddAsync(setting, ct);
                byKey[definition.Key] = setting;
            }

            list.Add(FacilityPolicyDto.From(setting, definition));
        }

        if (list.Any(p => p.Id == 0))
        {
            await UnitOfWork.SaveChangesAsync(ct);
            rows = await _settings.ListAsync(ct);
            byKey = rows.ToDictionary(s => s.Key, StringComparer.Ordinal);
            list = FacilityPolicyCatalog.All
                .Select(d => FacilityPolicyDto.From(byKey[d.Key], d))
                .ToList();
        }

        return list
            .OrderBy(p => p.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<EvaluationResult<FacilityPolicyDto>> UpdateAsync(
        long id,
        SaveFacilityPolicyRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await _settings.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<FacilityPolicyDto>.Fail("Facility policy not found.");
        }

        var definition = FacilityPolicyCatalog.Find(entity.Key);
        if (definition is null)
        {
            return EvaluationResult<FacilityPolicyDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop(FacilityPolicyValidator.UnknownKeyCode, "Unrecognized facility policy key.")]));
        }

        var normalized = Normalize(request.Value, definition);
        var validation = FacilityPolicyValidator.Validate(entity, normalized, request.Reason, entity.LegalHold);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<FacilityPolicyDto>.Blocked(validation);
        }

        if (string.Equals(entity.Value, normalized, StringComparison.OrdinalIgnoreCase)
            && definition.Kind == FacilityPolicyValueKind.Boolean)
        {
            return EvaluationResult<FacilityPolicyDto>.Ok(FacilityPolicyDto.From(entity, definition), validation);
        }

        if (string.Equals(entity.Value, normalized, StringComparison.Ordinal))
        {
            return EvaluationResult<FacilityPolicyDto>.Ok(FacilityPolicyDto.From(entity, definition), validation);
        }

        var old = FacilityPolicyDto.From(entity, definition);
        entity.Value = normalized;
        entity.Category = definition.Category;
        entity.Description = definition.Description;
        _settings.Update(entity);

        var dto = FacilityPolicyDto.From(entity, definition);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, request.Reason.Trim());
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<FacilityPolicyDto>.Ok(dto, validation);
    }

    private static string Normalize(string? value, FacilityPolicyDefinition definition)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (definition.Kind == FacilityPolicyValueKind.Boolean && bool.TryParse(trimmed, out var flag))
        {
            return flag ? "true" : "false";
        }

        return trimmed;
    }
}
