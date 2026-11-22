using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class InterpretationLogicMatcherTests
{
    private const ReactionPolarity Neg = ReactionPolarity.Negative;
    private const ReactionPolarity Pos = ReactionPolarity.Positive;

    [Theory]
    [InlineData(Neg, Neg, Pos, Pos, Pos, AboGroup.O, RhType.Positive)]
    [InlineData(Pos, Neg, Neg, Neg, Pos, AboGroup.A, RhType.Negative)]
    [InlineData(Neg, Pos, Pos, Pos, Neg, AboGroup.B, RhType.Positive)]
    [InlineData(Pos, Pos, Neg, Neg, Neg, AboGroup.AB, RhType.Negative)]
    public void Match_AboRhReactions_PicksTheType(
        ReactionPolarity antiA, ReactionPolarity antiB, ReactionPolarity antiD,
        ReactionPolarity aCells, ReactionPolarity bCells, AboGroup abo, RhType rh)
    {
        var row = InterpretationLogicMatcher.Match(
            InterpretationLogicDefinitions.DefaultAboRhLogic(),
            AboRh(antiA, antiB, antiD, aCells, bCells));

        Assert.Equal(InterpretationLogicDefinitions.BuildAboRhKey(abo, rh), row?.InterpretationKey);
    }

    [Fact]
    public void Match_ForwardAndReverseDisagree_ReturnsNull()
    {
        // Forward type says A, reverse says O: a discrepancy the technologist must resolve.
        var row = InterpretationLogicMatcher.Match(
            InterpretationLogicDefinitions.DefaultAboRhLogic(),
            AboRh(Pos, Neg, Pos, Pos, Pos));

        Assert.Null(row);
    }

    [Fact]
    public void Match_ReactionNotEnteredYet_ReturnsNull()
    {
        var reactions = AboRh(Neg, Neg, Pos, Pos, Pos);
        reactions[AboRhPanelSubtestCodes.BCells] = null;

        Assert.Null(InterpretationLogicMatcher.Match(InterpretationLogicDefinitions.DefaultAboRhLogic(), reactions));
    }

    [Fact]
    public void Match_MixedFieldReaction_ReturnsNull()
    {
        var reactions = AboRh(Neg, Neg, Pos, Pos, Pos);
        reactions[AboRhPanelSubtestCodes.AntiD] = ReactionPolarity.Neutral;

        Assert.Null(InterpretationLogicMatcher.Match(InterpretationLogicDefinitions.DefaultAboRhLogic(), reactions));
    }

    [Fact]
    public void Match_SubtestNotOnPanel_IsSkipped()
    {
        // Front type only (retype panels): reverse cells are not part of the reactions.
        var reactions = new Dictionary<string, ReactionPolarity?>(StringComparer.OrdinalIgnoreCase)
        {
            [AboRhPanelSubtestCodes.AntiA] = Pos,
            [AboRhPanelSubtestCodes.AntiB] = Neg,
            [AboRhPanelSubtestCodes.AntiD] = Pos
        };

        var row = InterpretationLogicMatcher.Match(InterpretationLogicDefinitions.DefaultAboRhLogic(), reactions);

        Assert.Equal(InterpretationLogicDefinitions.BuildAboRhKey(AboGroup.A, RhType.Positive), row?.InterpretationKey);
    }

    [Fact]
    public void Match_AntibodyScreenAllNegative_IsNegative()
    {
        var logic = ScreenLogic();
        var reactions = ScreenReactions(Neg);

        Assert.Equal("Negative", InterpretationLogicMatcher.Match(logic, reactions)?.InterpretationKey);
    }

    [Fact]
    public void Match_AntibodyScreenOnePositive_IsPositiveEvenBeforeTheRestAreEntered()
    {
        var logic = ScreenLogic();
        var reactions = ScreenReactions(null);
        reactions[PhaseResultKeys.Compose("Cell2", "AHG")] = Pos;

        Assert.Equal("Positive", InterpretationLogicMatcher.Match(logic, reactions)?.InterpretationKey);
    }

    [Fact]
    public void Match_AntibodyScreenPartlyEnteredAndNegative_ReturnsNull()
    {
        var reactions = ScreenReactions(Neg);
        reactions[PhaseResultKeys.Compose("Cell3", "AHG")] = null;

        Assert.Null(InterpretationLogicMatcher.Match(ScreenLogic(), reactions));
    }

    [Fact]
    public void CrossmatchLogic_AllNegative_IsCompatible_AnyPositive_IsIncompatible()
    {
        var reactions = new Dictionary<string, ReactionPolarity?>(StringComparer.OrdinalIgnoreCase)
        {
            ["IS"] = Neg,
            ["AHG"] = Neg
        };
        var logic = InterpretationLogicMatcher.CrossmatchLogic(reactions.Keys);

        Assert.Equal(InterpretationLogicMatcher.CrossmatchCompatibleKey, InterpretationLogicMatcher.Match(logic, reactions)?.InterpretationKey);

        reactions["AHG"] = Pos;
        Assert.Equal(InterpretationLogicMatcher.CrossmatchIncompatibleKey, InterpretationLogicMatcher.Match(logic, reactions)?.InterpretationKey);
    }

    private static Dictionary<string, ReactionPolarity?> AboRh(
        ReactionPolarity antiA, ReactionPolarity antiB, ReactionPolarity antiD,
        ReactionPolarity aCells, ReactionPolarity bCells) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [AboRhPanelSubtestCodes.AntiA] = antiA,
            [AboRhPanelSubtestCodes.AntiB] = antiB,
            [AboRhPanelSubtestCodes.AntiD] = antiD,
            [AboRhPanelSubtestCodes.ACells] = aCells,
            [AboRhPanelSubtestCodes.BCells] = bCells
        };

    private static IReadOnlyList<InterpretationLogicRow> ScreenLogic() =>
        InterpretationLogicDefinitions.DefaultAntibodyScreenLogic(["Cell1", "Cell2", "Cell3"], ["IS", "37C", "AHG"]);

    private static Dictionary<string, ReactionPolarity?> ScreenReactions(ReactionPolarity? each)
    {
        var reactions = new Dictionary<string, ReactionPolarity?>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in new[] { "Cell1", "Cell2", "Cell3" })
        {
            foreach (var phase in new[] { "IS", "37C", "AHG" })
            {
                reactions[PhaseResultKeys.Compose(cell, phase)] = each;
            }
        }

        return reactions;
    }
}

public class AboRhForwardTypeLogicTests
{
    [Theory]
    [InlineData(AboGroup.A, ReactionPolarity.Positive, ReactionPolarity.Negative)]
    [InlineData(AboGroup.B, ReactionPolarity.Negative, ReactionPolarity.Positive)]
    [InlineData(AboGroup.AB, ReactionPolarity.Positive, ReactionPolarity.Positive)]
    [InlineData(AboGroup.O, ReactionPolarity.Negative, ReactionPolarity.Negative)]
    public void DefaultLogic_ForwardType_MatchesReagentAntisera(AboGroup abo, ReactionPolarity antiA, ReactionPolarity antiB)
    {
        foreach (var logic in new[] { InterpretationLogicDefinitions.DefaultAboRhLogic(), InterpretationLogicDefinitions.DefaultAboRhRetypeLogic() })
        {
            var row = logic.Single(r => r.InterpretationKey == InterpretationLogicDefinitions.BuildAboRhKey(abo, RhType.Positive));
            Assert.Equal(antiA, row.SubtestExpectations[AboRhPanelSubtestCodes.AntiA]);
            Assert.Equal(antiB, row.SubtestExpectations[AboRhPanelSubtestCodes.AntiB]);
        }
    }

    [Fact]
    public void RepairSwappedForwardType_FixesOnlyTheSwappedRows()
    {
        var correct = InterpretationLogicDefinitions.DefaultAboRhLogic();
        var swapped = correct.Select(r => r.InterpretationKey.StartsWith("A|") || r.InterpretationKey.StartsWith("B|")
                ? r with
                {
                    SubtestExpectations = new Dictionary<string, ReactionPolarity>(r.SubtestExpectations, StringComparer.OrdinalIgnoreCase)
                    {
                        [AboRhPanelSubtestCodes.AntiA] = r.SubtestExpectations[AboRhPanelSubtestCodes.AntiB],
                        [AboRhPanelSubtestCodes.AntiB] = r.SubtestExpectations[AboRhPanelSubtestCodes.AntiA]
                    }
                }
                : r)
            .ToList();

        var repaired = InterpretationLogicDefinitions.RepairSwappedForwardType(swapped, out var changed);

        Assert.True(changed);
        Assert.Equal(InterpretationLogicDefinitions.ToJson(correct), InterpretationLogicDefinitions.ToJson(repaired));

        InterpretationLogicDefinitions.RepairSwappedForwardType(correct, out var changedAgain);
        Assert.False(changedAgain);
    }
}
