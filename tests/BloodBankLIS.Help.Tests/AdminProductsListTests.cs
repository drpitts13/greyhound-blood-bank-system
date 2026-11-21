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

/// <summary>Copy on products and modification rules starts a new unsaved draft (end-user testing loop 2 tick 9).</summary>
public class AdminCopyTests : BunitContext
{
    // TEST-BB-1391: Copy fills a new product form from the row with the code suffixed -COPY and the ISBT code cleared,
    // and says which product it came from; nothing is saved.
    [Fact]
    public void CopyProduct_OpensAPrefilledNewDraft()
    {
        var rbc = new ProductDefinitionDto(1, "RBC-LR", "Red cells LR", ComponentClass.RedBloodCells, null, 1008, true, true, true, true,
            "E0336", null, null, null, null, null, 1, true, []);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/products"] = new List<ProductDefinitionDto> { rbc },
            ["/api/admin/products/1"] = rbc
        });
        var page = Render<AdminProducts>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-product-copy='RBC-LR']")));
        page.Find("[data-product-copy='RBC-LR']").Click();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-product-copied-from]")));
        Assert.Contains("New product", page.Markup);
        Assert.Contains("Copied from RBC-LR", page.Find("[data-product-copied-from]").TextContent);
        Assert.Contains(page.FindAll("input"), i => i.GetAttribute("value") == "RBC-LR-COPY");
        Assert.Contains(page.FindAll("input"), i => i.GetAttribute("value") == "Red cells LR");
        Assert.DoesNotContain(page.FindAll("input"), i => i.GetAttribute("value") == "E0336");
    }

    // TEST-BB-1391: Copy on a modification rule fills a new rule form with the code suffixed -COPY.
    [Fact]
    public void CopyRule_OpensAPrefilledNewDraft()
    {
        var rule = new ModificationRuleDto(5, "IRR-RBC-LR", 1, "E0336", ModificationType.Irradiate, 2, "E0332", 3, "28D",
            ExpirationRelativeTo.ModificationDateTime, "Irradiate", 1, true);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/modification-rules"] = new List<ModificationRuleDto> { rule },
            ["/api/admin/modification-rules/5"] = rule
        });
        var page = Render<AdminModificationRules>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-rule-copy='IRR-RBC-LR']")));
        page.Find("[data-rule-copy='IRR-RBC-LR']").Click();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-rule-copied-from]")));
        Assert.Contains("New modification rule", page.Markup);
        Assert.Contains(page.FindAll("input"), i => i.GetAttribute("value") == "IRR-RBC-LR-COPY");
    }
}

/// <summary>The product attribute picker names each attribute and works by keyboard (end-user testing loop 2 tick 34).</summary>
public class ProductAttributePickerTests : BunitContext
{
    // TEST-BB-1410: each attribute toggle is a button reading code and name; choosing it marks it pressed and offers
    // "required" beside it.
    [Fact]
    public void AttributePicker_NamesAttributes_AndTogglesAsButtons()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/products"] = new List<ProductDefinitionDto>(),
            ["/api/admin/products/attributes"] = new List<ProductAttributeDto> { new(4, "IRRAD", "Irradiated", null, true) }
        });
        var page = Render<AdminProducts>();
        page.WaitForAssertion(() => Assert.Contains("New product", page.Markup));
        page.FindAll("button").First(b => b.TextContent.Trim() == "New product").Click();

        var toggle = page.Find("[data-product-attr-toggle='IRRAD']");
        Assert.Equal("BUTTON", toggle.TagName);
        Assert.Contains("IRRAD · Irradiated", toggle.TextContent);
        Assert.Equal("false", toggle.GetAttribute("aria-pressed"));
        toggle.Click();
        Assert.Equal("true", page.Find("[data-product-attr-toggle='IRRAD']").GetAttribute("aria-pressed"));
        Assert.Contains("required", page.Find("[data-product-attr='IRRAD']").TextContent);
    }
}

/// <summary>Clickable elements are buttons or links, so they can be reached by keyboard (end-user testing loop 2 tick 34).</summary>
public partial class ClickableElementSourceTests
{
    // TEST-BB-1410: no page puts @onclick on a span, div, td, li or tr (other than a dialog backdrop), which a keyboard
    // cannot reach; the grouper and product attribute pickers did.
    [Fact]
    public void ClickHandlers_AreOnButtonsOrLinks()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var offenders = Directory.GetFiles(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => ClickableNonButton().Matches(File.ReadAllText(f))
                .Where(m => !m.Value.Contains("bb-dialog-backdrop"))
                .Select(m => $"{Path.GetFileName(f)}: {m.Value}"))
            .ToList();
        Assert.Empty(offenders);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"<(?:span|div|td|li|tr)\b[^>]*@onclick=")]
    private static partial System.Text.RegularExpressions.Regex ClickableNonButton();
}
