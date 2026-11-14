using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Complete workup was offered before an interpretation was recorded or a supervisor had accepted it; the service
/// refused only after the click. The card now says what it is waiting for (Cycle 645).
/// </summary>
public class AntibodyIdCompleteGateTests : BunitContext
{
    private static AntibodyIdWorkupDetailDto Workup(bool interpreted, DateTime? reviewed = null, bool accepted = false, string? supervisor = null) =>
        new(1, 1, 5, null, AntibodyWorkupStatus.PendingSupervisorReview, AntibodyIdDatResult.NotPerformed, null, null,
            interpreted ? "anti-K identified." : null, interpreted ? "tech1" : null,
            interpreted ? DateTime.UtcNow.AddMinutes(-10) : null, supervisor, reviewed, null, accepted, null, null, null, [],
            [new AntibodyIdCellDto(1, "1", PanelCellRole.Panel, 1, false, [],
                [new("IS", ReactionGrade.Negative), new("37C", ReactionGrade.Negative), new("AHG", ReactionGrade.TwoPlus)])],
            [], ["IS", "37C", "AHG"], true);

    private IRenderedComponent<AntibodyIdentification> Open(AntibodyIdWorkupDetailDto workup, bool reviewRequired = true)
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/antibody-id/1"] = workup,
            [$"/api/reference/policies/{FacilityPolicyKeys.AntibodyIdRequireSupervisorReview}"] =
                new FacilityPolicyValueDto(FacilityPolicyKeys.AntibodyIdRequireSupervisorReview, reviewRequired)
        });
        var page = Render<AntibodyIdentification>(p => p.Add(c => c.PatientId, 1L).Add(c => c.WorkupId, 1L));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("#abid-complete")));
        return page;
    }

    private static string? Waiting(IRenderedComponent<AntibodyIdentification> page) =>
        page.FindAll("[data-complete-waiting]").SingleOrDefault()?.TextContent;

    private static bool OffersComplete(IRenderedComponent<AntibodyIdentification> page) =>
        page.Find("#abid-complete").QuerySelectorAll("button").Any(b => b.TextContent.Trim() == "Complete workup");

    // TEST-BB-790: with no interpretation, Complete says it waits for the interpretation and review, and is not offered.
    [Fact]
    public void NoInterpretation_WaitsForInterpretation()
    {
        var page = Open(Workup(interpreted: false));
        page.WaitForAssertion(() => Assert.Contains("technologist interpretation", Waiting(page)));
        Assert.False(OffersComplete(page));
    }

    // TEST-BB-791: interpreted but not reviewed waits for supervisor review; rejected says so, names the supervisor, and
    // asks for a new interpretation. Neither offers Complete.
    [Fact]
    public void Interpreted_WaitsForReview()
    {
        var page = Open(Workup(interpreted: true));
        page.WaitForAssertion(() => Assert.Contains("Waiting for supervisor review", Waiting(page)));
        Assert.False(OffersComplete(page));
    }

    [Fact]
    public void Rejected_AsksForANewInterpretation()
    {
        var page = Open(Workup(interpreted: true, reviewed: DateTime.UtcNow.AddMinutes(-2), accepted: false, supervisor: "supervisor1"));
        page.WaitForAssertion(() => Assert.Contains("supervisor rejected this interpretation (supervisor1)", Waiting(page)));
        Assert.False(OffersComplete(page));
    }

    // TEST-BB-792: once a supervisor accepts, Complete is offered; with review not required, an interpretation is enough.
    [Fact]
    public void Accepted_OffersComplete()
    {
        var page = Open(Workup(interpreted: true, reviewed: DateTime.UtcNow.AddMinutes(-2), accepted: true, supervisor: "supervisor1"));
        page.WaitForAssertion(() => Assert.True(OffersComplete(page)));
        Assert.Null(Waiting(page));
    }

    [Fact]
    public void ReviewNotRequired_OffersCompleteAfterInterpretation()
    {
        var page = Open(Workup(interpreted: true), reviewRequired: false);
        page.WaitForAssertion(() => Assert.True(OffersComplete(page)));
        Assert.Null(Waiting(page));
    }
}
