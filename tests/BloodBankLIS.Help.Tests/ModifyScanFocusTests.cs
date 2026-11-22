using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Web.Components.Pages;
using BloodBankLIS.Web.Components.Shared;
using Bunit;
using BloodBankLIS.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Modify Products is scan-first, but opened from the menu the cursor was nowhere, so the first scanned unit was lost
/// (Cycle 648).
/// </summary>
public class ModifyScanFocusTests : BunitContext
{
    private string? InputId(IRenderedComponent<FilterableComboBox<BloodUnitDto, long?>> box) =>
        ((ElementReference)typeof(FilterableComboBox<BloodUnitDto, long?>)
            .GetField("_input", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(box.Instance)!).Id;

    private int Focuses(string? id) => JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)
        && i.Arguments.OfType<ElementReference>().Any(r => r.Id == id));

    // TEST-BB-797: opened without a unit, the source unit picker takes the cursor once, without dropping its list open;
    // later renders do not take it again.
    [Fact]
    public void OpenedWithoutAUnit_SourcePickerTakesTheCursor()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var page = Render<Modifications>();
        var box = page.FindComponent<FilterableComboBox<BloodUnitDto, long?>>();
        var id = InputId(box);
        page.WaitForAssertion(() => Assert.Equal(1, Focuses(id)));
        Assert.Empty(page.FindAll(".bb-combobox-menu"));

        page.Render();
        Assert.Equal(1, Focuses(id));
    }

    // TEST-BB-799: the autofocus must not swallow the user's own click: a focus arriving after the brief quiet window
    // (the autofocus's own focus event may never arrive, as in a background tab) opens the list as usual.
    [Fact]
    public async Task LaterFocus_OpensTheList()
    {
        var unit = new BloodUnitDto(7, "W000000000001", null, null, null, null, null, null, null, null, null, false, 1,
            AboGroup.O, RhType.Positive, "O+", DateTime.UtcNow.AddDays(10), UnitStatus.Available, null,
            ComponentEntrySource.Manual, DateTime.UtcNow, "tech1");
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/inventory/units"] = new List<BloodUnitDto> { unit }
        });
        var page = Render<Modifications>();
        var box = page.FindComponent<FilterableComboBox<BloodUnitDto, long?>>();
        page.WaitForAssertion(() => Assert.Equal(1, Focuses(InputId(box))));

        await Task.Delay(700);
        await page.Find(".bb-combobox-input").FocusAsync(new Microsoft.AspNetCore.Components.Web.FocusEventArgs());
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".bb-combobox-menu")));
    }

    // TEST-BB-798: opened on a unit (from the unit page or the chart), the picker leaves the cursor alone.
    [Fact]
    public void OpenedOnAUnit_PickerDoesNotTakeTheCursor()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var nav = Services.GetService(typeof(NavigationManager)) as NavigationManager;
        nav!.NavigateTo("/inventory/modify?unitId=42");
        var page = Render<Modifications>();
        var box = page.FindComponent<FilterableComboBox<BloodUnitDto, long?>>();
        var id = InputId(box);
        page.Render();
        Assert.Equal(0, Focuses(id));
    }
}
