using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Ward receipt, Return, and Transfusion start from a scanned unit, but opening one left the cursor elsewhere, so the
/// first scan went nowhere (Cycle 622). Opened from the desk button, the Unit number field takes the focus; opened from a
/// row, the unit is already filled and the focus is left alone.
/// </summary>
public class ScanDeskFocusTests : BunitContext
{
    private int FocusesOn(IRenderedComponent<Issuing> page, string desk)
    {
        var id = page.Find($"[data-desk-unit='{desk}']").GetAttribute("blazor:elementReference");
        return JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)
            && i.Arguments.OfType<ElementReference>().Any(r => r.Id == id));
    }

    // TEST-BB-744: each scan desk opened from its button puts the cursor in Unit number.
    [Theory]
    [InlineData("Ward receipt", "receipt")]
    [InlineData("Return", "return")]
    [InlineData("Transfusion", "transfuse")]
    public void DeskButton_FocusesUnitNumber(string button, string desk)
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.Trim() == button).Click();

        page.WaitForAssertion(() => Assert.Equal(1, FocusesOn(page, desk)));
    }

    // TEST-BB-745: Return opened from an Issued units row fills the unit and does not take the focus to Unit number.
    [Fact]
    public void ReturnFromARow_LeavesTheFocusAlone()
    {
        var issued = DateTime.UtcNow.AddHours(-2);
        var outstanding = new OutstandingIssueWorkItemDto(
            41, 5, "MRN0022", "Demo-Multi, Riley", "1980-01-01", 9, "W0001230009205", "O+", false, null,
            null, false, issued, null, "nurse1", null, "4W", IssueStatus.Issued, ProductName: "Red Blood Cells");
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/outstanding"] = new List<OutstandingIssueWorkItemDto> { outstanding }
        });

        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.StartsWith("Issued units")).Click();
        AngleSharp.Dom.IElement RowReturn() => page.FindAll("table.bb tbody tr")
            .First(r => r.TextContent.Contains("W0001230009205"))
            .QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Return");
        page.WaitForAssertion(() => Assert.NotNull(RowReturn()));
        RowReturn().Click();

        page.WaitForAssertion(() => Assert.Equal("W0001230009205", page.Find("[data-desk-unit='return']").GetAttribute("value")));
        Assert.Equal(0, FocusesOn(page, "return"));
    }
}
