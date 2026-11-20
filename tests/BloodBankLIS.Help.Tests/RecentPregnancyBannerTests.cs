using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class RecentPregnancyBannerTests
{
    // TEST-BB-1336 (end-user testing pass 309): the chart header says a pregnancy within the look-back period puts the
    // patient's specimens on the shorter alloimmunization-risk window; an older one, or none, says nothing.
    [Fact]
    public void For_NotesARecentPregnancy()
    {
        var now = new DateTime(2026, 11, 19, 20, 0, 0, DateTimeKind.Utc);
        var pregnancy = new DateTime(2026, 10, 7, 14, 50, 0, DateTimeKind.Utc);
        Assert.Equal($"Recent pregnancy {ClinicalTimeText.Local(pregnancy)[..10]}: specimens take the shorter alloimmunization-risk window.",
            RecentPregnancyBanner.For(pregnancy, now));
        Assert.Null(RecentPregnancyBanner.For(now.AddDays(-120), now));
        Assert.Null(RecentPregnancyBanner.For(null, now));
    }
}
