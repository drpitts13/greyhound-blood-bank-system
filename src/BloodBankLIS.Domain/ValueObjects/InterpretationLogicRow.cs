using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.ValueObjects;

public sealed record InterpretationLogicRow(
    string InterpretationKey,
    string Label,
    IReadOnlyDictionary<string, ReactionPolarity> SubtestExpectations,
    InterpretationMatchMode MatchMode = InterpretationMatchMode.AllMatch);

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
                i.SubtestExpectations ?? new Dictionary<string, ReactionPolarity>(StringComparer.OrdinalIgnoreCase),
                i.MatchMode ?? InterpretationMatchMode.AllMatch))
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<InterpretationLogicRow>();
        }
    }

    /// <summary>
    /// Drops composite expectation keys whose phase is no longer assigned to that
    /// subtest (e.g. leftover IS/37C after an antibody screen is limited to AHG).
    /// Bare subtest keys and keys for unassigned subtests are left for validation.
    /// </summary>
    public static IReadOnlyList<InterpretationLogicRow> DropUnassignedPhaseExpectations(
        IReadOnlyList<InterpretationLogicRow> rows,
        IReadOnlyList<PanelSubtestAssignment> assignments)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        var phasesBySubtest = assignments
            .Where(a => !string.IsNullOrWhiteSpace(a.SubtestCode))
            .GroupBy(a => a.SubtestCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (g.First().PhaseCodes ?? Array.Empty<string>())
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        return rows.Select(row =>
        {
            var kept = new Dictionary<string, ReactionPolarity>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, polarity) in row.SubtestExpectations)
            {
                if (PhaseResultKeys.TrySplit(key, out var subtest, out var phase)
                    && phasesBySubtest.TryGetValue(subtest, out var phases)
                    && !phases.Contains(phase))
                {
                    continue;
                }

                kept[key] = polarity;
            }

            return row with { SubtestExpectations = kept };
        }).ToList();
    }

    public static string BuildAboRhKey(AboGroup abo, RhType rh) => $"{abo}|{rh}";

    /// <summary>Standard ABO/Rh logic rows using the Type O Positive reaction pattern as template.</summary>
    public static IReadOnlyList<InterpretationLogicRow> DefaultAntibodyScreenLogic(
        IReadOnlyList<string> cellCodes,
        IReadOnlyList<string> interpretivePhaseCodes)
    {
        var negative = new Dictionary<string, ReactionPolarity>(StringComparer.OrdinalIgnoreCase);
        var positive = new Dictionary<string, ReactionPolarity>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in cellCodes)
        {
            foreach (var phase in interpretivePhaseCodes)
            {
                var key = PhaseResultKeys.Compose(cell, phase);
                negative[key] = ReactionPolarity.Negative;
                positive[key] = ReactionPolarity.Positive;
            }
        }

        return
        [
            new InterpretationLogicRow("Negative", "Negative", negative, InterpretationMatchMode.AllMatch),
            new InterpretationLogicRow("Positive", "Positive", positive, InterpretationMatchMode.AnyPositive)
        ];
    }

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

    /// <summary>Front-type only expectations (Anti-A, Anti-B, Anti-D) for product retype.</summary>
    public static IReadOnlyList<InterpretationLogicRow> DefaultAboRhRetypeLogic()
    {
        var rows = new List<InterpretationLogicRow>();
        foreach (var abo in Enum.GetValues<AboGroup>().Where(a => a != AboGroup.Unknown))
        {
            foreach (var rh in Enum.GetValues<RhType>().Where(r => r != RhType.Unknown))
            {
                var all = BuildAboRhExpectations(abo, rh);
                var front = new Dictionary<string, ReactionPolarity>(StringComparer.OrdinalIgnoreCase)
                {
                    [AboRhPanelSubtestCodes.AntiA] = all[AboRhPanelSubtestCodes.AntiA],
                    [AboRhPanelSubtestCodes.AntiB] = all[AboRhPanelSubtestCodes.AntiB],
                    [AboRhPanelSubtestCodes.AntiD] = all[AboRhPanelSubtestCodes.AntiD]
                };
                rows.Add(new InterpretationLogicRow(
                    BuildAboRhKey(abo, rh),
                    $"Type {abo} {rh}",
                    front));
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
        public InterpretationMatchMode? MatchMode { get; set; }
    }
}
