using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class SpecimenRelabelRuleTests
{
    [Theory]
    [InlineData(SpecimenStatus.Collected)]
    [InlineData(SpecimenStatus.Received)]
    [InlineData(SpecimenStatus.Accepted)]
    public void Status_Usable_Passes(SpecimenStatus status)
    {
        Assert.Equal(RuleSeverity.Pass, SpecimenRelabelRule.EvaluateStatus(status).Severity);
    }

    [Theory]
    [InlineData(SpecimenStatus.Rejected)]
    [InlineData(SpecimenStatus.Cancelled)]
    [InlineData(SpecimenStatus.Expired)]
    public void Status_Closed_IsHardStop(SpecimenStatus status)
    {
        var result = SpecimenRelabelRule.EvaluateStatus(status);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenRelabelRule.StatusCode, result.Code);
    }

    [Fact]
    public void Barcode_Blank_IsHardStop()
    {
        var result = SpecimenRelabelRule.EvaluateBarcode("  ");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenRelabelRule.BarcodeCode, result.Code);
    }

    [Fact]
    public void Reason_Blank_IsHardStop()
    {
        var result = SpecimenRelabelRule.EvaluateReason(null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenRelabelRule.ReasonCode, result.Code);
    }

    [Fact]
    public void Change_SameBarcode_IsHardStop()
    {
        var result = SpecimenRelabelRule.EvaluateChange("SPC-1", "SPC-1");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SpecimenRelabelRule.UnchangedCode, result.Code);
    }

    [Fact]
    public void Change_DifferentBarcode_Passes()
    {
        Assert.Equal(RuleSeverity.Pass, SpecimenRelabelRule.EvaluateChange(null, "SPC-2").Severity);
    }
}
