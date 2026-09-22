using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BloodDeliveryValidatorTests
{
    private static readonly DateTime Received = new(2026, 9, 22, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Validate_RequiresSupplierAndAtLeastOneLine()
    {
        var result = BloodDeliveryValidator.Validate(
            Received.AddHours(-2),
            Received,
            packingListNumber: null,
            supplierMissing: true,
            supplierInactive: false,
            lines: []);

        Assert.Contains(result.Results, r => r.Code == "DELIVERY.SUPPLIER.REQUIRED");
        Assert.Contains(result.Results, r => r.Code == "DELIVERY.LINES.REQUIRED");
    }

    [Fact]
    public void Validate_RejectsOrderedAfterReceipt()
    {
        var result = BloodDeliveryValidator.Validate(
            Received.AddHours(2),
            Received,
            packingListNumber: null,
            supplierMissing: false,
            supplierInactive: false,
            lines: [(ComponentClass.RedBloodCells, 2)]);

        Assert.Contains(result.Results, r => r.Code == "DELIVERY.ORDERED.AFTER.RECEIPT");
    }

    [Fact]
    public void Validate_RejectsDuplicateProductType()
    {
        var result = BloodDeliveryValidator.Validate(
            Received.AddHours(-1),
            Received,
            packingListNumber: "PL-1",
            supplierMissing: false,
            supplierInactive: false,
            lines: [(ComponentClass.RedBloodCells, 2), (ComponentClass.RedBloodCells, 3)]);

        Assert.Contains(result.Results, r => r.Code == "DELIVERY.LINE.DUPLICATE");
    }
}
