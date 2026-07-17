using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class RuleEvaluationTests
{
    [Fact]
    public void AllPass_IsAllowed()
    {
        var eval = new RuleEvaluation(new[]
        {
            RuleResult.Pass("A"),
            RuleResult.Pass("B")
        });

        Assert.Equal(RuleSeverity.Pass, eval.OverallSeverity);
        Assert.True(eval.IsAllowed);
        Assert.False(eval.RequiresOverride);
        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void WarningsOnly_RequireOverride()
    {
        var eval = new RuleEvaluation(new[]
        {
            RuleResult.Pass("A"),
            RuleResult.Warning("B", "near expiry")
        });

        Assert.Equal(RuleSeverity.Warning, eval.OverallSeverity);
        Assert.True(eval.RequiresOverride);
        Assert.False(eval.IsAllowed);
    }

    [Fact]
    public void AnyHardStop_DominatesWarnings()
    {
        var eval = new RuleEvaluation(new[]
        {
            RuleResult.Warning("A", "near expiry"),
            RuleResult.HardStop("B", "incompatible"),
            RuleResult.Pass("C")
        });

        Assert.Equal(RuleSeverity.HardStop, eval.OverallSeverity);
        Assert.True(eval.IsHardStopped);
        Assert.False(eval.RequiresOverride);
        Assert.Single(eval.HardStops);
        Assert.Single(eval.Warnings);
    }
}
