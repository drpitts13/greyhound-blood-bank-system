using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// The antibody card (end-user testing pass 80): it did not say which blood bank issued it or whom to call, so another
/// hospital could not confirm the history.
/// </summary>
public class AntibodyCardFacilityTests : BunitContext
{
    private static DowntimePatientRow Card() => new(4, "MRN0004", "Kellerman, Diane", new DateOnly(1957, 2, 18), "O+", true,
        ["anti-K"], ["K-negative red cells (anti-K on file)"], [], [], []);

    // TEST-BB-1090: the card names the blood bank and its telephone when configured; otherwise the screen says so.
    [Fact]
    public void Card_NamesTheBloodBank_OrSaysItIsNotConfigured()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/patients/4/antibody-card"] = Card(),
            ["/api/reference/facility"] = new FacilityIdentityDto("General Hospital Blood Bank", "555-0100")
        });
        var page = Render<AntibodyCard>(p => p.Add(c => c.PatientId, 4));
        page.WaitForAssertion(() => Assert.Contains("General Hospital Blood Bank · tel 555-0100", page.Find("[data-antibody-card-facility]").TextContent));
        Assert.Empty(page.FindAll("[data-antibody-card-no-facility]"));
    }

    [Fact]
    public void Card_WithoutFacility_SaysItIsNotConfigured()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/patients/4/antibody-card"] = Card(),
            ["/api/reference/facility"] = new FacilityIdentityDto(null, null)
        });
        var page = Render<AntibodyCard>(p => p.Add(c => c.PatientId, 4));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-antibody-card-no-facility]")));
        Assert.Contains("Facility:Name and Facility:Phone", page.Find("[data-antibody-card-no-facility]").TextContent);
        Assert.Empty(page.FindAll("[data-antibody-card-facility]"));
    }
}
