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

    // TEST-BB-1006: today's QC lists every open lot in date on one table; a lot with QC today shows done (red when it
    // failed); expected starts blank with the last value as a hint, copied only on request; a mismatch reads Failed; the
    // button counts the rows with both results.
    [Fact]
    public void QcRun_ListsOpenLots_AndCountsRowsToRecord()
    {
        var expires = DateTime.UtcNow.AddDays(30);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/reagents"] = new List<ReagentDto> { new(1, "ANTI-A", "Anti-A", true), new(2, "ANTI-D", "Anti-D", true), new(3, "AHG", "AHG", true) },
            ["/api/reagents/lots"] = new List<ReagentLotDto>
            {
                new(11, 1, "ANTI-A", "AA-1", DateTime.UtcNow, DateTime.UtcNow, null, expires, BloodBankLIS.Domain.Entities.ReagentLotState.Opened),
                new(12, 2, "ANTI-D", "AD-1", DateTime.UtcNow, DateTime.UtcNow, null, expires, BloodBankLIS.Domain.Entities.ReagentLotState.Opened),
                new(13, 3, "AHG", "AH-1", DateTime.UtcNow, DateTime.UtcNow, null, expires, BloodBankLIS.Domain.Entities.ReagentLotState.Opened)
            },
            ["/api/reagents/qc"] = new List<ReagentDailyQcDto>
            {
                new(1, 13, "AH-1", DateTime.UtcNow.Date, "2+", "0", false, "tech", DateTime.UtcNow, null, null, 3, "AHG"),
                new(2, 12, "AD-OLD", DateTime.UtcNow.Date.AddDays(-1), "3+", "3+", true, "tech", DateTime.UtcNow.AddDays(-1), null, null, 2, "ANTI-D")
            }
        });
        var page = Render<Reagents>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("[data-qc-run-row]").Count));

        Assert.Contains("Failed today", page.Find("[data-qc-run-row='AH-1']").TextContent);
        Assert.Contains("last: 3+", page.Find("[data-qc-run-row='AD-1']").TextContent);
        Assert.Equal("", page.Find("[data-qc-run-row='AD-1'] [data-qc-run-expected]").GetAttribute("value") ?? "");
        Assert.True(page.Find("[data-qc-run-save]").HasAttribute("disabled"));

        page.Find("[data-qc-run-use-last]").Click();
        Assert.Equal("3+", page.Find("[data-qc-run-row='AD-1'] [data-qc-run-expected]").GetAttribute("value"));
        page.Find("[data-qc-run-row='AD-1'] [data-qc-run-observed]").Input("0");
        Assert.Equal("fail", page.Find("[data-qc-run-row='AD-1'] [data-qc-run-result]").GetAttribute("value"));
        page.Find("[data-qc-run-row='AA-1'] [data-qc-run-expected]").Input("4+");
        page.Find("[data-qc-run-row='AA-1'] [data-qc-run-observed]").Input("4+");
        Assert.Equal("pass", page.Find("[data-qc-run-row='AA-1'] [data-qc-run-result]").GetAttribute("value"));
        Assert.Contains("Record 2 QC", page.Find("[data-qc-run-save]").TextContent);
        Assert.False(page.Find("[data-qc-run-save]").HasAttribute("disabled"));
    }
}
