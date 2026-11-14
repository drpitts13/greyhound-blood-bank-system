using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// A unit's ABO retype, rendered: the form took its interpretation from the label it is meant to confirm and started
/// every reading at "0", so a retype saved untouched "confirmed" the label with nothing read (Cycle 592).
/// </summary>
public class ProductRetypeEntryPanelTests : BunitContext
{
    private static ProductRetypeDetailDto Detail() => new(
        7, "W0001230000007", "E0336", "Red Blood Cells", true, AboGroup.A, RhType.Positive, "A+",
        UnitStatus.Received, CanRecord: true, CanVerify: false, BlockReason: null, AntiDRequired: true,
        [new ProductRetypeSubtestDto("ANTI-A", "Anti-A", true), new ProductRetypeSubtestDto("ANTI-B", "Anti-B", true),
         new ProductRetypeSubtestDto("ANTI-D", "Anti-D", true), new ProductRetypeSubtestDto("CTL", "Control", false)],
        ["0", "1+", "2+", "3+", "4+"], Latest: null);

    private IRenderedComponent<ProductRetypeEntryPanel> RenderPanel()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        return Render<ProductRetypeEntryPanel>(p => p.Add(x => x.Detail, Detail()));
    }

    // TEST-BB-690: the interpretation is not taken from the label, and required readings start blank.
    [Fact]
    public void Retype_StartsUnchosen_NotFromTheLabel()
    {
        var panel = RenderPanel();
        var selects = panel.FindAll("select");

        // ABO, Rh(D), then the four readings.
        Assert.Equal(6, selects.Count);
        foreach (var select in selects.Take(5))
        {
            Assert.True(string.IsNullOrEmpty(select.GetAttribute("value")), select.OuterHtml);
        }

        Assert.Equal("NT", selects[5].GetAttribute("value"));
    }

    // TEST-BB-691: recording with readings missing names them; with the readings in and no type chosen, it asks for
    // the type. Nothing is sent either time.
    [Fact]
    public void Retype_WithReadingsOrTypeMissing_IsRefused()
    {
        var panel = RenderPanel();
        panel.FindAll("select")[2].Change("4+");
        panel.Find("button").Click();
        panel.WaitForAssertion(() => Assert.Contains("Enter a reading for Anti-B, Anti-D before saving.", panel.Markup));

        panel.FindAll("select")[3].Change("0");
        panel.FindAll("select")[4].Change("3+");
        panel.Find("button").Click();
        panel.WaitForAssertion(() => Assert.Contains("Choose the interpreted ABO and Rh(D).", panel.Markup));
    }
}
