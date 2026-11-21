using BloodBankLIS.Application.Admin;
using BloodBankLIS.Web.Components.Pages.Admin;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>Facility Policies says which decided settings are not entered yet (Cycle 677).</summary>
public class SettingsToEnterPageTests : BunitContext
{
    private static FacilityPolicyDto Policy(long id, string name, string value, bool awaits) => new(
        id, $"Key.{id}", value, "RhIG", name, "Description", "Medical director", "Integer", "0", 0, 100, false, awaits);

    // TEST-BB-887: the page names the settings waiting to be entered and marks each "Not set"; "Show only these", or
    // arriving from the home card (?show=to-enter), lists only them; "Show all settings" brings the rest back.
    [Fact]
    public void SettingsToEnter_AreNamedMarkedAndFiltered()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/facility-policies"] = new List<FacilityPolicyDto>
            {
                Policy(1, "RhIG dose: mL of fetal whole blood per vial", "0", true),
                Policy(2, "Specimen retention days", "0", true),
                Policy(3, "Neonatal age limit (days)", "120", false)
            }
        });
        var page = Render<AdminFacilityPolicies>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-settings-to-enter]")));

        var alert = page.Find("[data-settings-to-enter]").TextContent;
        Assert.Contains("2 settings are decided but not entered yet", alert);
        Assert.Contains("RhIG dose: mL of fetal whole blood per vial; Specimen retention days", alert);
        Assert.Equal(2, page.FindAll("[data-setting-to-enter]").Count);
        Assert.Contains("Neonatal age limit", page.Markup);

        page.Find("[data-settings-to-enter-show]").Click();
        Assert.DoesNotContain("Neonatal age limit", page.Markup);
        page.Find("[data-settings-show-all]").Click();
        Assert.Contains("Neonatal age limit", page.Markup);
    }
}
