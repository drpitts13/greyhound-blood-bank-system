using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyPanelLotSupplyRuleTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);

    private static AntibodyPanelLotSupplyRule.Lot Lot(long id, int daysLeft, bool selected = false, bool active = true) =>
        new(id, selected ? "Selected" : "Panel A", $"L-{id}", "Maker", Today.AddDays(daysLeft), selected, active);

    // TEST-BB-963: a current lot expiring within the reagent warning window is listed; it needs action unless a later
    // current lot of the same kind follows it. Lots outside the window, inactive or already expired are not listed.
    [Fact]
    public void Review_ExpiringLots_NeedActionWithoutAReplacement()
    {
        var items = AntibodyPanelLotSupplyRule.Review(
        [
            Lot(1, 3), Lot(2, 0, selected: true), Lot(3, ReagentLotRule.ExpiringSoonDays + 1, selected: true),
            Lot(4, 1, active: false), Lot(5, -2)
        ], Today);

        var panel = Assert.Single(items, i => !i.IsSelectedCellLot);
        Assert.Equal((AntibodyPanelLotSupplyRule.Expiring, 1L, 3), (panel.Kind, panel.Lot!.Id, panel.DaysLeft!.Value));
        Assert.True(panel.NeedsAction);
        Assert.Contains("Panel A lot L-1 expires in 3 days (2026-11-14); no other primary panel lot is entered to follow it.", panel.Message);

        var selected = Assert.Single(items, i => i.IsSelectedCellLot);
        Assert.Equal(3L, selected.Replacement!.Id);
        Assert.False(selected.NeedsAction);
        Assert.Contains("expires today", selected.Message);
        Assert.Equal(panel, items[0]);

        Assert.Empty(AntibodyPanelLotSupplyRule.Review([Lot(1, ReagentLotRule.ExpiringSoonDays + 1)], Today));
        Assert.Contains("expires tomorrow", Assert.Single(AntibodyPanelLotSupplyRule.Review([Lot(1, 1)], Today)).Message);
    }

    // TEST-BB-964: with no active, unexpired primary panel lot the bench cannot start a workup, which needs action; a
    // facility that never entered a selected-cell lot is not told it lacks one, but one whose selected lots have all
    // expired is.
    [Fact]
    public void Review_NoCurrentLotOfAKind_NeedsAction()
    {
        var none = Assert.Single(AntibodyPanelLotSupplyRule.Review([], Today));
        Assert.Equal(AntibodyPanelLotSupplyRule.NoPanel, none.Kind);
        Assert.True(none.NeedsAction);
        Assert.Contains("an antibody workup cannot be started", none.Message);

        var items = AntibodyPanelLotSupplyRule.Review([Lot(1, 30), Lot(2, -1, selected: true), Lot(3, 20, selected: true, active: false)], Today);
        var selected = Assert.Single(items);
        Assert.Equal(AntibodyPanelLotSupplyRule.NoSelected, selected.Kind);
        Assert.Null(selected.Lot);
        Assert.Contains("selected cells cannot be added", selected.Message);
    }
}
