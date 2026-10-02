using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ElectronicCrossmatchBannerTests
{
    [Fact]
    public void Notice_ShowsBlockingReasonOnlyWhenNotEligible()
    {
        const string history =
            "Electronic crossmatch is not permitted with a history of clinically significant antibodies, including antibodies that are currently undetectable.";

        Assert.Null(ElectronicCrossmatchBanner.Notice(eligible: true, history));
        Assert.Null(ElectronicCrossmatchBanner.Notice(eligible: false, "  "));
        Assert.Null(ElectronicCrossmatchBanner.Notice(eligible: false, null));
        Assert.Equal(history, ElectronicCrossmatchBanner.Notice(eligible: false, $"  {history}  "));
    }
}
