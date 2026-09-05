using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Admin management of the global HL7 value-translation table (internal ↔ external
/// codes per data item). Saving a data item replaces only that item's rows.
/// </summary>
public sealed class InterfaceTranslationAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(InterfaceValueTranslation);

    private readonly IInterfaceValueTranslationRepository _translations;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public InterfaceTranslationAdminService(
        IInterfaceValueTranslationRepository translations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _translations = translations;
        _permissionEvaluator = permissionEvaluator;
    }

    public static IReadOnlyList<InterfaceDataItemDto> AllDataItems() =>
        InterfaceDataItemCatalog.AllDistinct()
            .Select(i => new InterfaceDataItemDto(i.Key, i.DisplayName, i.Description, i.DefaultHl7Path, i.Required))
            .ToList();

    public async Task<EvaluationResult<InterfaceTranslationTableDto>> GetAsync(
        string dataItemKey,
        CancellationToken ct = default)
    {
        var catalogError = ValidateCatalogKey(dataItemKey);
        if (catalogError is not null)
        {
            return EvaluationResult<InterfaceTranslationTableDto>.Fail(catalogError);
        }

        var key = dataItemKey.Trim();
        var rows = await _translations.ListAsync(t => t.DataItemKey == key, ct);
        return EvaluationResult<InterfaceTranslationTableDto>.Ok(Map(key, rows));
    }

    public async Task<EvaluationResult<InterfaceTranslationTableDto>> ReplaceAsync(
        string dataItemKey,
        SaveInterfaceTranslationsRequest req,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminHl7Manage, InterfaceTranslationAuthorizationRule.EvaluateReplace, ct);
        if (denied is not null)
        {
            return denied;
        }

        var catalogError = ValidateCatalogKey(dataItemKey);
        if (catalogError is not null)
        {
            return EvaluationResult<InterfaceTranslationTableDto>.Fail(catalogError);
        }

        var key = dataItemKey.Trim();
        var validation = ValidateRows(req.Rows);
        if (validation is not null)
        {
            return EvaluationResult<InterfaceTranslationTableDto>.Fail(validation);
        }

        var existing = await _translations.ListAsync(t => t.DataItemKey == key, ct);
        var before = Map(key, existing);
        var entities = ToEntities(key, req.Rows);
        await _translations.ReplaceForDataItemAsync(key, entities, ct);

        RecordChange(
            EntityType,
            EntityIdFor(key),
            1,
            ConfigChangeAction.Update,
            AuditEventType.Configure,
            before,
            Map(key, entities),
            req.ChangeReason);

        await UnitOfWork.SaveChangesAsync(ct);
        var saved = await _translations.ListAsync(t => t.DataItemKey == key, ct);
        return EvaluationResult<InterfaceTranslationTableDto>.Ok(Map(key, saved));
    }

    private static string? ValidateCatalogKey(string? dataItemKey)
    {
        if (string.IsNullOrWhiteSpace(dataItemKey))
        {
            return "A data item is required.";
        }

        return InterfaceDataItemCatalog.ContainsKey(dataItemKey.Trim())
            ? null
            : $"Unknown data item '{dataItemKey.Trim()}'.";
    }

    private static string? ValidateRows(IReadOnlyList<InterfaceValueTranslationDto>? rows)
    {
        var list = (rows ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.InternalValue) || !string.IsNullOrWhiteSpace(r.ExternalValue))
            .ToList();

        for (var i = 0; i < list.Count; i++)
        {
            var row = list[i];
            if (string.IsNullOrWhiteSpace(row.InternalValue) || string.IsNullOrWhiteSpace(row.ExternalValue))
            {
                return $"Row {i + 1} needs both an internal value and an external value.";
            }

            if (!Enum.IsDefined(row.Direction))
            {
                return $"Row {i + 1} has an invalid translation direction.";
            }
        }

        var outboundInternals = list
            .Where(r => InterfaceValueTranslator.AppliesOutbound(r.Direction))
            .Select(r => r.InternalValue.Trim())
            .ToList();
        if (HasDuplicate(outboundInternals))
        {
            return "Each internal value can appear only once among outbound or both-direction rows.";
        }

        var inboundExternals = list
            .Where(r => InterfaceValueTranslator.AppliesInbound(r.Direction))
            .Select(r => r.ExternalValue.Trim())
            .ToList();
        if (HasDuplicate(inboundExternals))
        {
            return "Each external value can appear only once among inbound or both-direction rows.";
        }

        return null;
    }

    private static bool HasDuplicate(IReadOnlyList<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (!seen.Add(value))
            {
                return true;
            }
        }

        return false;
    }

    private static List<InterfaceValueTranslation> ToEntities(
        string dataItemKey,
        IReadOnlyList<InterfaceValueTranslationDto>? rows) =>
        (rows ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.InternalValue) && !string.IsNullOrWhiteSpace(r.ExternalValue))
            .Select(r => new InterfaceValueTranslation
            {
                DataItemKey = dataItemKey,
                InternalValue = r.InternalValue.Trim(),
                ExternalValue = r.ExternalValue.Trim(),
                Direction = r.Direction
            })
            .ToList();

    private static InterfaceTranslationTableDto Map(string dataItemKey, IReadOnlyList<InterfaceValueTranslation> rows)
    {
        var item = InterfaceDataItemCatalog.AllDistinct()
            .First(i => string.Equals(i.Key, dataItemKey, StringComparison.Ordinal));
        return new InterfaceTranslationTableDto(
            dataItemKey,
            item.DisplayName,
            rows
                .OrderBy(r => r.InternalValue, StringComparer.OrdinalIgnoreCase)
                .Select(r => new InterfaceValueTranslationDto(r.InternalValue, r.ExternalValue, r.Direction))
                .ToList());
    }

    private static long EntityIdFor(string dataItemKey)
    {
        unchecked
        {
            long hash = 17;
            foreach (var c in dataItemKey)
            {
                hash = (hash * 31) + c;
            }

            return hash == 0 ? 1 : hash;
        }
    }

    private async Task<EvaluationResult<InterfaceTranslationTableDto>?> RejectUnauthorizedAsync(
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
            ? EvaluationResult<InterfaceTranslationTableDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
