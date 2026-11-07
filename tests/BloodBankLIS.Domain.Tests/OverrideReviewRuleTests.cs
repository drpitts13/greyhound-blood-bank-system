using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-332: an override is reviewed once, by someone who neither performed nor authorized it.
public class OverrideReviewRuleTests
{
    [Theory]
    [InlineData(false, "supervisor1", OverrideReviewOutcome.Acceptable, null, false)]
    [InlineData(false, "supervisor1", OverrideReviewOutcome.FollowUpRequired, "Ask why O-neg stock was not used", false)]
    [InlineData(false, "supervisor1", OverrideReviewOutcome.FollowUpRequired, " ", true)]
    [InlineData(true, "supervisor1", OverrideReviewOutcome.Acceptable, null, true)]
    [InlineData(false, "TECH1", OverrideReviewOutcome.Acceptable, null, true)]        // performer
    [InlineData(false, "dr-lee", OverrideReviewOutcome.Acceptable, null, true)]       // authorizer
    [InlineData(false, "supervisor1", (OverrideReviewOutcome)0, null, true)]          // no outcome
    public void Evaluate(bool reviewed, string reviewer, OverrideReviewOutcome outcome, string? note, bool stopped) =>
        Assert.Equal(stopped, OverrideReviewRule.Evaluate(reviewed, reviewer, "tech1", "Dr-Lee", outcome, note).Severity == RuleSeverity.HardStop);
}
