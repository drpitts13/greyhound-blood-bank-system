using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>The ABO/Rh of one source unit in a pool.</summary>
public sealed record PoolComponent(AboGroup Abo, RhType RhD);

/// <summary>
/// Mixed-ABO pooling, phase 1 (docs/architecture/mixed-abo-pooling.md): every pool records the ABO/Rh of each source
/// unit it was made from, in source order. Phase 1 only records and shows them; nothing reads them for compatibility yet,
/// and pooling across ABO/Rh is still refused.
/// </summary>
public static class PoolComponents
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToJson(IEnumerable<PoolComponent> components) =>
        JsonSerializer.Serialize(components.ToList(), Options);

    /// <summary>The recorded components, or none for a unit that is not a pool or was pooled before they were recorded.</summary>
    public static IReadOnlyList<PoolComponent> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PoolComponent>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>The pool's ABO: the shared group, or Mixed when the sources differ (phase 3).</summary>
    public static AboGroup ResultAbo(IReadOnlyList<PoolComponent> components)
    {
        var groups = components.Select(c => c.Abo).Distinct().ToList();
        return groups.Count == 1 ? groups[0] : AboGroup.Mixed;
    }

    /// <summary>
    /// The pool's Rh(D), read conservatively from what the bag contains: Unknown if any source is unknown, Positive if any
    /// is positive, otherwise Negative (OCD-094 decision 2).
    /// </summary>
    public static RhType ResultRh(IReadOnlyList<PoolComponent> components) =>
        components.Count == 0 || components.Any(c => c.RhD == RhType.Unknown) ? RhType.Unknown
        : components.Any(c => c.RhD == RhType.Positive) ? RhType.Positive
        : RhType.Negative;

    /// <summary>True when the sources differ in ABO or Rh(D).</summary>
    public static bool IsMixed(IReadOnlyList<PoolComponent> components) =>
        components.Select(c => c.Abo).Distinct().Count() > 1 || components.Select(c => c.RhD).Distinct().Count() > 1;

    /// <summary>The components grouped, in the order first seen: "4 units: O Pos ×3, A Pos".</summary>
    public static string Summary(IReadOnlyList<PoolComponent> components, Func<AboGroup, RhType, string> bloodType)
    {
        var groups = components
            .GroupBy(c => c)
            .Select(g => g.Count() == 1 ? bloodType(g.Key.Abo, g.Key.RhD) : $"{bloodType(g.Key.Abo, g.Key.RhD)} ×{g.Count()}");
        return $"{components.Count} unit{(components.Count == 1 ? "" : "s")}: {string.Join(", ", groups)}";
    }
}
