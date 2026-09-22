using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BloodSupplierValidatorTests
{
    [Fact]
    public void Validate_RequiresCodeNameAndRegistration()
    {
        var supplier = new BloodSupplier { Code = " ", Name = "", Registration = "" };
        var result = BloodSupplierValidator.Validate(supplier, duplicateCode: false);

        Assert.True(result.IsHardStopped);
        Assert.Contains(result.Results, r => r.Code == "SUPPLIER.CODE.REQUIRED");
        Assert.Contains(result.Results, r => r.Code == "SUPPLIER.NAME.REQUIRED");
        Assert.Contains(result.Results, r => r.Code == "SUPPLIER.REGISTRATION.REQUIRED");
    }

    [Fact]
    public void Validate_AllowsMissingAddress()
    {
        var supplier = new BloodSupplier
        {
            Code = "ARC",
            Name = "American Red Cross",
            Registration = "FDA-1001",
            Address = null
        };

        var result = BloodSupplierValidator.Validate(supplier, duplicateCode: false);

        Assert.False(result.IsHardStopped);
    }

    [Fact]
    public void Validate_RejectsDuplicateCode()
    {
        var supplier = new BloodSupplier
        {
            Code = "ARC",
            Name = "American Red Cross",
            Registration = "FDA-1001"
        };

        var result = BloodSupplierValidator.Validate(supplier, duplicateCode: true);

        Assert.Contains(result.Results, r => r.Code == "SUPPLIER.CODE.DUPLICATE");
    }
}
