using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Recorded outages (Cycle 662): the downtime page records and ends them and counts the paper issues back-entered in
/// each; back-entry on the Issue page offers the outage, carries it as the reason, and warns when the paper time falls
/// outside every recorded outage.
/// </summary>
public class DowntimeOutagePageTests : BunitContext
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static List<DowntimeEpisodeDto> Episodes() =>
    [
        new(2, Now.AddHours(-3), null, "LIS upgrade", "tech1", null, 0, 0, "Issued on paper during downtime #2: LIS upgrade"),
        new(1, Now.AddDays(-2), Now.AddDays(-2).AddHours(4), "Network outage", "tech1", "super1", 3, 1, "Issued on paper during downtime #1: Network outage")
    ];

    // TEST-BB-847: the open outage stands out with End outage (the time defaulting to now); each outage lists its window,
    // what was down, the paper issues back-entered (those still open linked to Issued units) and who recorded and ended
    // it; recording another waits for a start and a description.
    [Fact]
    public void DowntimePage_ShowsAndRecordsOutages()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/compliance/downtime-episodes"] = Episodes(),
            ["/api/compliance/downtime-episodes/2/end"] = Episodes()[0] with { EndedUtc = Now }
        });
        var page = Render<DowntimeReconciliation>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-outage-open='2']")));

        Assert.Contains("LIS upgrade", page.Find("[data-outage-open='2']").TextContent);
        Assert.False(string.IsNullOrEmpty(page.Find("[data-outage-end-time]").GetAttribute("value")));
        Assert.Contains("Still down", page.Find("[data-outage='2']").TextContent);
        var ended = page.Find("[data-outage='1']");
        Assert.Contains("Network outage", ended.TextContent);
        Assert.Contains("1 transfusion not yet entered", ended.TextContent);
        Assert.Equal("/issuing?desk=issued", ended.QuerySelector("a")!.GetAttribute("href"));
        Assert.Contains("tech1; ended by super1", ended.TextContent);

        var save = page.Find("[data-outage-save]");
        Assert.True(save.HasAttribute("disabled"));
        page.Find("[data-outage-start]").Change(DateTime.Now.AddHours(-30).ToString("yyyy-MM-ddTHH:mm:00"));
        page.Find("[data-outage-description]").Input("Planned maintenance");
        Assert.False(page.Find("[data-outage-save]").HasAttribute("disabled"));

        page.Find("[data-outage-end]").Click();
        page.WaitForAssertion(() => Assert.Contains("Outage #2 ended", page.Markup));
    }

    // TEST-BB-848: ticking back-entry offers the open outage and fills the reason from it; a paper time in the earlier
    // outage switches to that outage's reason, and a time outside both warns to check the record.
    [Fact]
    public void BackEntry_UsesTheRecordedOutage()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/compliance/downtime-episodes"] = Episodes()
        });
        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.Trim() == "Issue").Click();
        var backEntry = page.FindAll("label").First(l => l.TextContent.Contains("Downtime back-entry")).QuerySelector("input")!;
        backEntry.Change(true);

        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-backentry-outage='2']")));
        Assert.Equal("Issued on paper during downtime #2: LIS upgrade", page.Find("[data-backentry-reason]").GetAttribute("value"));

        page.Find("[data-backentry-time]").Change(Now.AddDays(-2).AddHours(1).ToLocalTime().ToString("yyyy-MM-ddTHH:mm:00"));
        Assert.NotNull(page.Find("[data-backentry-outage='1']"));
        Assert.Equal("Issued on paper during downtime #1: Network outage", page.Find("[data-backentry-reason]").GetAttribute("value"));
        Assert.Empty(page.FindAll("[data-backentry-outside]"));

        page.Find("[data-backentry-time]").Change(Now.AddDays(-1).ToLocalTime().ToString("yyyy-MM-ddTHH:mm:00"));
        Assert.Contains("outside every recorded outage", page.Find("[data-backentry-outside]").TextContent);
    }
}
