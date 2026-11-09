using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class CrossmatchReactionRuleTests
{
    private static readonly Dictionary<string, SubtestDefinition> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IS"] = new SubtestDefinition { Code = "IS", Name = "Immediate spin" },
        ["AHG"] = new SubtestDefinition { Code = "AHG", Name = "AHG" },
        ["CC"] = new SubtestDefinition { Code = "CC", Name = "Check cells" },
        ["Cell1"] = new SubtestDefinition { Code = "Cell1", Name = "Donor cell" },
        ["NOTE"] = new SubtestDefinition { Code = "NOTE", Name = "Note", ResultType = SubtestResultType.FreeText }
    };

    private static readonly Dictionary<string, PhaseDefinition> Phases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["37C"] = new PhaseDefinition { Code = "37C", Name = "37°C" },
        ["AHG"] = new PhaseDefinition { Code = "AHG", Name = "AHG" },
        ["CC"] = new PhaseDefinition { Code = "CC", Name = "Check cells", IsCheckCell = true, IncludeInInterpretation = false }
    };

    private static IReadOnlyList<string> Positive(params (string Key, string Value)[] entered) =>
        CrossmatchReactionRule.PositiveReactions(
            entered.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase), Catalog, Phases);

    // TEST-BB-686: positive counted reactions are named; check cells (as a subtest or a phase), neutral grades (mixed
    // field, not tested), blanks and free text are not counted.
    [Fact]
    public void PositiveReactions_NamesCountedPositivesOnly()
    {
        Assert.Equal(["AHG 2+"], Positive(("IS", "0"), ("AHG", "2+"), ("CC", "3+")));
        Assert.Equal(["Donor cell 37°C Weak positive"],
            Positive((PhaseResultKeys.Compose("Cell1", "37C"), "w+"), (PhaseResultKeys.Compose("Cell1", "CC"), "2+")));
        Assert.Empty(Positive(("IS", "+/-"), ("AHG", "NT"), ("NOTE", "4+"), ("CC", "2+")));
        Assert.Empty(Positive(("IS", " "), ("AHG", "0")));
        Assert.Equal(["Immediate spin Hemolysis"], Positive(("IS", "h")));
    }

    // TEST-BB-687: Compatible with a positive reaction is a hard stop naming it; Incompatible, or Compatible with none,
    // is not stopped.
    [Fact]
    public void Evaluate_StopsCompatibleWithPositiveReactions()
    {
        var stop = CrossmatchReactionRule.Evaluate(CrossmatchResult.Compatible, ["AHG 2+"]);
        Assert.NotNull(stop);
        Assert.Equal(RuleSeverity.HardStop, stop!.Severity);
        Assert.Equal(CrossmatchReactionRule.PositiveOnCompatibleCode, stop.Code);
        Assert.Contains("positive reaction (AHG 2+) cannot be recorded as Compatible", stop.Message);

        Assert.Null(CrossmatchReactionRule.Evaluate(CrossmatchResult.Incompatible, ["AHG 2+"]));
        Assert.Null(CrossmatchReactionRule.Evaluate(CrossmatchResult.Compatible, []));
    }
}
