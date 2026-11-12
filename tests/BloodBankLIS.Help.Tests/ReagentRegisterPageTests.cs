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

    // TEST-BB-988: Correct on a lot opens a form filled with its number, which needs a reason and says results keep the
    // number they were recorded with; saving shows the corrected number.
    [Fact]
    public void CorrectLot_IsPrefilled_AndNeedsAReason()
    {
        var expires = DateTime.UtcNow.AddDays(30);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/reagents"] = new List<ReagentDto> { new(1, "ANTI-A", "Anti-A", true) },
            ["/api/reagents/lots"] = new List<ReagentLotDto> { new(7, 1, "ANTI-A", "L-1", DateTime.UtcNow, null, null, expires, BloodBankLIS.Domain.Entities.ReagentLotState.Received) },
            ["/api/reagents/lots/7"] = new ReagentLotDto(7, 1, "ANTI-A", "L-1A", DateTime.UtcNow, null, null, expires, BloodBankLIS.Domain.Entities.ReagentLotState.Received)
        });
        var page = Render<Reagents>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-lot-correct='L-1']")));

        page.Find("[data-lot-correct='L-1']").Click();
        Assert.Equal("L-1", page.Find("[data-lot-correct-number]").GetAttribute("value"));
        Assert.Contains("keep the number as it was", page.Find("[data-lot-correct-form]").TextContent);
        Assert.True(page.Find("[data-lot-correct-save]").HasAttribute("disabled"));
        page.Find("[data-lot-correct-number]").Input("L-1A");
        page.Find("[data-lot-correct-reason]").Input("Typo at receipt");
        page.Find("[data-lot-correct-save]").Click();

        page.WaitForAssertion(() => Assert.Contains("Lot L-1A corrected.", page.Markup));
        Assert.NotNull(page.Find("[data-lot-correct='L-1A']"));
    }
}
