using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Discard expired (end-user testing pass 54): the label said "when your facility requires one", so the first scan was
/// sent and refused (INV-DISC-2ND) before the technologist knew a second verifier was needed.
/// </summary>
public class InventoryDiscardExpiredTests : BunitContext
{
    private static Dictionary<string, object> Responses(bool required) => new()
    {
        ["/api/inventory/expired"] = new List<ExpiredUnitDto>
        {
            new(1, "W0001230001028", "RBC-LR", "Red Blood Cells, Leukoreduced", "AB-", UnitStatus.Available, DateTime.UtcNow.AddDays(-10), "Main Blood Bank Refrigerator")
        },
        [$"/api/reference/policies/{FacilityPolicyKeys.RequireDiscardVerifier}"] = new FacilityPolicyValueDto(FacilityPolicyKeys.RequireDiscardVerifier, required)
    };

    // TEST-BB-1068: when the facility requires a second verifier the page says so, and a scan with none chosen is not
    // sent; when it does not, the page says that instead.
    [Fact]
    public void SecondVerifier_RequiredIsSaid_AndAScanWithoutOneIsNotSent()
    {
        CannedApi.Register(this, Responses(required: true));
        var page = Render<InventoryDiscardExpired>();
        page.WaitForAssertion(() => Assert.Contains("W0001230001028", page.Markup));
        page.WaitForAssertion(() => Assert.Equal("Second verifier (required at this facility)", page.Find("[data-discard-verifier-label]").TextContent));

        var scan = page.Find("[data-discard-scan]");
        scan.Input("W0001230001028");
        scan.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.Contains("Choose the second verifier first", page.Markup);
        Assert.DoesNotContain("This run:", page.Markup);
    }

    [Fact]
    public void SecondVerifier_NotRequired_IsSaid()
    {
        CannedApi.Register(this, Responses(required: false));
        var page = Render<InventoryDiscardExpired>();
        page.WaitForAssertion(() => Assert.Equal("Second verifier (not required at this facility)", page.Find("[data-discard-verifier-label]").TextContent));
    }
}
