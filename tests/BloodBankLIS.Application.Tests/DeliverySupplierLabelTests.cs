using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

public class DeliverySupplierLabelTests
{
    [Fact]
    public void SupplierNameLabel_KeepsTheSupplierIdWhenTheNameIsBlank()
    {
        var when = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var open = new OpenDeliveryDto(4, 9, "ARC", "   ", null, when, when, 1, 0);
        Assert.Equal("   ", open.SupplierName);
        Assert.Equal("ARC", open.SupplierCode);
        Assert.Equal("Supplier 9", open.SupplierNameLabel);
        Assert.DoesNotContain("ARC", open.SupplierNameLabel);

        var named = open with { SupplierName = " American Red Cross " };
        Assert.Equal("American Red Cross", named.SupplierNameLabel);
        Assert.Equal(" American Red Cross ", named.SupplierName);

        var detail = new DeliveryDetailDto(
            4, 9, "ARC", "   ", null, when, when, DeliveryStatus.Open, false, [], []);
        Assert.Equal("   ", detail.SupplierName);
        Assert.Equal("Supplier 9", detail.SupplierNameLabel);
        Assert.DoesNotContain("ARC", detail.SupplierNameLabel);
    }
}
