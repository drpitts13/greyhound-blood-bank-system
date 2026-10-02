using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class CurrentSpecimenBannerTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoAcceptedSpecimen_BlocksIssue()
    {
        var collected = new CurrentSpecimenBanner.Fact("ACC-C", SpecimenStatus.Collected, Now.AddHours(-2), Now.AddDays(2));
        var cue = CurrentSpecimenBanner.Describe([collected], Now);
        Assert.True(cue.BlocksIssue);
        Assert.Equal("No current specimen.", cue.Message);
    }

    [Fact]
    public void LatestAcceptedSpecimen_IsTheOneTheIssueGateReads()
    {
        var older = new CurrentSpecimenBanner.Fact("ACC-OLD", SpecimenStatus.Accepted, Now.AddDays(-2), Now.AddDays(3));
        var newer = new CurrentSpecimenBanner.Fact("ACC-NEW", SpecimenStatus.Accepted, Now.AddHours(-1), Now.AddDays(2));
        var cue = CurrentSpecimenBanner.Describe([older, newer], Now);
        Assert.Equal("ACC-NEW", cue.AccessionNumber);
        Assert.False(cue.BlocksIssue);
        Assert.False(cue.ExpiresSoon);
    }

    [Fact]
    public void ExpiredCurrentSpecimen_BlocksIssue()
    {
        var expired = new CurrentSpecimenBanner.Fact("ACC-EXP", SpecimenStatus.Accepted, Now.AddHours(-2), Now.AddMinutes(-1));
        var olderInDate = new CurrentSpecimenBanner.Fact("ACC-OK", SpecimenStatus.Accepted, Now.AddDays(-1), Now.AddDays(1));
        var cue = CurrentSpecimenBanner.Describe([olderInDate, expired], Now);
        Assert.Equal("ACC-EXP", cue.AccessionNumber);
        Assert.True(cue.BlocksIssue);
        Assert.Contains("expired", cue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpecimenInsideTwelveHours_IsExpiresSoon()
    {
        var soon = new CurrentSpecimenBanner.Fact("ACC-SOON", SpecimenStatus.Accepted, Now.AddHours(-1), Now.AddHours(6));
        var cue = CurrentSpecimenBanner.Describe([soon], Now);
        Assert.False(cue.BlocksIssue);
        Assert.True(cue.ExpiresSoon);
    }
}
