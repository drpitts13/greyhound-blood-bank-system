using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ChargeBillingValidatorTests
{
    [Fact]
    public void ChargeCode_RejectsMissingCodeAndDescription()
    {
        var code = new ChargeCode { Code = "", Description = "", DefaultAmount = 1m };
        var result = ChargeCodeValidator.Validate(code, duplicateCode: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "CHARGE.CODE.REQUIRED");
        Assert.Contains(result.HardStops, r => r.Code == "CHARGE.DESCRIPTION.REQUIRED");
    }

    [Fact]
    public void ChargeCode_RejectsNegativeAmount()
    {
        var code = new ChargeCode { Code = "BB-X", Description = "X", DefaultAmount = -1m };
        var result = ChargeCodeValidator.Validate(code, duplicateCode: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "CHARGE.AMOUNT.NEGATIVE");
    }

    [Fact]
    public void ChargeCode_RejectsDuplicate()
    {
        var code = new ChargeCode { Code = "BB-X", Description = "X", DefaultAmount = 1m };
        var result = ChargeCodeValidator.Validate(code, duplicateCode: true);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "CHARGE.CODE.DUPLICATE");
    }

    [Fact]
    public void ChargeCode_AcceptsValid()
    {
        var code = new ChargeCode { Code = "BB-X", Description = "X", DefaultAmount = 0m };
        var result = ChargeCodeValidator.Validate(code, duplicateCode: false);
        Assert.False(result.IsHardStopped);
    }

    [Fact]
    public void ChargeCode_RejectsNonNumericRevenueCode()
    {
        var code = new ChargeCode { Code = "BB-X", Description = "X", DefaultAmount = 1m, RevenueCode = "AB" };
        var result = ChargeCodeValidator.Validate(code, duplicateCode: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "CHARGE.REVENUE.FORMAT");
    }

    [Fact]
    public void ChargeCode_AcceptsFourDigitRevenueAndModifier()
    {
        var code = new ChargeCode { Code = "BB-X", Description = "X", DefaultAmount = 1m, RevenueCode = "0381", Modifier = "26" };
        var result = ChargeCodeValidator.Validate(code, duplicateCode: false);
        Assert.False(result.IsHardStopped);
    }

    [Fact]
    public void ChargeRule_RejectsMissingChargeCode()
    {
        var rule = new ChargeRule { TriggerType = BillingTriggerType.TestVerified, ChargeCodeId = 0 };
        var result = ChargeRuleValidator.Validate(rule, chargeCodeMissing: true, duplicateActive: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "CHARGE.RULE.CODE.REQUIRED");
    }

    [Fact]
    public void ChargeRule_RejectsDuplicateActive()
    {
        var rule = new ChargeRule
        {
            TriggerType = BillingTriggerType.TestVerified,
            TriggerKey = "ABORH",
            ChargeCodeId = 1
        };
        var result = ChargeRuleValidator.Validate(rule, chargeCodeMissing: false, duplicateActive: true);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == "CHARGE.RULE.DUPLICATE");
    }

    [Fact]
    public void ChargeRule_AcceptsValid()
    {
        var rule = new ChargeRule { TriggerType = BillingTriggerType.UnitIssued, ChargeCodeId = 2 };
        var result = ChargeRuleValidator.Validate(rule, chargeCodeMissing: false, duplicateActive: false);
        Assert.False(result.IsHardStopped);
    }
}
