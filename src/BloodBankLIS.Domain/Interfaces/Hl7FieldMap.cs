using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Interfaces;

/// <summary>
/// Resolved data-item-to-HL7-path map for one endpoint. Missing rows fall back to
/// the catalog default so inbound/outbound still work without stored mappings.
/// </summary>
public sealed class Hl7FieldMap
{
    private readonly Dictionary<string, string> _paths;
    private readonly Dictionary<string, string> _defaults;
    private readonly Dictionary<string, string[]> _fallbacks;

    private Hl7FieldMap(
        InterfaceType interfaceType,
        Hl7Direction direction,
        IReadOnlyDictionary<string, string> paths)
    {
        InterfaceType = interfaceType;
        Direction = direction;
        _defaults = InterfaceDataItemCatalog.For(interfaceType, direction)
            .ToDictionary(i => i.Key, i => i.DefaultHl7Path, StringComparer.Ordinal);
        _paths = new Dictionary<string, string>(_defaults, StringComparer.Ordinal);
        foreach (var pair in paths)
        {
            if (!string.IsNullOrWhiteSpace(pair.Value))
            {
                _paths[pair.Key] = pair.Value.Trim();
            }
        }

        _fallbacks = DefaultFallbacks();
    }

    public InterfaceType InterfaceType { get; }

    public Hl7Direction Direction { get; }

    public static Hl7FieldMap Default(InterfaceType type, Hl7Direction direction) =>
        new(type, direction, new Dictionary<string, string>());

    public static Hl7FieldMap From(
        InterfaceType type,
        Hl7Direction direction,
        IEnumerable<InterfaceFieldMapping>? mappings)
    {
        var paths = (mappings ?? [])
            .Where(m => !string.IsNullOrWhiteSpace(m.DataItemKey))
            .GroupBy(m => m.DataItemKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Hl7Path ?? string.Empty, StringComparer.Ordinal);
        return new Hl7FieldMap(type, direction, paths);
    }

    public static Hl7FieldMap From(InterfaceEndpoint? endpoint)
    {
        if (endpoint is null)
        {
            return Default(InterfaceType.Adt, Hl7Direction.Inbound);
        }

        return From(endpoint.InterfaceType, endpoint.Direction, endpoint.FieldMappings);
    }

    public string Path(string dataItemKey)
    {
        if (_paths.TryGetValue(dataItemKey, out var path) && !string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return _defaults.TryGetValue(dataItemKey, out var fallback) ? fallback : string.Empty;
    }

    /// <summary>
    /// Reads the mapped path. When the path is still the catalog default, also tries
    /// the historical fallback locations used by the original hard-coded mappers.
    /// </summary>
    public string Get(Hl7MessageReader message, string dataItemKey)
    {
        ArgumentNullException.ThrowIfNull(message);
        var path = Path(dataItemKey);
        var value = message.Get(path);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (!IsDefaultPath(dataItemKey, path))
        {
            return value;
        }

        if (_fallbacks.TryGetValue(dataItemKey, out var extras))
        {
            foreach (var extra in extras)
            {
                var next = message.Get(extra);
                if (!string.IsNullOrEmpty(next))
                {
                    return next;
                }
            }
        }

        return value;
    }

    private bool IsDefaultPath(string key, string path) =>
        _defaults.TryGetValue(key, out var def)
        && string.Equals(def, path, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string[]> DefaultFallbacks() => new(StringComparer.Ordinal)
    {
        [InterfaceDataItemKeys.PatientMrn] = ["PID-3-1", "PID-2"],
        [InterfaceDataItemKeys.EncounterVisitNumber] = ["PV1-19-1", "PV1-19", "PV1-50"],
        [InterfaceDataItemKeys.EncounterAccountNumber] = ["PID-18-1", "PID-18"],
        [InterfaceDataItemKeys.EncounterAdmitUtc] = ["PV1-44", "PV1-44-1"],
        [InterfaceDataItemKeys.EncounterDischargeUtc] = ["PV1-45", "PV1-45-1"],
        [InterfaceDataItemKeys.EncounterCurrentLocation] = ["PV1-3-1", "PV1-3-2", "PV1-3"],
        [InterfaceDataItemKeys.OrderNumber] = ["ORC-2-1", "OBR-2-1", "ORC-2", "OBR-2"],
        [InterfaceDataItemKeys.OrderLocationCode] = ["ORC-13-1", "ORC-13"]
    };
}

/// <summary>Minimal reader so Domain does not depend on the HL7 parser project.</summary>
public interface Hl7MessageReader
{
    string Get(string path);
}
