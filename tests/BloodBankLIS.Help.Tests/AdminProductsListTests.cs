using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages.Admin;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>The product list reads in words and shows each product's ISBT code (end-user testing loop 2 tick 5).</summary>
public class AdminProductsListTests : BunitContext
{
    private static ProductDefinitionDto Product(long id, string code, ComponentClass cls, bool retype, string? isbt) => new(
        id, code, code + " name", cls, null, null, retype, retype, retype, retype, isbt, null, null, null, null, null, 1, true, [],
        RhPositiveRetypeTestCode: retype ? "RT-POS" : null, RhNegativeRetypeTestCode: null);

    // TEST-BB-1387: the class reads as its label, retype reads Yes/No with the retype tests beneath (one not set says so),
    // and the ISBT code shows, or a dash when none is set.
    [Fact]
    public void ProductList_ReadsInWords()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/products"] = new List<ProductDefinitionDto>
            {
                Product(1, "RBC-LR", ComponentClass.RedBloodCells, true, "E0336"),
                Product(2, "FFP", ComponentClass.Plasma, false, null)
            }
        });
        var page = Render<AdminProducts>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-product-class='RBC-LR']")));

        Assert.Equal(BloodBankLIS.Domain.Rules.ComponentClassLabels.Label(ComponentClass.RedBloodCells), page.Find("[data-product-class='RBC-LR']").TextContent.Trim());
        Assert.DoesNotContain("RedBloodCells", page.Find("[data-product-class='RBC-LR']").TextContent);
        Assert.Equal("E0336", page.Find("[data-product-isbt='RBC-LR']").TextContent.Trim());
        Assert.Equal("—", page.Find("[data-product-isbt='FFP']").TextContent.Trim());
        var retype = page.Find("[data-product-retype='RBC-LR']").TextContent;
        Assert.Contains("Yes", retype);
        Assert.Contains("RT-POS · not set", retype);
        Assert.Equal("No", page.Find("[data-product-retype='FFP']").TextContent.Trim());
    }
}
