using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Opening a workup asked the technologist to pick the panel lot even when only one was in date, and with none the
/// Open workup button was disabled without saying why (Cycle 640).
/// </summary>
public class AntibodyIdLotDefaultTests : BunitContext
{
    private static AntibodyPanelLotListItemDto Lot(long id, string lot, bool expired = false, bool active = true, bool selectedCell = false) =>
        new(id, 1, "Demo Reagents", lot, new DateOnly(2027, 6, 30), selectedCell ? "Selected cells" : "Panel A", selectedCell, active, expired);

    private IRenderedComponent<AntibodyIdentification> Open(params AntibodyPanelLotListItemDto[] lots)
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/antibody-id/lots"] = lots.ToList()
        });
        return Render<AntibodyIdentification>(p => p.Add(c => c.PatientId, 1L));
    }

    private static string? SelectedLot(IRenderedComponent<AntibodyIdentification> page) =>
        page.FindAll("select").First(s => s.InnerHtml.Contains("Select an in-date lot")).GetAttribute("value");

    private static bool OpenEnabled(IRenderedComponent<AntibodyIdentification> page) =>
        !page.FindAll("button").First(b => b.TextContent.Trim() == "Open workup").HasAttribute("disabled");

    // TEST-BB-777: the only in-date panel lot is selected and says so, so Open workup is ready; expired, inactive and
    // selected-cell lots do not count.
    [Fact]
    public void OnlyInDatePanelLot_IsSelected()
    {
        var page = Open(Lot(7, "PA-001"), Lot(8, "PA-OLD", expired: true), Lot(9, "PA-OFF", active: false), Lot(10, "SC-1", selectedCell: true));
        page.WaitForAssertion(() => Assert.Equal("7", SelectedLot(page)));
        Assert.NotNull(page.Find("[data-lot-defaulted]"));
        Assert.Empty(page.FindAll("[data-no-panel-lot]"));
        Assert.True(OpenEnabled(page));
    }

    // TEST-BB-778: with two in-date panel lots nothing is chosen for the technologist, and Open workup waits for a pick.
    [Fact]
    public void SeveralPanelLots_LeaveTheChoice()
    {
        var page = Open(Lot(7, "PA-001"), Lot(11, "PA-002"));
        page.WaitForAssertion(() => Assert.Contains("PA-002", page.Markup));
        Assert.True(string.IsNullOrEmpty(SelectedLot(page)));
        Assert.Empty(page.FindAll("[data-lot-defaulted]"));
        Assert.False(OpenEnabled(page));
    }

    // TEST-BB-779: with no in-date active panel lot the page says why a workup cannot be opened and links to the lots.
    [Fact]
    public void NoPanelLot_SaysWhyAndWhere()
    {
        var page = Open(Lot(8, "PA-OLD", expired: true), Lot(10, "SC-1", selectedCell: true));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-no-panel-lot]")));
        Assert.Contains("/admin/antibody-panel-lots", page.Find("[data-no-panel-lot]").InnerHtml);
        Assert.False(OpenEnabled(page));
    }
}
