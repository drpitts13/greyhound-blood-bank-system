using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-381: changing an override exception records why.
public class ExceptionCatalogChangeReasonRuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567")]
    [InlineData("  short  ")]
    public void MissingOrShortReason_IsAHardStop(string? reason)
    {
        var result = ExceptionCatalogChangeReasonRule.Evaluate(reason);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ExceptionCatalogChangeReasonRule.Code, result.Code);
    }

    [Fact]
    public void EightCharactersAfterTrimming_Passes()
    {
        Assert.Equal(RuleSeverity.Pass, ExceptionCatalogChangeReasonRule.Evaluate(" 12345678 ").Severity);
    }
}
