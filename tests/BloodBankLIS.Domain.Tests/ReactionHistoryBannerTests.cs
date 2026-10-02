using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReactionHistoryBannerTests
{
    [Fact]
    public void Notice_NamesARecordedReactionAndStaysQuietOtherwise()
    {
        Assert.Equal(ReactionHistoryBanner.RecordedReaction, ReactionHistoryBanner.Notice(hasRecordedReaction: true));
        Assert.Null(ReactionHistoryBanner.Notice(hasRecordedReaction: false));
    }
}
