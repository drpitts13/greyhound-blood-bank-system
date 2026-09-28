using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReactionFatalityFollowUpRuleTests
{
    [Fact]
    public void NonFatality_Passes()
    {
        var result = ReactionFatalityFollowUpRule.Evaluate(false, null, null);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void ClosedFatality_StaysDueUntilCberAndWrittenReport()
    {
        var both = ReactionFatalityFollowUpRule.Evaluate(true, null, null);
        Assert.Equal(RuleSeverity.Warning, both.Severity);
        Assert.Equal(ReactionFatalityFollowUpRule.Code, both.Code);
        Assert.Contains("CBER", both.Message);
        Assert.Contains("written report", both.Message);

        var writtenOnly = ReactionFatalityFollowUpRule.Evaluate(true, null, DateTime.UtcNow);
        Assert.Equal(RuleSeverity.Warning, writtenOnly.Severity);
        Assert.Contains("CBER", writtenOnly.Message);

        var cberOnly = ReactionFatalityFollowUpRule.Evaluate(true, DateTime.UtcNow, null);
        Assert.Equal(RuleSeverity.Warning, cberOnly.Severity);
        Assert.Contains("written report", cberOnly.Message);

        var done = ReactionFatalityFollowUpRule.Evaluate(true, DateTime.UtcNow, DateTime.UtcNow);
        Assert.Equal(RuleSeverity.Pass, done.Severity);
    }
}
