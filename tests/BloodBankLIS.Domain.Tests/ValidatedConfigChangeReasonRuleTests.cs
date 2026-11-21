using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-411
public class ValidatedConfigChangeReasonRuleTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("short", false)]
    [InlineData("Revalidated QA-17", true)]
    public void AReasonOfAtLeastEightCharacters_IsRequired_AndNamesWhatChanged(string? reason, bool ok)
    {
        var result = ValidatedConfigChangeReasonRule.Evaluate(reason, "a transport container");
        Assert.Equal(ok ? RuleSeverity.Pass : RuleSeverity.HardStop, result.Severity);
        if (!ok) Assert.Contains("a transport container", result.Message);
    }
}
