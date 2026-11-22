using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-410
public class StorageLocationChangeReasonRuleTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    public void AReasonOfAtLeastEightCharacters_IsRequired(string? reason, bool ok) =>
        Assert.Equal(ok ? RuleSeverity.Pass : RuleSeverity.HardStop, StorageLocationChangeReasonRule.Evaluate(reason).Severity);
}
