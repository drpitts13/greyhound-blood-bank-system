using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class OpenReactionInvestigationRuleTests
{
    [Fact]
    public void Count_IncludesOpenAndUnderReview_AndOmitsClosed()
    {
        var count = OpenReactionInvestigationRule.Count(
        [
            ReactionInvestigationStatus.Open,
            ReactionInvestigationStatus.UnderReview,
            ReactionInvestigationStatus.Closed
        ]);

        Assert.Equal(2, count);
        Assert.False(OpenReactionInvestigationRule.NeedsAttention(ReactionInvestigationStatus.Closed));
    }
}
