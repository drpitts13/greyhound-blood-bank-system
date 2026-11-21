using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-393 (OCD-056)
public class SpecimenReceiptScanRuleListTests
{
    [Fact]
    public void ListReceipt_IsAHardStopOnlyWhenScanIsRequired()
    {
        Assert.Equal(RuleSeverity.Pass, SpecimenReceiptScanRule.EvaluateListReceipt(false).Severity);
        var refused = SpecimenReceiptScanRule.EvaluateListReceipt(true);
        Assert.Equal(RuleSeverity.HardStop, refused.Severity);
        Assert.Equal(SpecimenReceiptScanRule.RequiredCode, refused.Code);
        Assert.Contains("scanning the tube", refused.Message);
    }

    [Fact]
    public void Setting_IsInTheCatalog_OffByDefault()
    {
        Assert.False(FacilityPolicyCatalog.BoolDefault(FacilityPolicyKeys.RequireSpecimenReceiptByScan));
        Assert.Contains("from its row", SpecimenReceiptScanRule.Ambiguous("X").Message);
        Assert.DoesNotContain("from its row", SpecimenReceiptScanRule.Ambiguous("X", scanRequired: true).Message);
    }
}
