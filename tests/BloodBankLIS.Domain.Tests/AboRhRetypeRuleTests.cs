using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class AboRhRetypeRuleTests
{
    [Fact]
    public void RhPositive_MatchingAbo_DoesNotRequireAntiD()
    {
        var outcome = AboRhRetypeRule.Evaluate(AboGroup.O, RhType.Positive, new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "0",
            [AboRhPanelSubtestCodes.AntiB] = "0"
        });

        Assert.True(outcome.CanRecord);
        Assert.True(outcome.MatchesLabel);
        Assert.Equal(AboGroup.O, outcome.InterpretedAbo);
        Assert.Null(outcome.InterpretedRh);
    }

    [Fact]
    public void RhPositive_WrongAbo_IsMismatch()
    {
        var outcome = AboRhRetypeRule.Evaluate(AboGroup.O, RhType.Positive, new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "4+",
            [AboRhPanelSubtestCodes.AntiB] = "0"
        });

        Assert.True(outcome.CanRecord);
        Assert.False(outcome.MatchesLabel);
        Assert.Equal(AboGroup.A, outcome.InterpretedAbo);
        Assert.Contains("discrepancy", outcome.DiscrepancyDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RhNegative_RequiresAntiD()
    {
        var outcome = AboRhRetypeRule.Evaluate(AboGroup.A, RhType.Negative, new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "4+",
            [AboRhPanelSubtestCodes.AntiB] = "0"
        });

        Assert.False(outcome.CanRecord);
        Assert.True(outcome.Validation.IsHardStopped);
        Assert.Contains(outcome.Validation.HardStops, r => r.Code == AboRhRetypeRule.IncompleteCode);
    }

    [Fact]
    public void RhNegative_MatchingAboAndD_Confirms()
    {
        var outcome = AboRhRetypeRule.Evaluate(AboGroup.A, RhType.Negative, new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "3+",
            [AboRhPanelSubtestCodes.AntiB] = "0",
            [AboRhPanelSubtestCodes.AntiD] = "0"
        });

        Assert.True(outcome.CanRecord);
        Assert.True(outcome.MatchesLabel);
        Assert.Equal(AboGroup.A, outcome.InterpretedAbo);
        Assert.Equal(RhType.Negative, outcome.InterpretedRh);
    }

    [Fact]
    public void RhNegative_AntiDPositive_IsMismatch()
    {
        var outcome = AboRhRetypeRule.Evaluate(AboGroup.O, RhType.Negative, new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "0",
            [AboRhPanelSubtestCodes.AntiB] = "0",
            [AboRhPanelSubtestCodes.AntiD] = "4+"
        });

        Assert.True(outcome.CanRecord);
        Assert.False(outcome.MatchesLabel);
        Assert.Equal(RhType.Positive, outcome.InterpretedRh);
    }

    [Fact]
    public void IncompleteAntiA_HardStops()
    {
        var outcome = AboRhRetypeRule.Evaluate(AboGroup.O, RhType.Positive, new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiB] = "0"
        });

        Assert.False(outcome.CanRecord);
        Assert.Contains(outcome.Validation.HardStops, r => r.Code == AboRhRetypeRule.IncompleteCode);
    }

    [Fact]
    public void MixedField_HardStops()
    {
        var outcome = AboRhRetypeRule.Evaluate(AboGroup.O, RhType.Positive, new Dictionary<string, string>
        {
            [AboRhPanelSubtestCodes.AntiA] = "+/-",
            [AboRhPanelSubtestCodes.AntiB] = "0"
        });

        Assert.False(outcome.CanRecord);
        Assert.Contains(outcome.Validation.HardStops, r => r.Code == AboRhRetypeRule.GradeCode);
    }
}
