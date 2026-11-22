using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// The Retype board worked by scanning, rendered: a scan opens the unit's entry, and once the retype is recorded the
/// scan field takes the focus for the next label (Cycle 610), so a box of units is worked without the mouse.
/// </summary>
public class RetypeBoardFlowTests : BunitContext
{
    // TEST-BB-723: scan, record, and the focus returns to the scan field.
    [Fact]
    public void AfterARetypeIsRecorded_TheScanFieldTakesTheFocus()
    {
        var pending = new ProductRetypeWorkItemDto(7, "W0001230000007", "RBC-LR", "Red Blood Cells", AboGroup.O, RhType.Positive,
            "O+", DateTime.UtcNow.AddHours(-1), UnitStatus.Received);
        var detail = new ProductRetypeDetailDto(
            7, "W0001230000007", "RBC-LR", "Red Blood Cells", true, AboGroup.O, RhType.Positive, "O+",
            UnitStatus.Received, CanRecord: true, CanVerify: false, BlockReason: null, AntiDRequired: true,
            [new ProductRetypeSubtestDto("ANTI-A", "Anti-A", true), new ProductRetypeSubtestDto("ANTI-B", "Anti-B", true),
             new ProductRetypeSubtestDto("ANTI-D", "Anti-D", true)],
            ["0", "1+", "2+", "3+", "4+"], Latest: null);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/inventory/retypes/pending"] = new List<ProductRetypeWorkItemDto> { pending },
            ["/api/inventory/units/7/retype"] = detail
        });

        var page = Render<Inventory>();
        page.FindAll("button").First(b => b.TextContent.Trim() == "Retype").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-retype-scan]")));

        page.Find("[data-retype-scan]").Input("W0001230000007");
        page.Find("[data-retype-scan]").KeyDown("Enter");
        page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), b => b.TextContent.Trim() == "Record retype"));

        List<AngleSharp.Dom.IElement> FormSelects() =>
            page.FindAll("button").First(b => b.TextContent.Trim() == "Record retype").ParentElement!.QuerySelectorAll("select").ToList();
        Assert.Equal(5, FormSelects().Count);
        var values = new[] { "O", "Positive", "0", "0", "3+" };
        for (var k = 0; k < values.Length; k++)
        {
            FormSelects()[k].Change(values[k]);
        }

        var focusesBefore = JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));
        page.FindAll("button").First(b => b.TextContent.Trim() == "Record retype").Click();

        page.WaitForAssertion(() => Assert.True(
            JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)) > focusesBefore,
            "The scan field did not take the focus after the retype was recorded."));
    }
}
