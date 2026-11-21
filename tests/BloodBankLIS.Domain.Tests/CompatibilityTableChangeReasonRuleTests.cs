using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-382: changing a rule in the active compatibility table records why.
public class CompatibilityTableChangeReasonRuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("1234567")]
    public void ActiveTable_WithoutAReason_IsAHardStop(string? reason)
    {
        Assert.Equal(RuleSeverity.HardStop, CompatibilityTableChangeReasonRule.Evaluate(versionIsActive: true, reason).Severity);
    }

    [Fact]
    public void ActiveTableWithReason_OrDraftTable_Passes()
    {
        Assert.Equal(RuleSeverity.Pass, CompatibilityTableChangeReasonRule.Evaluate(versionIsActive: true, "12345678").Severity);
        Assert.Equal(RuleSeverity.Pass, CompatibilityTableChangeReasonRule.Evaluate(versionIsActive: false, null).Severity);
    }
}
