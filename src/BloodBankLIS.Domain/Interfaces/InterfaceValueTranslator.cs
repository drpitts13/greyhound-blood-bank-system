using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Interfaces;

/// <summary>A value with no translation, seen while building or reading one message.</summary>
public sealed record TranslationMiss(string DataItemKey, string Value, InterfaceTranslationDirection Direction);

/// <summary>Where a translated value came from, for the admin "test a value" check.</summary>
public sealed record TranslationOutcome(string Value, bool Translated, string? FromKey, bool FromInterfaceOverride);

/// <summary>
/// In-memory lookup of interface value translations for one message. Missing or empty values pass through. Lookups are
/// case-insensitive; the stored mapped string is returned.
/// <list type="bullet">
/// <item>Rows for the message's interface override global rows for the same field, direction, and value.</item>
/// <item>Order and result test codes (OBR-4) fall back to the shared <see cref="InterfaceDataItemKeys.ProcedureCode"/>.</item>
/// <item>Inbound, when one external code maps to both a test grouper and a test, the grouper wins.</item>
/// <item>A value with no translation, for a field that has translations in that direction, is recorded in
/// <see cref="Misses"/> so it can be listed for the interface team.</item>
/// </list>
/// </summary>
public sealed class InterfaceValueTranslator
{
    public static InterfaceValueTranslator Empty => new();

    private readonly Dictionary<string, Dictionary<string, Entry>> _toInternal;
    private readonly Dictionary<string, Dictionary<string, Entry>> _toExternal;
    private readonly List<TranslationMiss> _misses = [];

    private readonly record struct Entry(string Value, bool FromOverride);

    private InterfaceValueTranslator()
    {
        _toInternal = new Dictionary<string, Dictionary<string, Entry>>(StringComparer.OrdinalIgnoreCase);
        _toExternal = new Dictionary<string, Dictionary<string, Entry>>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Values this message needed that had no translation, each once.</summary>
    public IReadOnlyList<TranslationMiss> Misses => _misses;

    /// <param name="rows">All translation rows (global and per interface).</param>
    /// <param name="endpointId">The message's interface; its rows override global rows. Rows for other interfaces are ignored.</param>
    /// <param name="isGrouper">Whether a local code is a test grouper, so a grouper wins over a test inbound.</param>
    public static InterfaceValueTranslator From(
        IEnumerable<InterfaceValueTranslation> rows,
        long? endpointId = null,
        Func<string, bool>? isGrouper = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var translator = new InterfaceValueTranslator();
        var usable = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.DataItemKey)
                && !string.IsNullOrWhiteSpace(r.InternalValue)
                && !string.IsNullOrWhiteSpace(r.ExternalValue))
            .ToList();

        // Global rows first, then this interface's rows over them.
        foreach (var pass in new[] { usable.Where(r => r.EndpointId is null), usable.Where(r => endpointId is not null && r.EndpointId == endpointId) })
        {
            var fromOverride = false;
            var passInbound = new Dictionary<(string Key, string External), string>(PairComparer.Instance);
            foreach (var row in pass)
            {
                fromOverride = row.EndpointId is not null;
                var key = row.DataItemKey.Trim();
                var internalValue = row.InternalValue.Trim();
                var externalValue = row.ExternalValue.Trim();

                if (AppliesInbound(row.Direction))
                {
                    // Within one scope a grouper beats a test for the same external code.
                    if (passInbound.TryGetValue((key, externalValue), out var already)
                        && isGrouper is not null && isGrouper(already) && !isGrouper(internalValue))
                    {
                        continue;
                    }

                    passInbound[(key, externalValue)] = internalValue;
                    Set(translator._toInternal, key, externalValue, new Entry(internalValue, fromOverride));
                }

                if (AppliesOutbound(row.Direction))
                {
                    Set(translator._toExternal, key, internalValue, new Entry(externalValue, fromOverride));
                }
            }
        }

        return translator;
    }

    /// <summary>Inbound: external HIS value → internal LIS value.</summary>
    public string ToInternal(string dataItemKey, string? externalValue) =>
        Explain(dataItemKey, externalValue, inbound: true).Value;

    /// <summary>Outbound: internal LIS value → external HIS value.</summary>
    public string? ToExternal(string dataItemKey, string? internalValue) =>
        string.IsNullOrEmpty(internalValue) ? internalValue : Explain(dataItemKey, internalValue, inbound: false).Value;

    /// <summary>Whether an outbound translation exists for this exact value (without falling back to the value itself).</summary>
    public bool TryToExternal(string dataItemKey, string? internalValue, out string external)
    {
        external = internalValue ?? string.Empty;
        if (string.IsNullOrEmpty(internalValue) || string.IsNullOrEmpty(dataItemKey))
        {
            return false;
        }

        if (Lookup(_toExternal, dataItemKey, internalValue.Trim()) is { } found)
        {
            external = found.Entry.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// The translation of one value in one direction, with where it came from. Records a miss when the field has
    /// translations in that direction but none for this value.
    /// </summary>
    public TranslationOutcome Explain(string dataItemKey, string? value, bool inbound)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(dataItemKey))
        {
            return new TranslationOutcome(value ?? string.Empty, false, null, false);
        }

        var table = inbound ? _toInternal : _toExternal;
        var lookup = value.Trim();
        if (Lookup(table, dataItemKey, lookup) is { } found)
        {
            return new TranslationOutcome(found.Entry.Value, true, found.Key, found.Entry.FromOverride);
        }

        if (HasRows(table, dataItemKey))
        {
            RecordMiss(dataItemKey, lookup, inbound ? InterfaceTranslationDirection.Inbound : InterfaceTranslationDirection.Outbound);
        }

        return new TranslationOutcome(value, false, null, false);
    }

    /// <summary>Records a value that could not be used (for example an order code that names no test or grouper).</summary>
    public void RecordMiss(string dataItemKey, string value, InterfaceTranslationDirection direction)
    {
        if (string.IsNullOrWhiteSpace(dataItemKey) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var miss = new TranslationMiss(dataItemKey, value.Trim(), direction);
        if (!_misses.Any(m => string.Equals(m.DataItemKey, miss.DataItemKey, StringComparison.Ordinal)
                && string.Equals(m.Value, miss.Value, StringComparison.OrdinalIgnoreCase)
                && m.Direction == miss.Direction))
        {
            _misses.Add(miss);
        }
    }

    public static bool AppliesInbound(InterfaceTranslationDirection direction) =>
        direction is InterfaceTranslationDirection.Inbound or InterfaceTranslationDirection.Both;

    public static bool AppliesOutbound(InterfaceTranslationDirection direction) =>
        direction is InterfaceTranslationDirection.Outbound or InterfaceTranslationDirection.Both;

    /// <summary>Test-code fields that fall back to the shared procedure code.</summary>
    public static bool UsesProcedureCode(string dataItemKey) =>
        dataItemKey is InterfaceDataItemKeys.OrderTestCode or InterfaceDataItemKeys.ResultObrTestCode;

    private static (string Key, Entry Entry)? Lookup(Dictionary<string, Dictionary<string, Entry>> table, string dataItemKey, string value)
    {
        if (table.TryGetValue(dataItemKey, out var byValue) && byValue.TryGetValue(value, out var entry))
        {
            return (dataItemKey, entry);
        }

        if (UsesProcedureCode(dataItemKey)
            && table.TryGetValue(InterfaceDataItemKeys.ProcedureCode, out var shared)
            && shared.TryGetValue(value, out var sharedEntry))
        {
            return (InterfaceDataItemKeys.ProcedureCode, sharedEntry);
        }

        return null;
    }

    private static bool HasRows(Dictionary<string, Dictionary<string, Entry>> table, string dataItemKey) =>
        table.ContainsKey(dataItemKey) || (UsesProcedureCode(dataItemKey) && table.ContainsKey(InterfaceDataItemKeys.ProcedureCode));

    private static void Set(Dictionary<string, Dictionary<string, Entry>> target, string dataItemKey, string from, Entry to)
    {
        if (!target.TryGetValue(dataItemKey, out var inner))
        {
            inner = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            target[dataItemKey] = inner;
        }

        inner[from] = to;
    }

    private sealed class PairComparer : IEqualityComparer<(string Key, string External)>
    {
        public static readonly PairComparer Instance = new();

        public bool Equals((string Key, string External) x, (string Key, string External) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Key, y.Key) && StringComparer.OrdinalIgnoreCase.Equals(x.External, y.External);

        public int GetHashCode((string Key, string External) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.External));
    }
}
