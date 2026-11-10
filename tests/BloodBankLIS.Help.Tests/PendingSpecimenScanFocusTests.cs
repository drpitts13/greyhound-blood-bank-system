using BloodBankLIS.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Pending specimens works by scanning, but opening it or changing Receive/Accept left the cursor elsewhere, so the next
/// tube was scanned into the page or a radio button (Cycle 637).
/// </summary>
public class PendingSpecimenScanFocusTests : BunitContext
{
    private string? ScanInputId(IRenderedComponent<TestWorklist> page) =>
        ((ElementReference)typeof(TestWorklist).GetField("_specimenScanInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(page.Instance)!).Id;

    private int Focuses(string? id) => JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)
        && i.Arguments.OfType<ElementReference>().Any(r => r.Id == id));

    // TEST-BB-773: opening Pending specimens puts the cursor in the scan field, and so does changing to Accept.
    [Fact]
    public void ScanField_TakesTheFocus_OnOpenAndModeChange()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var page = Render<TestWorklist>();
        page.FindAll("button").First(b => b.TextContent.Trim() == "Specimens").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-specimen-scan]")));
        var id = ScanInputId(page);
        page.WaitForAssertion(() => Assert.Equal(1, Focuses(id)));

        page.Find("[data-scan-mode='accept']").Change(true);
        page.WaitForAssertion(() => Assert.Equal(2, Focuses(id)));
        Assert.Contains("Scan tube to accept", page.Markup);
    }
}
