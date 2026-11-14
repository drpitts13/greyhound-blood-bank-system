using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Web.Components.Pages;
using BloodBankLIS.Web.Components.Shared;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// The chart opens Compatibility for a patient with no unit; scanning the unit to crossmatch is the next step, but the
/// cursor was not in Unit number (Cycle 649).
/// </summary>
public class CompatibilityScanFocusTests : BunitContext
{
    private IRenderedComponent<Compatibility> Open(string url)
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        return Render<Compatibility>();
    }

    private static string? InputId(IRenderedComponent<FilterableComboBox<BloodUnitDto, long?>> box) =>
        ((ElementReference)typeof(FilterableComboBox<BloodUnitDto, long?>)
            .GetField("_input", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(box.Instance)!).Id;

    private int Focuses(string? id = null) => JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)
        && (id is null || i.Arguments.OfType<ElementReference>().Any(r => r.Id == id)));

    // TEST-BB-800: opened for a patient with no unit, the crossmatch Unit number takes the cursor once.
    [Fact]
    public void OpenedForAPatient_CrossmatchUnitTakesTheCursor()
    {
        var page = Open("/compatibility?patientId=5");
        var crossmatchUnit = page.FindComponents<FilterableComboBox<BloodUnitDto, long?>>().First();
        var id = InputId(crossmatchUnit);
        page.WaitForAssertion(() => Assert.Equal(1, Focuses(id)));
        page.Render();
        Assert.Equal(1, Focuses(id));
    }

    // TEST-BB-801: opened bare from the menu, or with the unit already chosen (a return or retrospective follow-up),
    // nothing takes the cursor.
    [Theory]
    [InlineData("/compatibility")]
    [InlineData("/compatibility?patientId=5&bloodUnitId=9&from=return")]
    public void OpenedBareOrWithAUnit_NothingTakesTheCursor(string url)
    {
        var page = Open(url);
        page.Render();
        Assert.Equal(0, Focuses());
    }
}
