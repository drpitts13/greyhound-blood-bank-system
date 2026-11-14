using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>The chart's DAT / Elution tab, rendered with workups on file (end-user testing pass 9).</summary>
public class ChartDatElutionTabTests : BunitContext
{
    private static DatElutionWorkupDto Workup(long id, DatElutionWorkupStatus status) => new(
        id, 4, null, null, status, DatWorkupResult.Positive, "Tube", "Reactive", null, null, DateTime.UtcNow.AddHours(-1), "tech1",
        null, null, null, null, null)
    {
        PolyspecificGrade = ReactionGrade.TwoPlus, IgGGrade = ReactionGrade.TwoPlus, C3Grade = ReactionGrade.Negative,
        ControlGrade = ReactionGrade.Negative
    };

    // TEST-BB-1015: a patient with an open and a completed workup renders (the void-reason box threw on any workup), the
    // open workup offers the four reagent grades, and the completed one reads its result and grades in words.
    [Fact]
    public void Tab_WithWorkups_RendersTheGrades()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/patients/4/dat-elution"] = new List<DatElutionWorkupDto> { Workup(1, DatElutionWorkupStatus.Open), Workup(2, DatElutionWorkupStatus.Completed) }
        });

        var tab = Render<PatientChartDatElutionTab>(p => p.Add(c => c.PatientId, 4L));
        tab.WaitForAssertion(() => Assert.Single(tab.FindAll("[data-dat-grades]")));
        Assert.Equal(4, tab.Find("[data-dat-grades]").QuerySelectorAll("select").Length);
        Assert.Contains("DAT Positive (Tube): Polyspecific 2+ · IgG 2+ · C3 0 · Control 0", tab.Markup);
        Assert.Contains("Completed", tab.Markup);
        Assert.NotEmpty(tab.FindAll("[data-dat-void-reason]"));
    }
}
