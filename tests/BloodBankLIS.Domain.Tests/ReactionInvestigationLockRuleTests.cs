using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-327: a closed, signed reaction investigation is locked until it is reopened with a reason.
public class ReactionInvestigationLockRuleTests
{
    [Theory]
    [InlineData(ReactionInvestigationStatus.Open, true, null, false)]
    [InlineData(ReactionInvestigationStatus.UnderReview, true, ReactionInvestigationStatus.Closed, false)]
    [InlineData(ReactionInvestigationStatus.Closed, false, null, false)]
    [InlineData(ReactionInvestigationStatus.Closed, false, ReactionInvestigationStatus.Closed, false)]
    [InlineData(ReactionInvestigationStatus.Closed, true, null, true)]
    [InlineData(ReactionInvestigationStatus.Closed, false, ReactionInvestigationStatus.Open, true)]
    [InlineData(ReactionInvestigationStatus.Closed, false, ReactionInvestigationStatus.UnderReview, true)]
    public void Update(ReactionInvestigationStatus current, bool changesFindings, ReactionInvestigationStatus? requested, bool stopped) =>
        Assert.Equal(stopped, ReactionInvestigationLockRule.EvaluateUpdate(current, changesFindings, requested).Severity == RuleSeverity.HardStop);

    [Theory]
    [InlineData(ReactionInvestigationStatus.Closed, "Late culture result", false)]
    [InlineData(ReactionInvestigationStatus.Closed, " ", true)]
    [InlineData(ReactionInvestigationStatus.Closed, null, true)]
    [InlineData(ReactionInvestigationStatus.Open, "Anything", true)]
    public void Reopen(ReactionInvestigationStatus current, string? reason, bool stopped) =>
        Assert.Equal(stopped, ReactionInvestigationLockRule.EvaluateReopen(current, reason).Severity == RuleSeverity.HardStop);
}
