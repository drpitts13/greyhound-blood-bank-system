using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Admin management of the HL7 value-translation table (internal ↔ external codes per data item), for all interfaces
/// or as one interface's overrides. Saving a data item replaces only that item's rows in that scope. Local values are
/// checked against the field's value list (tests and groupers, visit types, race…).
/// </summary>
public sealed class InterfaceTranslationAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(InterfaceValueTranslation);

    private readonly IInterfaceValueTranslationRepository _translations;
    private readonly IPermissionEvaluator? _permissionEvaluator;
    private readonly InterfaceValueSets? _valueSets;
    private readonly InterfaceUnmappedValueService? _unmapped;
    private readonly IRepository<InterfaceEndpoint>? _endpoints;

    public InterfaceTranslationAdminService(
        IInterfaceValueTranslationRepository translations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null,
        InterfaceValueSets? valueSets = null,
        InterfaceUnmappedValueService? unmapped = null,
        IRepository<InterfaceEndpoint>? endpoints = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _translations = translations;
        _permissionEvaluator = permissionEvaluator;
        _valueSets = valueSets;
        _unmapped = unmapped;
        _endpoints = endpoints;
    }

    public static IReadOnlyList<InterfaceDataItemDto> AllDataItems() =>
        InterfaceDataItemCatalog.Translatable()
            .Select(i => new InterfaceDataItemDto(i.Key, i.DisplayName, i.Description, i.DefaultHl7Path, i.Required))
            .ToList();

    /// <summary>Every translatable field with its group and how many rows it has in any scope.</summary>
    public async Task<IReadOnlyList<InterfaceTranslationFieldDto>> ListFieldsAsync(CancellationToken ct = default)
    {
        var counts = (await _translations.ListAsync(ct))
            .GroupBy(t => t.DataItemKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return InterfaceDataItemCatalog.Translatable()
            .Select(i => new InterfaceTranslationFieldDto(
                i.Key, i.DisplayName, i.Description, InterfaceDataItemCatalog.CategoryOf(i.Key),
                counts.GetValueOrDefault(i.Key), InterfaceValueSets.HasValueList(i.Key)))
            .OrderBy(f => f.Category).ThenBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<EvaluationResult<InterfaceTranslationTableDto>> GetAsync(
        string dataItemKey,
        CancellationToken ct = default,
        long? endpointId = null)
    {
        var catalogError = ValidateCatalogKey(dataItemKey);
        if (catalogError is not null)
        {
            return EvaluationResult<InterfaceTranslationTableDto>.Fail(catalogError);
        }

        var key = dataItemKey.Trim();
        var rows = await _translations.ListAsync(t => t.DataItemKey == key, ct);
        return EvaluationResult<InterfaceTranslationTableDto>.Ok(await MapAsync(key, endpointId, rows, ct));
    }

    /// <summary>
    /// Tries a value through the saved translations as an interface would (its overrides, the procedure-code fallback,
    /// a grouper before a test), without changing anything.
    /// </summary>
    public async Task<InterfaceTranslationCheckDto> CheckAsync(
        string dataItemKey, string? value, bool inbound, long? endpointId, CancellationToken ct = default)
    {
        var key = (dataItemKey ?? string.Empty).Trim();
        var groupers = _valueSets is null ? new HashSet<string>() : await _valueSets.GrouperCodesAsync(ct);
        var translator = InterfaceValueTranslator.From(await _translations.ListAsync(ct), endpointId, groupers.Contains);
        var outcome = translator.Explain(key, value, inbound);
        string? note = null;
        if (inbound && InterfaceValueSets.IsProcedureKey(key) && _valueSets is not null)
        {
            var local = await _valueSets.ForAsync(key, ct);
            var match = local.FirstOrDefault(l => string.Equals(l.Value, outcome.Value, StringComparison.OrdinalIgnoreCase));
            note = match is null
                ? $"{outcome.Value} is not an active test or grouper: an order with this code would go to the interface error queue."
                : match.Kind == "Grouper"
                    ? $"Grouper {match.Value} ({match.Label}): the order gets its tests."
                    : $"Test {match.Value} ({match.Label}).";
        }

        return new InterfaceTranslationCheckDto(value ?? string.Empty, outcome.Value, outcome.Translated, outcome.FromKey, outcome.FromInterfaceOverride, note);
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

        if (string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<InterfaceTranslationTableDto>.Fail("Say why the translations are changing.");
        }

        if (req.EndpointId is long endpointId && _endpoints is not null && await _endpoints.GetByIdAsync(endpointId, ct) is null)
        {
            return EvaluationResult<InterfaceTranslationTableDto>.Fail("That interface was not found.");
        }

        var key = dataItemKey.Trim();
        var localValues = _valueSets is null ? [] : await _valueSets.ForAsync(key, ct);
        var validation = ValidateRows(req.Rows, localValues, InterfaceValueSets.IsProcedureKey(key));
        if (validation is not null)
        {
            return EvaluationResult<InterfaceTranslationTableDto>.Fail(validation);
        }

        var all = await _translations.ListAsync(t => t.DataItemKey == key, ct);
        var before = Rows(all.Where(t => t.EndpointId == req.EndpointId));
        var entities = ToEntities(key, req.Rows, localValues);
        await _translations.ReplaceForDataItemAsync(key, entities, ct, req.EndpointId);
        if (_unmapped is not null)
        {
            await _unmapped.ResolveCoveredAsync(key, req.EndpointId, entities, ct);
        }

        RecordChange(
            EntityType,
            EntityIdFor(req.EndpointId is long scope ? $"{key}@{scope}" : key),
            1,
            ConfigChangeAction.Update,
            AuditEventType.Interface,
            new { Scope = req.EndpointId, Rows = before },
            new { Scope = req.EndpointId, Rows = Rows(entities) },
            req.ChangeReason.Trim());

        await UnitOfWork.SaveChangesAsync(ct);
        var saved = await _translations.ListAsync(t => t.DataItemKey == key, ct);
        return EvaluationResult<InterfaceTranslationTableDto>.Ok(await MapAsync(key, req.EndpointId, saved, ct));
    }

    private static string? ValidateCatalogKey(string? dataItemKey)
    {
        if (string.IsNullOrWhiteSpace(dataItemKey))
        {
            return "A data item is required.";
        }

        return InterfaceDataItemCatalog.IsTranslatable(dataItemKey.Trim())
            ? null
            : $"Unknown data item '{dataItemKey.Trim()}'.";
    }

    private static string? ValidateRows(
        IReadOnlyList<InterfaceValueTranslationDto>? rows,
        IReadOnlyList<InterfaceLocalValueDto> localValues,
        bool procedureCodes)
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

            if (localValues.Count > 0
                && !localValues.Any(l => string.Equals(l.Value, row.InternalValue.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return $"{InterfaceTranslationAuthorizationRule.LocalValueCode}: row {i + 1}: '{row.InternalValue.Trim()}' is not "
                    + (procedureCodes ? "an active test or test grouper." : "one of this field's local values.");
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

        var inbound = list.Where(r => InterfaceValueTranslator.AppliesInbound(r.Direction)).ToList();
        if (procedureCodes)
        {
            // One external code may name a grouper and a test (the grouper wins), but not two of either.
            var kinds = localValues.ToDictionary(l => l.Value, l => l.Kind, StringComparer.OrdinalIgnoreCase);
            var clash = inbound
                .GroupBy(r => r.ExternalValue.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.GroupBy(r => kinds.GetValueOrDefault(r.InternalValue.Trim()) ?? "Test").Any(k => k.Count() > 1));
            if (clash is not null)
            {
                return $"External code '{clash.Key}' maps inbound to more than one test or more than one grouper. It may name one grouper and one test; the grouper wins.";
            }
        }
        else if (HasDuplicate(inbound.Select(r => r.ExternalValue.Trim()).ToList()))
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

    /// <summary>Rows to save; a local value from a value list is stored as the list spells it.</summary>
    private static List<InterfaceValueTranslation> ToEntities(
        string dataItemKey,
        IReadOnlyList<InterfaceValueTranslationDto>? rows,
        IReadOnlyList<InterfaceLocalValueDto> localValues) =>
        (rows ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.InternalValue) && !string.IsNullOrWhiteSpace(r.ExternalValue))
            .Select(r => new InterfaceValueTranslation
            {
                DataItemKey = dataItemKey,
                InternalValue = localValues.FirstOrDefault(l => string.Equals(l.Value, r.InternalValue.Trim(), StringComparison.OrdinalIgnoreCase))?.Value
                    ?? r.InternalValue.Trim(),
                ExternalValue = r.ExternalValue.Trim(),
                Direction = r.Direction
            })
            .ToList();

    private static IReadOnlyList<InterfaceValueTranslationDto> Rows(IEnumerable<InterfaceValueTranslation> rows) =>
        rows
            .OrderBy(r => r.InternalValue, StringComparer.OrdinalIgnoreCase)
            .Select(r => new InterfaceValueTranslationDto(r.InternalValue, r.ExternalValue, r.Direction))
            .ToList();

    private async Task<InterfaceTranslationTableDto> MapAsync(
        string dataItemKey, long? endpointId, IReadOnlyList<InterfaceValueTranslation> all, CancellationToken ct)
    {
        var item = InterfaceDataItemCatalog.Translatable()
            .First(i => string.Equals(i.Key, dataItemKey, StringComparison.Ordinal));
        return new InterfaceTranslationTableDto(
            dataItemKey,
            item.DisplayName,
            Rows(all.Where(r => r.EndpointId == endpointId)),
            endpointId,
            _valueSets is null ? [] : await _valueSets.ForAsync(dataItemKey, ct),
            endpointId is null ? null : Rows(all.Where(r => r.EndpointId is null)));
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
