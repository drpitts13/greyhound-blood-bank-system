using System.Text.Json;
using System.Text.Json.Serialization;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// One row in a multi-subtest result panel (e.g. ABORH typing). Configured on
/// <see cref="Entities.Configuration.TestDefinition"/> and rendered at result entry.
/// </summary>
public sealed record PanelSubtestDefinition(
    string Code,
    string Label,
    bool Required,
    int SortOrder = 0);

/// <summary>Serializes and parses panel subtest configuration JSON on test definitions.</summary>
public static class PanelSubtestDefinitions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IReadOnlyList<PanelSubtestDefinition> DefaultAboRh() =>
    [
        new(AboRhPanelSubtestCodes.AntiA, "Anti-A", true, 1),
        new(AboRhPanelSubtestCodes.AntiB, "Anti-B", true, 2),
        new(AboRhPanelSubtestCodes.AntiD, "Anti-D", true, 3),
        new(AboRhPanelSubtestCodes.ACells, "A cells", true, 4),
        new(AboRhPanelSubtestCodes.BCells, "B cells", true, 5),
        new(AboRhPanelSubtestCodes.Control, "Control", false, 6),
        new(AboRhPanelSubtestCodes.WeakD, "Weak-D", false, 7)
    ];

    public static string? ToJson(IReadOnlyList<PanelSubtestDefinition>? items)
    {
        if (items is null || items.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(items.OrderBy(i => i.SortOrder).ThenBy(i => i.Code), JsonOptions);
    }

    public static IReadOnlyList<PanelSubtestDefinition> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<PanelSubtestDefinition>();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<PanelSubtestDefinition>>(json, JsonOptions);
            return items is null || items.Count == 0
                ? Array.Empty<PanelSubtestDefinition>()
                : items.OrderBy(i => i.SortOrder).ThenBy(i => i.Code).ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<PanelSubtestDefinition>();
        }
    }

    /// <summary>Subtests for entry/validation: configured list or ABORH defaults when type is panel but unset.</summary>
    public static IReadOnlyList<PanelSubtestDefinition> ResolveForEntry(
        string? panelSubtestsJson,
        bool useAboRhDefaultsWhenEmpty)
    {
        var parsed = Parse(panelSubtestsJson);
        if (parsed.Count > 0)
        {
            return parsed;
        }

        return useAboRhDefaultsWhenEmpty ? DefaultAboRh() : Array.Empty<PanelSubtestDefinition>();
    }
}
