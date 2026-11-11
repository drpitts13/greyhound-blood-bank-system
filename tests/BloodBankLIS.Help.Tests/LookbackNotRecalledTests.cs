using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>After a recall, Lookback names the components left as they were and why (Cycle 687).</summary>
public class LookbackNotRecalledTests : BunitContext
{
    // TEST-BB-913: a recall whose report names components not recalled shows them, each linked to its unit with the
    // reason; the recalled component stays in the components table.
    [Fact]
    public void Recall_NamesTheComponentsNotRecalled()
    {
        const string din = "W000058585858";
        var report = new LookbackReportDto(din,
            [new LookbackUnitDto(1, din, "U-A", UnitStatus.Recalled, false), new LookbackUnitDto(2, din, "U-B", UnitStatus.ReturnedToSupplier, false)],
            [], [],
            [new LookbackNotRecalledDto(2, "U-B", UnitStatus.ReturnedToSupplier, "already returned to the supplier; it is not here to recall")]);
        CannedApi.Register(this, new Dictionary<string, object> { [$"/api/lookback/{din}/recall"] = report });
        var page = Render<Lookback>();

        page.Find("input[placeholder='W0000X1234567']").Input(din);
        page.Find("input[placeholder='Required to recall']").Input("Donor subsequently reactive");
        page.FindAll("button").First(b => b.TextContent.Trim() == "Recall all components").Click();
        page.FindAll("button").First(b => b.TextContent.Trim() == "Yes, recall").Click();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-not-recalled]")));
        var alert = page.Find("[data-not-recalled]");
        Assert.Contains("1 component was not recalled", alert.TextContent);
        Assert.Contains("U-B", alert.TextContent);
        Assert.Contains("already returned to the supplier", alert.TextContent);
        Assert.Equal("/inventory/units/2", alert.QuerySelector("a")!.GetAttribute("href"));
    }
}
