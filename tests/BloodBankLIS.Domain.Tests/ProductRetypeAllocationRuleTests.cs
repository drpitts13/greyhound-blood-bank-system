using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ProductRetypeAllocationRuleTests
{
    [Fact]
    public void ProductWithoutRetype_CanBeAllocated()
    {
        var result = ProductRetypeAllocationRule.Evaluate(requiresRetype: false, hasVerifiedMatchingRetype: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void VerifiedMatchingRetype_CanBeAllocated()
    {
        var result = ProductRetypeAllocationRule.Evaluate(requiresRetype: true, hasVerifiedMatchingRetype: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void MissingRetype_BlocksAllocation()
    {
        var result = ProductRetypeAllocationRule.Evaluate(requiresRetype: true, hasVerifiedMatchingRetype: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(ProductRetypeAllocationRule.Code, result.Code);
    }
}
