using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class ExpectedInboundSupplierLabelTests
{
    [Fact]
    public void SupplierLabel_KeepsTheSupplierIdWhenTheDeliveryNamesABlankSupplier()
    {
        var when = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var blank = ExpectedInboundWorkItemDto.From(Unit("   "), when) with { BloodSupplierId = 9 };
        Assert.Equal("   ", blank.Supplier);
        Assert.Equal("Supplier 9", blank.SupplierLabel);
        Assert.DoesNotContain("ARC", blank.SupplierLabel!);

        var named = ExpectedInboundWorkItemDto.From(Unit(" American Red Cross "), when) with { BloodSupplierId = 9 };
        Assert.Equal("American Red Cross", named.SupplierLabel);
        Assert.Equal(" American Red Cross ", named.Supplier);

        var unknown = ExpectedInboundWorkItemDto.From(Unit("   "), when);
        Assert.Null(unknown.SupplierLabel);
        Assert.Equal("   ", unknown.Supplier);
    }

    private static BloodUnit Unit(string supplier) => new()
    {
        Id = 4,
        UnitNumber = "U-EXP",
        Supplier = supplier,
        CollectionFacility = "   ",
        ExpiresUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
        Status = UnitStatus.Expected
    };
}
