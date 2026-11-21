using BloodBankLIS.Application.Admin;
using BloodBankLIS.Web.Components.Pages.Admin;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>One licensed product code can be added without building an extract (end-user testing loop 2 tick 14).</summary>
public class IsbtOneCodeTests : BunitContext
{
    // TEST-BB-1396: the import dialog's Add one product code needs a code and a description, then imports that one row
    // under the dialog's version, acknowledgment and reason, and reports it.
    [Fact]
    public void AddOneCode_ImportsASingleLicensedRow()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/isbt-product-codes"] = new List<IsbtProductCodeDto>(),
            ["/api/admin/isbt-product-codes/import-licensed"] = new LicensedIsbtCatalogImportResult("ICCBBA-2026-10", 1, 0, 0)
        });
        var page = Render<AdminIsbtProductCodes>();
        page.WaitForAssertion(() => Assert.Contains("Import licensed extract", page.Markup));
        page.FindAll("button").First(b => b.TextContent.Contains("Import licensed extract")).Click();

        var add = page.Find("[data-isbt-one-add]");
        Assert.True(add.HasAttribute("disabled"));
        page.Find("[data-isbt-one-code]").Input("e9999");
        page.Find("[data-isbt-one-description]").Input("RED BLOOD CELLS|example");
        Assert.False(page.Find("[data-isbt-one-add]").HasAttribute("disabled"));
        page.Find("[data-isbt-one-add]").Click();

        page.WaitForAssertion(() => Assert.Contains("Imported 1 product and 0 ABO/RhD row(s).", page.Markup));
    }
}
