using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>End-user testing pass 89: units stored where the location does not take their component.</summary>
public class WrongStorageRuleTests
{
    // TEST-BB-1103: platelets on a shelf of a red-cell refrigerator are named, including expired ones still on the
    // shelf; a unit the location takes, or one no longer on a shelf (issued, transfused, discarded), is not.
    [Fact]
    public void WrongStorage_NamesAComponentTheShelfDoesNotTake()
    {
        string? Fridge(UnitStatus status, ComponentClass component) =>
            InventoryLocationPolicyRule.WrongStorage(status, "FRIDGE-1", allowsRbc: true, allowsPlasma: false, allowsPlatelets: false,
                allowsCryo: false, allowsWholeBlood: true, component);

        Assert.Equal("FRIDGE-1 does not take platelets", Fridge(UnitStatus.Available, ComponentClass.Platelets));
        Assert.Equal("FRIDGE-1 does not take platelets", Fridge(UnitStatus.Expired, ComponentClass.Platelets));
        Assert.Equal("FRIDGE-1 does not take plasma", Fridge(UnitStatus.Quarantine, ComponentClass.Plasma));
        Assert.Null(Fridge(UnitStatus.Available, ComponentClass.RedBloodCells));
        Assert.Null(Fridge(UnitStatus.Issued, ComponentClass.Platelets));
        Assert.Null(Fridge(UnitStatus.Discarded, ComponentClass.Platelets));
    }
    // TEST-BB-1234 (end-user testing pass 212): the list's note says to discard expired units in the wrong storage
    // rather than move them; with some expired it says so and to move the rest; with none expired it is as before.
    [Fact]
    public void WrongStorageSummary_SaysToDiscardExpiredUnits()
    {
        Assert.Null(InventoryLocationPolicyRule.WrongStorageSummary(0, 0));
        Assert.Equal("9 units are stored where the location does not take the component (marked below). All have expired: discard them (Discard expired) rather than moving them.",
            InventoryLocationPolicyRule.WrongStorageSummary(9, 9));
        Assert.Equal("1 unit is stored where the location does not take the component (marked below). It has expired: discard it (Discard expired) rather than moving it.",
            InventoryLocationPolicyRule.WrongStorageSummary(1, 1));
        Assert.Equal("3 units are stored where the location does not take the component (marked below). 1 of them has expired: discard it (Discard expired). Move the rest, or correct the location record, following your storage procedure.",
            InventoryLocationPolicyRule.WrongStorageSummary(3, 1));
        Assert.Equal("2 units are stored where the location does not take the component (marked below). Move them, or correct the location record, following your storage procedure.",
            InventoryLocationPolicyRule.WrongStorageSummary(2, 0));
    }
}
