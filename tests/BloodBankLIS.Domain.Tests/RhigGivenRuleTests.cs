using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-575: RhIG given is recorded once, on a completed candidate evaluation, with a lot, 1–50 vials, and a time not in
// the future; a difference from the vials recommended is noted, not stopped.
public class RhigGivenRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Given_IsRefused_UntilTheEvaluationAndEntriesAllowIt()
    {
        RuleResult Given(bool completed = true, RhigDecision decision = RhigDecision.Candidate, bool already = false, string? lot = "LOT-1", int vials = 1, int hours = 0) =>
            RhigEvaluationRule.EvaluateGiven(completed, decision, already, lot, vials, Now.AddHours(hours), Now);

        Assert.Equal(RuleSeverity.Pass, Given().Severity);
        Assert.Contains("completed evaluation", Given(completed: false).Message);
        Assert.Contains("completed evaluation", Given(decision: RhigDecision.NotCandidate).Message);
        Assert.Contains("already recorded", Given(already: true).Message);
        Assert.Contains("lot", Given(lot: " ").Message);
        Assert.Contains("between 1 and 50", Given(vials: 0).Message);
        Assert.Contains("future", Given(hours: 2).Message);
    }

    [Fact]
    public void Difference_IsNotedOnly_WhenTheVialsDiffer()
    {
        Assert.Equal("1 vial given; 2 recommended.", RhigEvaluationRule.GivenDifference(2, 1));
        Assert.Null(RhigEvaluationRule.GivenDifference(2, 2));
        Assert.Null(RhigEvaluationRule.GivenDifference(null, 2));
    }
}
