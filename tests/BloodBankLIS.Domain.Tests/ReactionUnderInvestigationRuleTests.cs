using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>OCD-078 (decided 2026-11-10): another unit while a reaction is under investigation needs an override.</summary>
public class ReactionUnderInvestigationRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    // TEST-BB-855: a unit issued or started after the reaction was reported warns (an overridable warning naming the
    // investigation); one issued or started before it was reported, or with no investigation open, does not.
    [Fact]
    public void Warns_OnlyAfterTheReactionWasReported()
    {
        var after = ReactionUnderInvestigationRule.Evaluate(7, Now.AddHours(-1), Now);
        Assert.Equal((RuleSeverity.Warning, ReactionUnderInvestigationRule.Code), (after.Severity, after.Code));
        Assert.Contains("#7", after.Message);
        Assert.Contains("OCD-078", after.Message);
        Assert.Equal(RuleSeverity.Warning, ReactionUnderInvestigationRule.Evaluate(7, Now, Now).Severity);
        Assert.Equal(RuleSeverity.Pass, ReactionUnderInvestigationRule.Evaluate(7, Now.AddHours(1), Now).Severity);
        Assert.Equal(RuleSeverity.Pass, ReactionUnderInvestigationRule.Evaluate(null, null, Now).Severity);
    }
}
