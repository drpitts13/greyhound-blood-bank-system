using BloodBankLIS.Application.Admin;
using BloodBankLIS.Web.Components.Pages.Admin;
using BloodBankLIS.Web.Services;
using Bunit;

namespace BloodBankLIS.Help.Tests;

public class Hl7SetupEmptyTests : BunitContext
{
    // TEST-BB-1264: with no interface configured, Interface Setup says what that leaves undone and how many outbound
    // messages are waiting for one, linked to the HL7 page (end-user testing pass 240).
    [Fact]
    public void NoInterfaces_SaysWhatIsWaiting()
    {
        var now = DateTime.UtcNow;
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/hl7/endpoints"] = new List<Hl7EndpointDto>(),
            ["/api/hl7/outbound/waiting"] = new List<Hl7OutboundWaitingVm>
            {
                new(1, "DFT^P03", "C1", now.AddDays(-3), 0, 5, "No enabled outbound interface is set up to send it. It sends once one is.", true),
                new(2, "DFT^P03", "C2", now.AddDays(-2), 0, 5, "No enabled outbound interface is set up to send it. It sends once one is.", true)
            }
        });
        var page = Render<AdminHl7Endpoints>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-outbound-waiting-count]")));

        Assert.Contains("outbound results to the EHR and charges to billing wait", System.Text.RegularExpressions.Regex.Replace(page.Find("[data-no-interfaces]").TextContent, @"\s+", " "));
        Assert.Equal("2 outbound messages are waiting.", page.Find("[data-outbound-waiting-count]").TextContent.Trim());
        Assert.Equal("/hl7", page.Find("[data-outbound-waiting-count]").GetAttribute("href"));
    }
}
