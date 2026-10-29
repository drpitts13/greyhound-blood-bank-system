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

    private static BloodUnit Unit(string supplier, string collectionFacility) => new()
    {
        Id = 4,
        UnitNumber = "U-CHART",
        Supplier = supplier,
        CollectionFacility = collectionFacility,
        DeliveryId = 8
    };
}
