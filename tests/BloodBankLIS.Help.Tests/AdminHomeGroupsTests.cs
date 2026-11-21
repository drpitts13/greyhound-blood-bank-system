using BloodBankLIS.Web.Components.Pages.Admin;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>Admin Home groups its cards like the Administration menu (end-user testing loop 2 tick 31).</summary>
public class AdminHomeGroupsTests : BunitContext
{
    // TEST-BB-1408: the cards sit under the menu's task groups in the same order, test setup and product setup each in one
    // group, and every card is in a group.
    [Fact]
    public void Cards_AreGroupedLikeTheMenu()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var page = Render<AdminHome>();

        var groups = page.FindAll("[data-admin-home-group] > h2").Select(h => h.TextContent.Trim()).ToList();
        Assert.Equal(["Tests & specimens", "Products & inventory", "Transfusion & safety", "Ordering", "Billing", "Interfaces & printing", "Directory, audit & reports"], groups);
        Assert.Equal(page.FindAll("a.admin-card").Count, page.FindAll("[data-admin-home-group] a.admin-card").Count);

        string GroupOf(string href) => page.FindAll("[data-admin-home-group]")
            .Single(s => s.QuerySelector($"a.admin-card[href='{href}']") is not null).GetAttribute("data-admin-home-group")!;
        Assert.All(["/admin/tests", "/admin/subtests", "/admin/phases", "/admin/reflex-rules"], h => Assert.Equal("Tests & specimens", GroupOf(h)));
        Assert.All(["/admin/products", "/admin/modification-rules", "/admin/expiration-codes", "/admin/isbt-product-codes"], h => Assert.Equal("Products & inventory", GroupOf(h)));
    }
}
