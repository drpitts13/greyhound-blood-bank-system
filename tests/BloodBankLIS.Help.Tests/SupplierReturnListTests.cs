using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>A return to the supplier has its own return list, printed again from Recent returns (Cycle 686).</summary>
public class SupplierReturnListTests : BunitContext
{
    private static readonly DateTime Returned = DateTime.UtcNow.AddHours(-1);

    private static BloodUnitDto Unit(string number) => BloodUnitDto.From(new BloodUnit
    {
        UnitNumber = number, Abo = AboGroup.A, RhD = RhType.Positive, ExpiresUtc = DateTime.UtcNow.AddDays(10),
        Status = UnitStatus.ReturnedToSupplier, Supplier = "Regional Blood Center", SupplierReturnReason = "Recall R-114"
    });

    private static SupplierReturnDto Return(string reason, params string[] units) => new(
        SupplierReturnListRule.ReturnKey(reason, Returned), reason, Returned, Returned, ["tech1"], ["Regional Blood Center"],
        units.Select(Unit).ToList());

    private IRenderedComponent<SupplierReturnList> Open(string? key, params SupplierReturnDto[] returns)
    {
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/inventory/supplier-returns"] = returns.ToList() });
        Services.GetRequiredService<NavigationManager>().NavigateTo(
            key is null ? "/inventory/return-to-supplier/list" : $"/inventory/return-to-supplier/list?key={Uri.EscapeDataString(key)}");
        return Render<SupplierReturnList>();
    }

    // TEST-BB-907: the list shows only the named return: its reason, who returned it, the supplier and its units, with
    // blanks for the courier or supplier.
    [Fact]
    public void ReturnList_ShowsTheNamedReturn()
    {
        var page = Open(SupplierReturnListRule.ReturnKey("Recall R-114", Returned),
            Return("Short-dated", "U-OTHER"), Return("Recall R-114", "U-1", "U-2"));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-return-sheet]")));

        var sheet = page.Find("[data-return-sheet]").TextContent;
        Assert.Contains("Recall R-114", page.Find("[data-return-reason-text]").TextContent);
        Assert.Contains("Regional Blood Center", page.Find("[data-return-suppliers]").TextContent);
        Assert.Contains("tech1", sheet);
        Assert.Equal(2, page.FindAll("[data-return-units] tbody tr").Count);
        Assert.DoesNotContain("U-OTHER", sheet);
        Assert.Contains("Courier or supplier", sheet);
    }

    // TEST-BB-908: an unknown key says no return was found and offers no Print; no key says to open it from Return to
    // supplier.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReturnList_NoMatch(bool unknownKey)
    {
        var page = Open(unknownKey ? "NOTHING|2026-01-01" : null, Return("Recall R-114", "U-1"));
        if (unknownKey)
        {
            page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-return-not-found]")));
        }
        else
        {
            page.WaitForAssertion(() => Assert.Contains("no return was named", page.Markup));
        }

        Assert.Empty(page.FindAll("[data-return-print]"));
        Assert.Empty(page.FindAll("[data-return-sheet]"));
    }

    // TEST-BB-909: Return to supplier lists recent returns, each linking to its own return list, and no longer prints the page.
    [Fact]
    public void ReturnToSupplier_RecentReturns_LinkToTheirList()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/inventory/supplier-returns"] = new List<SupplierReturnDto> { Return("Recall R-114", "U-1", "U-2") }
        });
        var page = Render<InventoryReturnToSupplier>();
        page.WaitForAssertion(() => Assert.Contains("Recall R-114", page.Find("[data-recent-returns]").TextContent));

        Assert.Equal("/inventory/return-to-supplier/list?key=" + Uri.EscapeDataString(SupplierReturnListRule.ReturnKey("Recall R-114", Returned)),
            page.Find("[data-return-list]").GetAttribute("href"));
        Assert.Empty(page.FindAll("[onclick='window.print()']"));
    }
}
