using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Components.Pages;
using BloodBankLIS.Web.Services;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>The HL7 page lists outbound messages that have not gone, with why, and calls a queued outbound message queued (Cycle 660).</summary>
public class Hl7OutboundWaitingPageTests : BunitContext
{
    // TEST-BB-840: messages waiting to send are listed with their reason and a Send button, the stuck one's wait marked;
    // in the log an outbound message not yet sent reads Queued, and an inbound one still reads Received.
    [Fact]
    public void WaitingMessages_AreListedWithWhy()
    {
        var now = DateTime.UtcNow;
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/hl7/outbound/waiting"] = new List<Hl7OutboundWaitingVm>
            {
                new(11, "DFT^P03", "DFT-1", now.AddDays(-3), 0, 5, OutboundMessageWaitRule.NoInterface, true),
                new(12, "ORU^R01", "ORU-1", now.AddMinutes(-2), 0, 5, "Queued; the next send attempt is within a minute.", false)
            },
            ["/api/hl7/messages"] = new List<Hl7MessageVm>
            {
                new(11, Hl7Direction.Outbound, "DFT", "P03", "DFT-1", Hl7MessageStatus.Received, now.AddDays(-3), null, null, null),
                new(3, Hl7Direction.Inbound, "ORM", "O01", "ORM-1", Hl7MessageStatus.Received, now.AddDays(-1), null, null, null)
            }
        });
        var page = Render<Hl7>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-outbound-waiting]")));

        var stuck = page.Find("[data-waiting='11']");
        Assert.Contains(OutboundMessageWaitRule.NoInterface, stuck.TextContent);
        Assert.Contains("3 d", stuck.QuerySelector(".badge")!.TextContent);
        Assert.Contains(stuck.QuerySelectorAll("button"), b => b.TextContent.Trim() == "Send");
        Assert.Null(page.Find("[data-waiting='12']").QuerySelector(".badge"));

        var rows = page.FindAll("table.bb tbody tr").Select(r => r.TextContent).ToList();
        Assert.Contains(rows, r => r.Contains("DFT-1") && r.Contains("Queued"));
        Assert.Contains(rows, r => r.Contains("ORM-1") && r.Contains("Received"));
    }
}
