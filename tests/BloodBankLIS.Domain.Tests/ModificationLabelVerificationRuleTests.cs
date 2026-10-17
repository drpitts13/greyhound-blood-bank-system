using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ModificationLabelVerificationRuleTests
{
    [Fact]
    public void OtherQuarantineRelease_DoesNotRequireAScan()
    {
        var result = ModificationLabelVerificationRule.Evaluate(false, "W123", null);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void ModificationResult_BlankOrDifferentScan_IsHardStop()
    {
        var blank = ModificationLabelVerificationRule.Evaluate(true, "W123", "  ");
        var different = ModificationLabelVerificationRule.Evaluate(true, "W123", "W999");
        Assert.Equal(ModificationLabelVerificationRule.Code, blank.Code);
        Assert.Equal(RuleSeverity.HardStop, blank.Severity);
        Assert.Equal(RuleSeverity.HardStop, different.Severity);
    }

    [Fact]
    public void ModificationResult_MatchingScan_Passes()
    {
        var result = ModificationLabelVerificationRule.Evaluate(true, "W123", " w123 ");
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
