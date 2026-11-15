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
}
