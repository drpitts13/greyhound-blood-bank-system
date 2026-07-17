using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.ValueObjects;

public sealed record InterpretationLogicRow(
    string InterpretationKey,
    string Label,
    IReadOnlyDictionary<string, ReactionPolarity> SubtestExpectations);

public static class InterpretationLogicDefinitions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string? ToJson(IReadOnlyList<InterpretationLogicRow>? items)
    {
        if (items is null || items.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(items, JsonOptions);
    }

    public static IReadOnlyList<InterpretationLogicRow> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<InterpretationLogicRow>();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<InterpretationLogicRowDto>>(json, JsonOptions);
            if (items is null || items.Count == 0)
            {
                return Array.Empty<InterpretationLogicRow>();
            }

            return items.Select(i => new InterpretationLogicRow(
                i.InterpretationKey ?? string.Empty,
                i.Label ?? string.Empty,
                i.SubtestExpectations ?? new Dictionary<string, ReactionPolarity>(StringComparer.OrdinalIgnoreCase)))
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<InterpretationLogicRow>();
        }
    }

    public static string BuildAboRhKey(AboGroup abo, RhType rh) => $"{abo}|{rh}";

    /// <summary>Standard ABO/Rh logic rows using the Type O Positive reaction pattern as template.</summary>
    public static IReadOnlyList<InterpretationLogicRow> DefaultAboRhLogic()
    {
        var rows = new List<InterpretationLogicRow>();
        foreach (var abo in Enum.GetValues<AboGroup>().Where(a => a != AboGroup.Unknown))
        {
            foreach (var rh in Enum.GetValues<RhType>().Where(r => r != RhType.Unknown))
            {
                var expectations = BuildAboRhExpectations(abo, rh);
                rows.Add(new InterpretationLogicRow(
                    BuildAboRhKey(abo, rh),
                    $"Type {abo} {rh}",
                    expectations));
            }
        }

        return rows;
    }

    private static Dictionary<string, ReactionPolarity> BuildAboRhExpectations(AboGroup abo, RhType rh)
    {
        var antiA = abo is AboGroup.B or AboGroup.AB ? ReactionPolarity.Positive : ReactionPolarity.Negative;
        var antiB = abo is AboGroup.A or AboGroup.AB ? ReactionPolarity.Positive : ReactionPolarity.Negative;
        var aCells = abo is AboGroup.A or AboGroup.AB ? ReactionPolarity.Negative : ReactionPolarity.Positive;
        var bCells = abo is AboGroup.B or AboGroup.AB ? ReactionPolarity.Negative : ReactionPolarity.Positive;
        var antiD = rh == RhType.Positive ? ReactionPolarity.Positive : ReactionPolarity.Negative;

        return new Dictionary<string, ReactionPolarity>(StringComparer.OrdinalIgnoreCase)
        {
            [AboRhPanelSubtestCodes.AntiA] = antiA,
            [AboRhPanelSubtestCodes.AntiB] = antiB,
            [AboRhPanelSubtestCodes.AntiD] = antiD,
            [AboRhPanelSubtestCodes.ACells] = aCells,
            [AboRhPanelSubtestCodes.BCells] = bCells
        };
    }

    private sealed class InterpretationLogicRowDto
    {
        public string? InterpretationKey { get; set; }
        public string? Label { get; set; }
        public Dictionary<string, ReactionPolarity>? SubtestExpectations { get; set; }
    }
}
