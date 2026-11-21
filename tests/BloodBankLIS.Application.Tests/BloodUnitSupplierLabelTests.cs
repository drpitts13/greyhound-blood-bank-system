using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Tests;

public class BloodUnitSupplierLabelTests
{
    [Fact]
    public void SupplierLabel_KeepsTheSupplierIdWhenTheDeliveryNamesABlankSupplier()
    {
        var blank = BloodUnitDto.From(Unit("   ", "   ")) with { BloodSupplierId = 9 };
        Assert.Equal("   ", blank.Supplier);
        Assert.Equal("   ", blank.CollectionFacility);
        Assert.Equal("Supplier 9", blank.SupplierLabel);
        Assert.DoesNotContain("ARC", blank.SupplierLabel!);

        var named = BloodUnitDto.From(Unit(" American Red Cross ", " West ")) with { BloodSupplierId = 9 };
        Assert.Equal("American Red Cross", named.SupplierLabel);
        Assert.Equal(" American Red Cross ", named.Supplier);
        Assert.Equal(" West ", named.CollectionFacility);

        var unknown = BloodUnitDto.From(Unit("   ", "   "));
        Assert.Null(unknown.SupplierLabel);
        Assert.Equal("   ", unknown.Supplier);
        Assert.Equal("   ", unknown.CollectionFacility);
    }

    // TEST-BB-1179: an Inventory search row names the patient an open reservation holds the unit for, "Name (MRN)",
    // else the MRN, else the patient id; with none, nothing (end-user testing pass 157).
    [Fact]
    public void WithReservation_NamesThePatient()
    {
        var row = BloodUnitDto.From(Unit("ARC", "West"));
        Assert.Null(row.WithReservation(null).ReservedForLabel);
        var named = row.WithReservation((13, "MRN0023", "Demo-Special, Avery"));
        Assert.Equal((13L, "Demo-Special, Avery (MRN0023)"), (named.ReservedForPatientId!.Value, named.ReservedForLabel));
        Assert.Equal("MRN0023", row.WithReservation((13, "MRN0023", null)).ReservedForLabel);
        Assert.Equal("patient 13", row.WithReservation((13, null, " ")).ReservedForLabel);
    }

    private static BloodUnit Unit(string supplier, string collectionFacility) => new()
    {
        Id = 4,
        UnitNumber = "U-CHART",
        Supplier = supplier,
        CollectionFacility = collectionFacility,
        DeliveryId = 8
    };
}
