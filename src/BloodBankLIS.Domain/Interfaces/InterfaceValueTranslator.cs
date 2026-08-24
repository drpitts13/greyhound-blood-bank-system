using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Interfaces;

/// <summary>
/// In-memory lookup of interface value translations. Missing or empty values pass through.
/// Lookups are case-insensitive; the stored mapped string is returned.
/// </summary>
public sealed class InterfaceValueTranslator
{
    public static InterfaceValueTranslator Empty { get; } = new();

    private readonly Dictionary<string, Dictionary<string, string>> _toInternal;
    private readonly Dictionary<string, Dictionary<string, string>> _toExternal;

    private InterfaceValueTranslator()
    {
        _toInternal = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        _toExternal = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
    }

    public static InterfaceValueTranslator From(IEnumerable<InterfaceValueTranslation> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var translator = new InterfaceValueTranslator();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.DataItemKey)
                || string.IsNullOrWhiteSpace(row.InternalValue)
                || string.IsNullOrWhiteSpace(row.ExternalValue))
            {
                continue;
            }

            var key = row.DataItemKey.Trim();
            var internalValue = row.InternalValue.Trim();
            var externalValue = row.ExternalValue.Trim();

            if (AppliesInbound(row.Direction))
            {
                Set(translator._toInternal, key, externalValue, internalValue);
            }

            if (AppliesOutbound(row.Direction))
            {
                Set(translator._toExternal, key, internalValue, externalValue);
            }
        }

        return translator;
    }

    /// <summary>Inbound: external HIS value → internal LIS value.</summary>
    public string ToInternal(string dataItemKey, string? externalValue)
    {
        if (string.IsNullOrEmpty(externalValue) || string.IsNullOrEmpty(dataItemKey))
        {
            return externalValue ?? string.Empty;
        }

        if (_toInternal.TryGetValue(dataItemKey, out var byExternal)
            && byExternal.TryGetValue(externalValue, out var mapped))
        {
            return mapped;
        }

        return externalValue;
    }

    /// <summary>Outbound: internal LIS value → external HIS value.</summary>
    public string? ToExternal(string dataItemKey, string? internalValue)
    {
        if (string.IsNullOrEmpty(internalValue) || string.IsNullOrEmpty(dataItemKey))
        {
            return internalValue;
        }

        if (_toExternal.TryGetValue(dataItemKey, out var byInternal)
            && byInternal.TryGetValue(internalValue, out var mapped))
        {
            return mapped;
        }

        return internalValue;
    }

    public static bool AppliesInbound(InterfaceTranslationDirection direction) =>
        direction is InterfaceTranslationDirection.Inbound or InterfaceTranslationDirection.Both;

    public static bool AppliesOutbound(InterfaceTranslationDirection direction) =>
        direction is InterfaceTranslationDirection.Outbound or InterfaceTranslationDirection.Both;

    private static void Set(
        Dictionary<string, Dictionary<string, string>> target,
        string dataItemKey,
        string from,
        string to)
    {
        if (!target.TryGetValue(dataItemKey, out var inner))
        {
            inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            target[dataItemKey] = inner;
        }

        inner[from] = to;
    }
}
