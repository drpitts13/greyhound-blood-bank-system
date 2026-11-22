using System.Text.RegularExpressions;
using BloodBankLIS.Web.Components.Layout;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>The Administration menu is grouped by task and reaches every admin page (end-user testing loop 2 tick 4).</summary>
public partial class AdminNavMenuTests : BunitContext
{
    /// <summary>Admin routes that are not menu pages: a redirect and pages opened from another page.</summary>
    private static readonly HashSet<string> NotInMenu = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin/specimen-label", "admin/hl7/translations", "admin/reports/design"
    };

    // TEST-BB-1386: the groups are Tests & specimens, Products & inventory, Transfusion & safety, Ordering, Billing,
    // Interfaces & printing, and Directory & audit; every /admin page with its own route is linked (Refrigerators &
    // Freezers was reachable only from other pages); test setup and product setup each sit in one group.
    [Fact]
    public void AdminMenu_IsGroupedByTask_AndLinksEveryAdminPage()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var menu = Render<NavMenu>();
        var groups = menu.FindAll("details.nav-group > summary").Select(s => s.TextContent.Trim()).ToList();
        Assert.Equal(["Tests & specimens", "Products & inventory", "Transfusion & safety", "Ordering", "Billing", "Interfaces & printing", "Directory & audit"], groups);

        var links = menu.FindAll("a[href^='admin']").Select(a => a.GetAttribute("href")!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pagesDir = Path.Combine(RepoRoot(), "src", "BloodBankLIS.Web", "Components", "Pages", "Admin");
        var routes = Directory.GetFiles(pagesDir, "*.razor")
            .SelectMany(f => RouteRegex().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Where(r => !r.Contains('{') && r != "admin" && !NotInMenu.Contains(r))
            .ToList();
        Assert.NotEmpty(routes);
        Assert.All(routes, r => Assert.Contains(r, links));

        string GroupOf(string href) => menu.FindAll("details.nav-group")
            .Single(d => d.QuerySelector($"a[href='{href}']") is not null).QuerySelector("summary")!.TextContent.Trim();
        Assert.All(new[] { "admin/tests", "admin/subtests", "admin/phases", "admin/reflex-rules", "admin/specimen-types" },
            h => Assert.Equal("Tests & specimens", GroupOf(h)));
        Assert.All(new[] { "admin/products", "admin/isbt-product-codes", "admin/modification-rules", "admin/expiration-codes" },
            h => Assert.Equal("Products & inventory", GroupOf(h)));
    }

    [GeneratedRegex("@page \"/(admin[^\"]*)\"")]
    private static partial Regex RouteRegex();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
