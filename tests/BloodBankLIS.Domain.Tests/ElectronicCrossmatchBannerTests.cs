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

    // TEST-BB-809: the header notice is silent when the facility has electronic crossmatch off (it is off for everyone)
    // and otherwise is the patient's blocking reason; the badge reads off, eligible, or not eligible.
    [Fact]
    public void HeaderNotice_AndBadge_SeparateFacilityOffFromThisPatient()
    {
        Assert.Null(ElectronicCrossmatchBanner.HeaderNotice(eligible: false, facilityAllows: false, "Disabled in facility policy."));
        Assert.Equal("Screen positive.", ElectronicCrossmatchBanner.HeaderNotice(eligible: false, facilityAllows: true, " Screen positive. "));
        Assert.Null(ElectronicCrossmatchBanner.HeaderNotice(eligible: true, facilityAllows: true, null));
        Assert.Equal("Off at this facility", ElectronicCrossmatchBanner.BadgeText(eligible: false, facilityAllows: false));
        Assert.Equal("Not eligible", ElectronicCrossmatchBanner.BadgeText(eligible: false, facilityAllows: true));
        Assert.Equal("Eligible", ElectronicCrossmatchBanner.BadgeText(eligible: true, facilityAllows: true));
    }
}
