using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// End-user testing pass 280: Demo, Patricia's Stat crossmatch showed a "Blocked" button on the chart's Tests tab and the
/// Test Worklist, with why only in the button's hover text, which a touch screen never shows. The reason is now in view.
/// </summary>
public class BlockReasonVisibleTests : BunitContext
{
    // TEST-BB-1306: a blocked worklist line shows why under its button.
    [Fact]
    public void BlockedLine_ShowsWhy()
    {
        const string reason = "No specimen is linked to this order. Accession a specimen and link it before entering results.";
        var item = new TestWorkItemDto(1, 1, 1, "Demo, Patricia", "MRN0001", "XM", "Crossmatch", "ORD0002", OrderPriority.Stat, DateTime.UtcNow.AddDays(-12),
            null, null, null, null, false, "O+", false, null, null, null, null, null, null, false, reason);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/test-worklist/pending"] = new List<TestWorkItemDto> { item }
        });

        var page = Render<TestWorklist>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-block-reason]")));

        Assert.Equal(reason, page.Find("[data-block-reason]").TextContent.Trim());
    }
}
