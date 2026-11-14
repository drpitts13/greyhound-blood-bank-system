using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Seven reagent lots overdue for QC were seven rows, burying the other alerts; alerts of one kind raised together are
/// now one row naming each, acknowledged together, each still recorded (Cycle 656).
/// </summary>
public class AlertGroupRowTests : BunitContext
{
    private static readonly DateTime Raised = DateTime.UtcNow.AddHours(-2);

    private static AlertDto Lot(long id, string lot) =>
        new(id, AlertRule.ReagentQc, AlertSeverity.Warning, $"Reagent QC overdue: lot {lot}", $"Lot {lot} has no QC for today.",
            $"/reagents?lot={lot}", Raised, null, null, null, null);

    private IRenderedComponent<Alerts> Open(bool failOne = false)
    {
        var alerts = new List<AlertDto>
        {
            Lot(1, "A1"), Lot(2, "B2"), Lot(3, "C3"),
            new(9, AlertRule.MtpStillOpen, AlertSeverity.Warning, "Massive transfusion protocol still open: MRN MRN0021", "MTP #1.", null, Raised, null, null, null, null)
        };
        var responses = new Dictionary<string, object>
        {
            ["/api/alerts"] = alerts,
            ["/api/alerts/1/acknowledge"] = alerts[0],
            ["/api/alerts/3/acknowledge"] = alerts[2]
        };
        if (!failOne)
        {
            responses["/api/alerts/2/acknowledge"] = alerts[1];
        }

        CannedApi.Register(this, responses);
        var page = Render<Alerts>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-alert-group]")));
        return page;
    }

    // TEST-BB-814: the three lots are one row naming each lot (linked), with Acknowledge all (3); the other alert keeps
    // its own row; Show each lists the three rows and Hide each folds them again.
    [Fact]
    public void LotsRaisedTogether_AreOneRow()
    {
        var page = Open();
        var group = page.Find("[data-alert-group]");
        Assert.Contains("Reagent QC overdue (3)", group.TextContent);
        Assert.Equal(["lot A1", "lot B2", "lot C3"], group.QuerySelectorAll("[data-alert-group-items] a").Select(a => a.TextContent));
        Assert.Equal("/reagents?lot=B2", group.QuerySelectorAll("[data-alert-group-items] a")[1].GetAttribute("href"));
        Assert.Contains(page.FindAll("button"), b => b.TextContent.Trim() == "Acknowledge all (3)");
        Assert.Contains("Massive transfusion protocol still open: MRN MRN0021", page.Markup);
        Assert.DoesNotContain("Lot B2 has no QC", page.Markup);

        group.QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Show each").Click();
        Assert.Contains("Lot B2 has no QC", page.Markup);
        page.Find("[data-alert-group]").QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Hide each").Click();
        Assert.DoesNotContain("Lot B2 has no QC", page.Markup);
    }

    // TEST-BB-815: Acknowledge all acknowledges each alert; when one cannot be acknowledged the page says how many were
    // and names the one that was not.
    [Theory]
    [InlineData(false, "3 alerts acknowledged.")]
    [InlineData(true, "2 of 3 acknowledged; not acknowledged: Reagent QC overdue: lot B2.")]
    public void AcknowledgeAll_AcknowledgesEach_AndSaysWhichFailed(bool failOne, string expected)
    {
        var page = Open(failOne);
        page.FindAll("button").First(b => b.TextContent.Trim() == "Acknowledge all (3)").Click();
        page.WaitForAssertion(() => Assert.Contains(expected, page.Markup));
    }
}
