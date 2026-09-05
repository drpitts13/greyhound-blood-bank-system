using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BillingCatalogValidatorTests
{
    [Fact]
    public void TestService_RejectsMissingChargeCode()
    {
        var row = new TestServiceBilling { ChargeCodeId = 0, TestCode = "ABORH", Trigger = BillingTriggerType.TestVerified };
        var result = TestServiceBillingValidator.Validate(row, chargeCodeMissing: true, duplicateActive: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "TESTBILL.CODE.REQUIRED");
    }

    [Fact]
    public void TestService_RejectsDuplicate()
    {
        var row = new TestServiceBilling { ChargeCodeId = 1, TestCode = "ABORH", Trigger = BillingTriggerType.TestVerified };
        var result = TestServiceBillingValidator.Validate(row, chargeCodeMissing: false, duplicateActive: true);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "TESTBILL.DUPLICATE");
    }

    [Fact]
    public void TestService_AcceptsValid()
    {
        var row = new TestServiceBilling { ChargeCodeId = 1, TestCode = "ABORH", Trigger = BillingTriggerType.TestVerified };
        var result = TestServiceBillingValidator.Validate(row, chargeCodeMissing: false, duplicateActive: false);
        Assert.False(result.IsHardStopped);
    }

    [Fact]
    public void Product_RejectsMissingChargeCode()
    {
        var row = new ProductBilling { ChargeCodeId = 0, IsbtProductCode = "E0336", Trigger = BillingTriggerType.UnitIssued };
        var result = ProductBillingValidator.Validate(row, chargeCodeMissing: true, duplicateActive: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "PRODBILL.CODE.REQUIRED");
    }

    [Fact]
    public void Product_RejectsIsbtTooLong()
    {
        var row = new ProductBilling { ChargeCodeId = 1, IsbtProductCode = "E0336X", Trigger = BillingTriggerType.UnitIssued };
        var result = ProductBillingValidator.Validate(row, chargeCodeMissing: false, duplicateActive: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "PRODBILL.ISBT.LENGTH");
    }

    [Fact]
    public void Product_AcceptsValid()
    {
        var row = new ProductBilling { ChargeCodeId = 1, IsbtProductCode = "E0336", Trigger = BillingTriggerType.UnitIssued };
        var result = ProductBillingValidator.Validate(row, chargeCodeMissing: false, duplicateActive: false);
        Assert.False(result.IsHardStopped);
    }

    [Fact]
    public void Product_AcceptsUnitTransfused()
    {
        var row = new ProductBilling { ChargeCodeId = 1, IsbtProductCode = "E0336", Trigger = BillingTriggerType.UnitTransfused };
        var result = ProductBillingValidator.Validate(row, chargeCodeMissing: false, duplicateActive: false);
        Assert.False(result.IsHardStopped);
    }

    [Fact]
    public void Product_RejectsTestVerifiedTrigger()
    {
        var row = new ProductBilling { ChargeCodeId = 1, IsbtProductCode = "E0336", Trigger = BillingTriggerType.TestVerified };
        var result = ProductBillingValidator.Validate(row, chargeCodeMissing: false, duplicateActive: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "PRODBILL.TRIGGER.INVALID");
    }
}
