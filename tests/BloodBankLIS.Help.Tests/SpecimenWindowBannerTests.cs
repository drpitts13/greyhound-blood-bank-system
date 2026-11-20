using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class SpecimenWindowBannerTests
{
    // TEST-BB-1336 (end-user testing passes 309-310): the chart header says why new specimens take the shorter window (a
    // recent pregnancy, a recent transfusion, or both) with the facility's look-back and hours; no risk says nothing.
    [Fact]
    public void For_SaysWhyAndForHowLong()
    {
        var pregnancy = new DateTime(2026, 10, 7, 14, 50, 0, DateTimeKind.Utc);
        var transfused = new DateTime(2026, 11, 6, 14, 30, 0, DateTimeKind.Utc);
        Assert.Equal($"Recent pregnancy {ClinicalTimeText.Local(pregnancy)[..10]} (within 90 days): new specimens expire 72 h after collection.",
            SpecimenWindowBanner.For(new SpecimenWindowDto(true, 72, 90, pregnancy, null)));
        Assert.Equal($"Transfused {ClinicalTimeText.Local(transfused)[..10]} (within 60 days): new specimens expire 48 h after collection.",
            SpecimenWindowBanner.For(new SpecimenWindowDto(true, 48, 60, null, transfused)));
        Assert.Equal($"Recent pregnancy {ClinicalTimeText.Local(pregnancy)[..10]} and transfused {ClinicalTimeText.Local(transfused)[..10]} (within 90 days): new specimens expire 72 h after collection.",
            SpecimenWindowBanner.For(new SpecimenWindowDto(true, 72, 90, pregnancy, transfused)));
        Assert.Null(SpecimenWindowBanner.For(new SpecimenWindowDto(false, 72, 90, null, null)));
        Assert.Null(SpecimenWindowBanner.For(null));
    }
}
