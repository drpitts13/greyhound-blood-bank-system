using BloodBankLIS.Application.Results;
using BloodBankLIS.Web.Components.Shared;
using BloodBankLIS.Web.Services;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>A short local time that never hides its day (Cycle 618).</summary>
public class ClockTextTests : BunitContext
{
    // TEST-BB-738: today shows the time alone; yesterday and tomorrow are named; further away shows the date; seconds
    // only when asked.
    [Fact]
    public void Short_NamesTheDayWhenItIsNotToday()
    {
        var now = new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Local);

        Assert.Equal("07:30", ClockText.Short(now.Date.AddHours(7.5), now));
        Assert.Equal("07:30:15", ClockText.Short(now.Date.AddHours(7.5).AddSeconds(15), now, seconds: true));
        Assert.Equal("tomorrow 07:30", ClockText.Short(now.Date.AddDays(1).AddHours(7.5), now));
        Assert.Equal("yesterday 22:10", ClockText.Short(now.Date.AddDays(-1).AddHours(22).AddMinutes(10), now));
        Assert.Equal("2026-11-06 22:10", ClockText.Short(now.Date.AddDays(-3).AddHours(22).AddMinutes(10), now));
        Assert.Equal("2026-11-12 08:00", ClockText.Short(now.Date.AddDays(3).AddHours(8), now));

        // A stored UTC moment is converted to local time first.
        var utc = new DateTime(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc);
        Assert.Equal(utc.ToLocalTime().ToString("HH:mm"), ClockText.Short(utc, utc.ToLocalTime()));
    }

    // TEST-BB-740: the verify-before badge shows the specimen's expiry in local time (it printed the stored UTC value as
    // if it were local) and names its day.
    [Fact]
    public void VerificationDeadlineBadge_ShowsTheLocalExpiry()
    {
        var expires = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(1), DateTimeKind.Utc);
        var item = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TestWorkItemDto)) as TestWorkItemDto;
        item = item! with { SpecimenExpiresUtc = expires, VerificationDeadlineNotice = "Verify before the specimen expires." };

        var badge = Render<VerificationDeadlineBadge>(p => p.Add(b => b.Item, item));

        Assert.Equal($"Verify before {ClockText.Short(expires)}", badge.Find("span").TextContent.Trim());
        Assert.Contains(expires.ToLocalTime().ToString("HH:mm"), badge.Markup);
    }
}
