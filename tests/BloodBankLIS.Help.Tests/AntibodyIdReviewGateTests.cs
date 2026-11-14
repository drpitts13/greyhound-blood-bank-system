using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Components.Pages;
using BloodBankLIS.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Supervisor review offered Accept and Reject before any interpretation was recorded, and offered Accept to the
/// technologist who recorded it; the service refused both only after the click (Cycle 644).
/// </summary>
public class AntibodyIdReviewGateTests : BunitContext
{
    private static AntibodyIdWorkupDetailDto Workup(string? interpretation, string? technologist) =>
        new(1, 1, 5, null,
            interpretation is null ? AntibodyWorkupStatus.InProgress : AntibodyWorkupStatus.PendingSupervisorReview,
            AntibodyIdDatResult.NotPerformed, null, null, interpretation, technologist,
            interpretation is null ? null : DateTime.UtcNow.AddMinutes(-5), null, null, null, false, null, null, null, [],
            [new AntibodyIdCellDto(1, "1", PanelCellRole.Panel, 1, false, [],
                [new("IS", ReactionGrade.Negative), new("37C", ReactionGrade.Negative), new("AHG", ReactionGrade.TwoPlus)])],
            [], ["IS", "37C", "AHG"], true);

    private IRenderedComponent<AntibodyIdentification> Open(AntibodyIdWorkupDetailDto workup, string user, bool blockSelf = true)
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/antibody-id/1"] = workup,
            [$"/api/reference/policies/{FacilityPolicyKeys.AntibodyIdRequireSupervisorReview}"] =
                new FacilityPolicyValueDto(FacilityPolicyKeys.AntibodyIdRequireSupervisorReview, true),
            [$"/api/reference/policies/{FacilityPolicyKeys.AntibodyIdBlockSelfReview}"] =
                new FacilityPolicyValueDto(FacilityPolicyKeys.AntibodyIdBlockSelfReview, blockSelf)
        });
        Services.GetRequiredService<UserSession>().SignIn(user, user, "TEST", []);
        var page = Render<AntibodyIdentification>(p => p.Add(c => c.PatientId, 1L).Add(c => c.WorkupId, 1L));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("#abid-review")));
        return page;
    }

    private static AngleSharp.Dom.IElement? Button(IRenderedComponent<AntibodyIdentification> page, string label) =>
        page.Find("#abid-review").QuerySelectorAll("button").FirstOrDefault(b => b.TextContent.Trim() == label);

    // No technologist finding is on file, so accepting needs the acknowledgment; fill it so only the self-review gate
    // decides.
    private static void Acknowledge(IRenderedComponent<AntibodyIdentification> page) =>
        page.Find("#abid-review input[placeholder^='Reviewed leftover']").Input("Reviewed.");

    // TEST-BB-787: with no interpretation recorded, review says it is waiting and offers neither Accept nor Reject.
    [Fact]
    public void NoInterpretation_ReviewWaits()
    {
        var page = Open(Workup(null, null), "supervisor1");
        Assert.NotNull(page.Find("[data-review-waiting]"));
        Assert.Null(Button(page, "Accept"));
        Assert.Null(Button(page, "Reject"));
    }

    // TEST-BB-788: the technologist who recorded the interpretation is told another supervisor must accept it; Accept is
    // disabled and Reject stays available, as the service allows. The name matches regardless of case.
    [Fact]
    public void OwnInterpretation_CannotBeAccepted()
    {
        var page = Open(Workup("anti-K identified.", "TECH1"), "tech1");
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-review-self]")));
        Acknowledge(page);
        Assert.True(Button(page, "Accept")!.HasAttribute("disabled"));
        Assert.False(Button(page, "Reject")!.HasAttribute("disabled"));
    }

    // TEST-BB-789: another supervisor may accept; and when the facility allows self-review, the technologist may too.
    [Fact]
    public void OtherSupervisorOrPolicyOff_MayAccept()
    {
        var other = Open(Workup("anti-K identified.", "tech1"), "supervisor1");
        Assert.Empty(other.FindAll("[data-review-self]"));
        Acknowledge(other);
        Assert.False(Button(other, "Accept")!.HasAttribute("disabled"));
    }

    [Fact]
    public void SelfReviewAllowed_TechnologistMayAccept()
    {
        var page = Open(Workup("anti-K identified.", "tech1"), "tech1", blockSelf: false);
        Acknowledge(page);
        page.WaitForAssertion(() => Assert.False(Button(page, "Accept")!.HasAttribute("disabled")));
        Assert.Empty(page.FindAll("[data-review-self]"));
    }
}
