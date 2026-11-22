using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// A panel is read a phase at a time and most cells are often negative, yet each cell was a separate pick. Rest 0 sets
/// the cells still NT in one phase to 0, outlined until saved so each is checked against its tube (Cycle 641, OCD-079).
/// </summary>
public class AntibodyIdRestNegativeTests : BunitContext
{
    private static AntibodyIdCellDto Cell(long id, params AntibodyIdReactionDto[] reactions) =>
        new(id, id.ToString(), PanelCellRole.Panel, (int)id, false, [], reactions);

    private static AntibodyIdWorkupDetailDto Workup(AntibodyWorkupStatus status, params AntibodyIdCellDto[] cells) =>
        new(1, 1, 5, null, status, AntibodyIdDatResult.NotPerformed, null, null, null, null, null, null, null, null, false,
            null, null, null, [], cells, [], ["IS", "37C", "AHG"], true);

    private IRenderedComponent<AntibodyIdentification> Open(AntibodyIdWorkupDetailDto workup, AntibodyIdWorkupDetailDto? afterSave = null)
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/antibody-id/1"] = workup,
            ["/api/antibody-id/1/reactions"] = afterSave ?? workup
        });
        var page = Render<AntibodyIdentification>(p => p.Add(c => c.PatientId, 1L).Add(c => c.WorkupId, 1L));
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("#abid-antigram select")));
        return page;
    }

    // Grade selects in row order: cell, then IS, 37C, AHG.
    private static string[] Grades(IRenderedComponent<AntibodyIdentification> page) =>
        page.FindAll("#abid-antigram tbody select").Select(s => s.GetAttribute("value") ?? "").ToArray();

    // TEST-BB-780: Rest 0 at IS sets only the cells still NT at IS, outlines them and says how many; a graded cell and the
    // other phases are left as they were, and changing an outlined cell by hand removes its outline.
    [Fact]
    public void RestNegative_FillsOnlyUntestedCellsInThePhase()
    {
        var page = Open(Workup(AntibodyWorkupStatus.InProgress,
            Cell(1, new AntibodyIdReactionDto("IS", ReactionGrade.TwoPlus)), Cell(2), Cell(3)));
        page.Find("[data-fill-negative='IS']").Click();

        var g = Grades(page);
        Assert.Equal(["TwoPlus", "NotTested", "NotTested", "Negative", "NotTested", "NotTested", "Negative", "NotTested", "NotTested"], g);
        Assert.Equal(2, page.FindAll("[data-bulk-filled]").Count);
        Assert.Contains("2 cells set to 0", page.Find("[data-bulk-filled-note]").TextContent);
        Assert.Empty(page.FindAll("[data-fill-negative='IS']"));
        Assert.NotEmpty(page.FindAll("[data-fill-negative='AHG']"));

        page.FindAll("#abid-antigram tbody select")[3].Change("OnePlus");
        Assert.Single(page.FindAll("[data-bulk-filled]"));
        Assert.Contains("1 cell set to 0", page.Find("[data-bulk-filled-note]").TextContent);
    }

    // TEST-BB-781: saving clears the outlines and the note once the reactions come back saved.
    [Fact]
    public void Saving_ClearsTheOutlines()
    {
        var saved = Workup(AntibodyWorkupStatus.InProgress,
            Cell(1, new AntibodyIdReactionDto("IS", ReactionGrade.Negative)), Cell(2, new AntibodyIdReactionDto("IS", ReactionGrade.Negative)));
        var page = Open(Workup(AntibodyWorkupStatus.InProgress, Cell(1), Cell(2)), saved);
        page.Find("[data-fill-negative='IS']").Click();
        Assert.Equal(2, page.FindAll("[data-bulk-filled]").Count);

        page.FindAll("button").First(b => b.TextContent.Trim() == "Save reactions").Click();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[data-bulk-filled]")));
        Assert.Empty(page.FindAll("[data-bulk-filled-note]"));
    }

    // TEST-BB-782: a completed workup cannot be edited, so it offers no Rest 0.
    [Fact]
    public void CompletedWorkup_OffersNoRestNegative()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/antibody-id/1"] = Workup(AntibodyWorkupStatus.Completed, Cell(1), Cell(2))
        });
        var page = Render<AntibodyIdentification>(p => p.Add(c => c.PatientId, 1L).Add(c => c.WorkupId, 1L));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("#abid-antigram")));
        Assert.Empty(page.FindAll("[data-fill-negative]"));
    }
}
