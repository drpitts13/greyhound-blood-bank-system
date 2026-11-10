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

    // TEST-BB-804: with no current specimen, a tube drawn and not yet received, or received and not yet accepted, is
    // named so the bench receives or accepts it instead of drawing again; it is not the current specimen and the banner
    // still blocks issue. An expired tube is not named.
    [Fact]
    public void NoCurrentSpecimen_NamesTheTubeOnItsWay()
    {
        var drawn = new CurrentSpecimenBanner.Fact("ACC-D", SpecimenStatus.Collected, Now.AddMinutes(-30), Now.AddDays(3));
        var cue = CurrentSpecimenBanner.Describe([drawn], Now);
        Assert.Equal((null, "No current specimen.", true, "ACC-D"), (cue.AccessionNumber, cue.Message, cue.BlocksIssue, cue.AwaitingAccession));
        Assert.Equal("Tube ACC-D was drawn 2026-09-29 21:30 UTC and is not yet received: receive it rather than drawing again.", cue.AwaitingNote);

        var received = new CurrentSpecimenBanner.Fact("ACC-R", SpecimenStatus.Received, Now.AddMinutes(-10), Now.AddDays(3));
        Assert.Equal("Tube ACC-R is received and not yet accepted: accept it rather than drawing again.",
            CurrentSpecimenBanner.Describe([drawn, received], Now).AwaitingNote);

        var stale = new CurrentSpecimenBanner.Fact("ACC-S", SpecimenStatus.Collected, Now.AddDays(-4), Now.AddHours(-1));
        var none = CurrentSpecimenBanner.Describe([stale], Now);
        Assert.Equal(((string?)null, (string?)null), (none.AwaitingAccession, none.AwaitingNote));
    }

    // TEST-BB-807: the shared helper picks the newest in-date tube drawn or received (not an accepted or expired one), and
    // only one drawn after a given time when asked.
    [Fact]
    public void TubeOnItsWay_PicksTheNewestInDateTube()
    {
        var accepted = new CurrentSpecimenBanner.Fact("ACC-A", SpecimenStatus.Accepted, Now.AddMinutes(-5), Now.AddDays(3));
        var older = new CurrentSpecimenBanner.Fact("ACC-1", SpecimenStatus.Received, Now.AddHours(-3), Now.AddDays(3));
        var newer = new CurrentSpecimenBanner.Fact("ACC-2", SpecimenStatus.Collected, Now.AddHours(-1), Now.AddDays(3));
        var expired = new CurrentSpecimenBanner.Fact("ACC-X", SpecimenStatus.Collected, Now.AddMinutes(-2), Now.AddMinutes(-1));
        Assert.Equal("ACC-2", CurrentSpecimenBanner.TubeOnItsWay([accepted, older, newer, expired], Now)!.AccessionNumber);
        Assert.Null(CurrentSpecimenBanner.TubeOnItsWay([accepted, older, newer], Now, drawnAfterUtc: Now.AddMinutes(-30)));
        Assert.Null(CurrentSpecimenBanner.TubeOnItsWay([accepted, expired], Now));
    }

    // TEST-BB-805: an expired current specimen names a newer tube on its way; a tube drawn before it, or any tube while
    // the current specimen is in date, is not named.
    [Fact]
    public void ExpiredCurrent_NamesOnlyANewerTube()
    {
        var expired = new CurrentSpecimenBanner.Fact("ACC-EXP", SpecimenStatus.Accepted, Now.AddDays(-3), Now.AddMinutes(-5));
        var older = new CurrentSpecimenBanner.Fact("ACC-OLDER", SpecimenStatus.Collected, Now.AddDays(-4), Now.AddDays(1));
        var newer = new CurrentSpecimenBanner.Fact("ACC-NEWER", SpecimenStatus.Collected, Now.AddMinutes(-20), Now.AddDays(3));
        Assert.Null(CurrentSpecimenBanner.Describe([expired, older], Now).AwaitingAccession);
        var cue = CurrentSpecimenBanner.Describe([expired, older, newer], Now);
        Assert.Equal(("ACC-EXP", true, "ACC-NEWER"), (cue.AccessionNumber, cue.BlocksIssue, cue.AwaitingAccession));

        var inDate = new CurrentSpecimenBanner.Fact("ACC-OK", SpecimenStatus.Accepted, Now.AddDays(-1), Now.AddDays(2));
        Assert.Null(CurrentSpecimenBanner.Describe([inDate, newer], Now).AwaitingAccession);
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

    // TEST-BB-716: the chart shows the specimen's times in the workstation's local time (it passes the conversion);
    // the timing decision still uses UTC.
    [Fact]
    public void Times_AreShownThroughTheDisplayConversion()
    {
        var soon = new CurrentSpecimenBanner.Fact("ACC-SOON", SpecimenStatus.Accepted, Now.AddDays(-3), Now.AddMinutes(3));
        var cue = CurrentSpecimenBanner.Describe([soon], Now, t => t.AddHours(-5));
        Assert.True(cue.ExpiresSoon);
        Assert.Equal("Specimen ACC-SOON expires soon, at 2026-09-29 17:03.", cue.Message);
        Assert.Equal("Specimen ACC-SOON expires soon, at 2026-09-29 22:03.", CurrentSpecimenBanner.Describe([soon], Now).Message);
    }
}
