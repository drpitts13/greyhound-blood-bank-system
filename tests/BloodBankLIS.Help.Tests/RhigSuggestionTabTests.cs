using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>OCD-069 (decided 2026-11-10): the RhIG tab shows the suggestion and the calculated dose; the reviewer decides.</summary>
public class RhigSuggestionTabTests : BunitContext
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static RhigEvaluationDto Evaluation(long id, RhigDecision decision, int? vials, DateTime? completed, int? calculated) => new(
        id, 1, RhigContext.Postpartum, Now.AddHours(-5), RhType.Positive, FetalBleedScreen.Positive, 0.9m, decision, vials, null,
        "tech1", Now.AddHours(-4), completed is null ? null : "super1", completed, ["Patient's current Rh type on file: negative."],
        SuggestedDecision: RhigDecision.Candidate,
        SuggestionReason: completed is null ? "Suggested candidate: the patient is Rh negative with no anti-D, and the infant is Rh positive." : null,
        VialsCalculated: calculated,
        DoseExplanation: completed is null ? "0.9% × 5000 mL = 45 mL fetal blood; ÷ 30 mL per vial = 1.5; rounded to the nearest and one added: 3 vials." : null);

    // TEST-BB-870: an open evaluation shows the suggestion and the dose working, and Use the suggestion fills the form with
    // the suggested decision and calculated vials for the reviewer to save; a completed one says what was suggested and
    // calculated then, flagged when the recorded dose differs.
    [Fact]
    public void Tab_ShowsSuggestionAndDose()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/patients/1/rhig"] = new List<RhigEvaluationDto>
            {
                Evaluation(2, RhigDecision.Undetermined, null, null, 3),
                Evaluation(1, RhigDecision.Candidate, 2, Now.AddDays(-30), 3)
            }
        });
        var tab = Render<PatientChartRhigTab>(p => p.Add(x => x.PatientId, 1));
        tab.WaitForAssertion(() => Assert.NotNull(tab.Find("[data-rhig-row='2']")));

        var open = tab.Find("[data-rhig-row='2']");
        Assert.Contains("Suggested candidate", open.QuerySelector("[data-rhig-suggestion]")!.TextContent);
        Assert.Contains("3 vials", open.QuerySelector("[data-rhig-dose]")!.TextContent);

        var completed = tab.Find("[data-rhig-row='1']");
        Assert.Contains("Suggested when completed: candidate; calculated: 3 vials.", completed.TextContent);
        Assert.NotNull(completed.QuerySelector("[data-rhig-differs]"));

        tab.Find("[data-rhig-use-suggestion]").Click();
        Assert.Equal("3", tab.Find("[data-rhig-vials]").GetAttribute("value"));
    }
}
