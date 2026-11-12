using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>Keeping the reagent register on Reagent QC (Cycle 713).</summary>
public class ReagentRegisterPageTests : BunitContext
{
    // TEST-BB-985: a retired reagent is hidden until Show retired reagents, and then offers Restore; Retire on a reagent
    // in use opens a form whose button needs a reason and says what retiring does; saving reports it and marks the
    // reagent retired.
    [Fact]
    public void Retire_NeedsAReason_AndRetiredAreHiddenUntilShown()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/reagents"] = new List<ReagentDto> { new(1, "ANTI-A", "Anti-A", true, "ABORH"), new(2, "OLD-D", "Old anti-D", false) },
            ["/api/reagents/1/active"] = new ReagentDto(1, "ANTI-A", "Anti-A", false, "ABORH")
        });
        var page = Render<Reagents>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-reagent-row='ANTI-A']")));
        Assert.Empty(page.FindAll("[data-reagent-row='OLD-D']"));

        page.Find("[data-reagent-show-retired]").Change(true);
        Assert.NotNull(page.Find("[data-reagent-row='OLD-D'] [data-reagent-restore]"));

        page.Find("[data-reagent-row='ANTI-A'] [data-reagent-retire]").Click();
        Assert.Contains("Close its lots first", page.Find("[data-reagent-register-form]").TextContent);
        Assert.True(page.Find("[data-reagent-register-save]").HasAttribute("disabled"));
        page.Find("[data-reagent-register-reason]").Input("Discontinued");
        page.Find("[data-reagent-register-save]").Click();

        page.WaitForAssertion(() => Assert.Contains("ANTI-A retired.", page.Markup));
        Assert.NotNull(page.Find("[data-reagent-row='ANTI-A'] [data-reagent-restore]"));
    }
}
