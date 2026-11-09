using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Tests;

public class ReactionInterpretationSuggestionTests
{
    private static readonly IReadOnlyList<SubtestChoiceDto> Grades = SubtestChoiceDefinitions.DefaultGradedReaction()
        .Select(c => new SubtestChoiceDto(c.Code, c.Label, c.Polarity))
        .ToList();

    [Fact]
    public void SuggestAboRh_FromPanelReactions_ReturnsTheType()
    {
        var values = Values(
            (AboRhPanelSubtestCodes.AntiA, "4+"),
            (AboRhPanelSubtestCodes.AntiB, "0"),
            (AboRhPanelSubtestCodes.AntiD, "3+"),
            (AboRhPanelSubtestCodes.ACells, "0"),
            (AboRhPanelSubtestCodes.BCells, "4+"));

        var type = ReactionInterpretationSuggestion.SuggestAboRh(AboRhOptions(), AboRhPanel(), values);

        Assert.Equal(new AboRh(AboGroup.A, RhType.Positive), type);
    }

    [Fact]
    public void SuggestAboRh_Discrepant_ReturnsNull()
    {
        var values = Values(
            (AboRhPanelSubtestCodes.AntiA, "4+"),
            (AboRhPanelSubtestCodes.AntiB, "0"),
            (AboRhPanelSubtestCodes.AntiD, "3+"),
            (AboRhPanelSubtestCodes.ACells, "4+"),
            (AboRhPanelSubtestCodes.BCells, "4+"));

        Assert.Null(ReactionInterpretationSuggestion.SuggestAboRh(AboRhOptions(), AboRhPanel(), values));
    }

    [Fact]
    public void SuggestKey_AntibodyScreen_IgnoresCheckCellsAndOptionalPhases()
    {
        var options = InterpretationLogicDefinitions.DefaultAntibodyScreenLogic(["Cell1", "Cell2"], ["37C", "AHG"])
            .Select(r => new InterpretationOptionDto(r.InterpretationKey, r.Label, r.SubtestExpectations, r.MatchMode))
            .ToList();
        var panel = new[] { "Cell1", "Cell2" }
            .Select((cell, i) => new ResolvedPanelSubtestDto(cell, cell, SubtestResultType.GradedReaction, Grades, true, i,
            [
                Phase("37C", required: false, sort: 1),
                Phase("AHG", required: true, sort: 2),
                Phase("CC", required: true, sort: 3, checkCell: true)
            ]))
            .ToList();
        var values = Values(
            (PhaseResultKeys.Compose("Cell1", "AHG"), "0"),
            (PhaseResultKeys.Compose("Cell2", "AHG"), "0"),
            // Check cells react positive on a valid negative AHG; they must not make the screen positive.
            (PhaseResultKeys.Compose("Cell1", "CC"), "2+"),
            (PhaseResultKeys.Compose("Cell2", "CC"), "2+"));

        Assert.Equal("Negative", ReactionInterpretationSuggestion.SuggestKey(options, panel, values));

        var positive = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
        {
            [PhaseResultKeys.Compose("Cell2", "37C")] = "1+"
        };
        Assert.Equal("Positive", ReactionInterpretationSuggestion.SuggestKey(options, panel, positive));
    }

    [Fact]
    public void SuggestCrossmatch_FollowsTheReactions()
    {
        var panel = new List<ResolvedPanelSubtestDto>
        {
            new("XM-IS", "Immediate spin", SubtestResultType.GradedReaction, Grades, true, 1),
            new("XM-AHG", "AHG", SubtestResultType.GradedReaction, Grades, false, 2)
        };

        Assert.Null(ReactionInterpretationSuggestion.SuggestCrossmatch(panel, Values()));
        Assert.Equal(CrossmatchResult.Compatible,
            ReactionInterpretationSuggestion.SuggestCrossmatch(panel, Values(("XM-IS", "0"))));
        Assert.Equal(CrossmatchResult.Incompatible,
            ReactionInterpretationSuggestion.SuggestCrossmatch(panel, Values(("XM-IS", "0"), ("XM-AHG", "1+"))));
        Assert.Equal(CrossmatchResult.Compatible,
            ReactionInterpretationSuggestion.SuggestCrossmatch(panel, Values(("XM-IS", "0"), ("XM-AHG", "1+")), new HashSet<string>(["XM-AHG"])));
    }

    // TEST-BB-682: a required reading not yet entered is named, by subtest and phase, so the save can refuse it; an
    // optional reading, and a phase left optional, are never asked for.
    [Fact]
    public void MissingReadings_NamesRequiredReadingsNotEntered()
    {
        var panel = new List<ResolvedPanelSubtestDto>
        {
            new("ANTI-A", "Anti-A", SubtestResultType.GradedReaction, Grades, true, 1),
            new("ANTI-D", "Anti-D", SubtestResultType.GradedReaction, Grades, true, 2),
            new("CTL", "Control", SubtestResultType.GradedReaction, Grades, false, 3),
            new("NOTE", "Note", SubtestResultType.FreeText, [], true, 4),
            new("SC1", "Screen cell I", SubtestResultType.GradedReaction, Grades, true, 5,
                [Phase("37C", required: false, sort: 1), Phase("AHG", required: true, sort: 2)])
        };

        Assert.Equal(["Anti-A", "Anti-D", "Note", "Screen cell I AHG"],
            ReactionInterpretationSuggestion.MissingReadings(panel, Values(("CTL", "NT"))));
        Assert.Equal(["Anti-D", "Screen cell I AHG"],
            ReactionInterpretationSuggestion.MissingReadings(panel, Values(("ANTI-A", "0"), ("ANTI-D", "  "), ("NOTE", "ok"))));
        Assert.Empty(ReactionInterpretationSuggestion.MissingReadings(panel, Values(
            ("ANTI-A", "0"), ("ANTI-D", "3+"), ("NOTE", "ok"), (PhaseResultKeys.Compose("SC1", "AHG"), "0"))));

        Assert.Equal("Enter a reading for Anti-A, Anti-D before saving.",
            ReactionInterpretationSuggestion.MissingReadingsMessage(["Anti-A", "Anti-D"]));
        Assert.Null(ReactionInterpretationSuggestion.MissingReadingsMessage([]));
    }

    // TEST-BB-683: a panel with no readings entered suggests no type, so nothing is filled in from reactions nobody read.
    [Fact]
    public void SuggestAboRh_NoReadingsEntered_ReturnsNull() =>
        Assert.Null(ReactionInterpretationSuggestion.SuggestAboRh(AboRhOptions(), AboRhPanel(), Values()));

    private static IReadOnlyList<InterpretationOptionDto> AboRhOptions() =>
        InterpretationLogicDefinitions.DefaultAboRhLogic()
            .Select(r => new InterpretationOptionDto(r.InterpretationKey, r.Label, r.SubtestExpectations, r.MatchMode))
            .ToList();

    private static IReadOnlyList<ResolvedPanelSubtestDto> AboRhPanel() =>
        new[]
        {
            AboRhPanelSubtestCodes.AntiA, AboRhPanelSubtestCodes.AntiB, AboRhPanelSubtestCodes.AntiD,
            AboRhPanelSubtestCodes.ACells, AboRhPanelSubtestCodes.BCells
        }
        .Select((code, i) => new ResolvedPanelSubtestDto(code, code, SubtestResultType.GradedReaction, Grades, true, i))
        .ToList();

    private static ResolvedPanelPhaseDto Phase(string code, bool required, int sort, bool checkCell = false) =>
        new(code, code, required, IncludeInInterpretation: !checkCell, checkCell, checkCell ? "AHG" : null, sort);

    private static Dictionary<string, string> Values(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
}
