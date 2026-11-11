using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>Inventory's Recalled board lists recalled units not yet pulled (Cycle 688).</summary>
public class InventoryRecalledBoardTests : BunitContext
{
    // TEST-BB-916: opened from the home card, the board lists each recalled unit with its reason and who recalled it, and
    // marks one that was issued as on the ward; with none it says so.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecalledBoard_ListsUnitsToPull(bool any)
    {
        var now = DateTime.UtcNow;
        var rows = any
            ? new List<RecalledWorkItemDto>
            {
                new(1, "W000000000011", AboGroup.O, RhType.Positive, now.AddDays(10), "RBC-LR", null, "Fridge 1", "Donor reactive", now.AddHours(-2), "tech1", UnitStatus.Available),
                new(2, "W000000000012", AboGroup.O, RhType.Positive, now.AddDays(10), "RBC-LR", null, "Fridge 1", "Donor reactive", now.AddHours(-2), "tech1", UnitStatus.Issued)
            }
            : new List<RecalledWorkItemDto>();
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/inventory/units/recalled"] = rows });
        Services.GetRequiredService<NavigationManager>().NavigateTo("/inventory?board=recalled");

        var page = Render<Inventory>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-recalled-board]")));
        if (any)
        {
            Assert.Contains("Recalled (2)", page.Find("[data-board-recalled]").TextContent);
            var shelf = page.Find("[data-recalled-row='1']").TextContent;
            Assert.Contains("Donor reactive", shelf);
            Assert.Contains("by tech1", shelf);
            Assert.Empty(page.Find("[data-recalled-row='1']").QuerySelectorAll("[data-recalled-on-ward]"));
            Assert.NotNull(page.Find("[data-recalled-row='2'] [data-recalled-on-ward]"));
        }
        else
        {
            Assert.Contains("No recalled units waiting to be pulled", page.Find("[data-recalled-board]").TextContent);
            Assert.Empty(page.FindAll("[data-recalled-scan]"));
        }
    }

    // TEST-BB-920 (Cycle 690): scanning a found recalled unit and pressing Enter quarantines it and says so; a scan the
    // server refuses shows its reason.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecalledBoard_ScanQuarantinesTheFoundUnit(bool accepted)
    {
        var now = DateTime.UtcNow;
        var responses = new Dictionary<string, object>
        {
            ["/api/inventory/units/recalled"] = new List<RecalledWorkItemDto>
            {
                new(1, "W000000000011", AboGroup.O, RhType.Positive, now.AddDays(10), "RBC-LR", null, "Fridge 1", "Donor reactive", now.AddHours(-2), "tech1", UnitStatus.Available)
            }
        };
        if (accepted)
        {
            responses["/api/inventory/units/pull-recalled-by-scan"] = BloodUnitDto.From(new BloodBankLIS.Domain.Entities.BloodUnit
            {
                Id = 1, UnitNumber = "W000000000011", Status = UnitStatus.Quarantine
            });
        }

        CannedApi.Register(this, responses);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/inventory?board=recalled");
        var page = Render<Inventory>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-recalled-scan]")));

        page.Find("[data-recalled-scan]").Input("W000000000011");
        page.Find("[data-recalled-scan]").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        if (accepted)
        {
            page.WaitForAssertion(() => Assert.Contains("W000000000011 quarantined for the recall", page.Markup));
        }
        else
        {
            page.WaitForAssertion(() => Assert.DoesNotContain("W000000000011 quarantined", page.Markup));
            Assert.NotNull(page.Find("[data-recalled-row='1']"));
        }

        Assert.Equal(string.Empty, page.Find("[data-recalled-scan]").GetAttribute("value") ?? string.Empty);
    }
}
