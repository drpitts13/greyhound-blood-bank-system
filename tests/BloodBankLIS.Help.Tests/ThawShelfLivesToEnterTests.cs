using BloodBankLIS.Application.Admin;
using BloodBankLIS.Web.Components.Pages.Admin;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>Thaw codes waiting for an open-system or pooled shelf life are named before a thaw is refused (Cycle 684).</summary>
public class ThawShelfLivesToEnterTests : BunitContext
{
    private static List<AutoThawProductDto> Codes() =>
    [
        new(1, "E0707", "Plasma", true, 24, null, null),
        new(2, "E2555", "Plasma, pooled", true, 24, 4, null),
        new(3, "E0710", "Plasma", true, 24, 4, 4),
        new(4, "E9999", "Old plasma", false, 24, null, null)
    ];

    // TEST-BB-899: only active codes with a time not set are named, each with what it waits for.
    [Fact]
    public void Describe_NamesActiveCodesWaiting()
    {
        Assert.Equal(["E0707 (open system, pooled)", "E2555 (pooled)"], AutoThawShelfLivesToEnter.Describe(Codes()));
        Assert.Empty(AutoThawShelfLivesToEnter.Describe([new(3, "E0710", "Plasma", true, 24, 4, 4)]));
    }

    // TEST-BB-900: Facility Policies names the waiting thaw codes and links to Auto thaw products filtered to them.
    [Fact]
    public void FacilityPolicies_NamesThawCodesWaiting()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/facility-policies"] = new List<FacilityPolicyDto>(),
            ["/api/admin/auto-thaw-products"] = Codes()
        });
        var page = Render<AdminFacilityPolicies>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-thaw-to-enter]")));

        var alert = page.Find("[data-thaw-to-enter]").TextContent;
        Assert.Contains("2 auto-thaw product codes are waiting", alert);
        Assert.Contains("E0707 (open system, pooled); E2555 (pooled)", alert);
        Assert.DoesNotContain("E9999", alert);
        Assert.Equal("/admin/auto-thaw-products?show=to-enter", page.Find("[data-thaw-to-enter-link]").GetAttribute("href"));
    }

    // TEST-BB-901: arriving filtered, Auto thaw products lists only the waiting codes; Show all codes brings the rest back.
    [Fact]
    public void AutoThawProducts_FilteredToCodesWaiting()
    {
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/admin/auto-thaw-products"] = Codes() });
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/auto-thaw-products?show=to-enter");
        var page = Render<AdminAutoThawProducts>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-thaw-open='E0707']")));

        Assert.NotNull(page.Find("[data-thaw-pooled='E2555']"));
        Assert.Empty(page.FindAll("[data-thaw-open='E0710']"));
        Assert.Contains("2 codes are waiting", page.Find("[data-thaw-to-enter]").TextContent);

        page.Find("[data-thaw-show-all]").Click();
        Assert.NotNull(page.Find("[data-thaw-open='E0710']"));
        Assert.Empty(page.FindAll("[data-thaw-open='E9999']"));
    }
}
