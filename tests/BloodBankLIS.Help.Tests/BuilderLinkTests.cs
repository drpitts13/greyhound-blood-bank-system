using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

/// <summary>Builder pages link to the pages they are built from (end-user testing loop 2 tick 17).</summary>
public partial class BuilderLinkTests
{
    // TEST-BB-1399: Tests links to Subtests and Phases (a panel is built from them), Products to Modification Rules and
    // ISBT Product Codes, and Modification Rules back to Products and Expiration Codes.
    [Theory]
    [InlineData("AdminTests.razor", "subtests", "phases", "specimen-types")]
    [InlineData("AdminProducts.razor", "modification-rules", "isbt-product-codes")]
    [InlineData("AdminModificationRules.razor", "products", "expiration-codes")]
    public void BuilderPage_LinksItsBuildingBlocks(string page, params string[] targets)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var text = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Pages", "Admin", page));
        var links = HeaderLink().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();
        Assert.All(targets, t => Assert.Contains(t, links));
    }

    [GeneratedRegex("class=\"btn btn-ghost\" href=\"/admin/([a-z-]+)\"")]
    private static partial Regex HeaderLink();
}
