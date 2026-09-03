using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class QuarantineReleaseVerifierRuleTests
{
    [Fact]
    public void NotRequired_PassesWithoutVerifier()
    {
        var result = QuarantineReleaseVerifierRule.Evaluate("tech1", null, required: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void RequiredMissing_IsHardStop()
    {
        var result = QuarantineReleaseVerifierRule.Evaluate("tech1", null, required: true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(QuarantineReleaseVerifierRule.Code, result.Code);
    }

    [Fact]
    public void SameUser_IsHardStop()
    {
        var result = QuarantineReleaseVerifierRule.Evaluate("tech1", "TECH1", required: true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void DistinctUser_Passes()
    {
        var result = QuarantineReleaseVerifierRule.Evaluate("tech1", "tech2", required: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
