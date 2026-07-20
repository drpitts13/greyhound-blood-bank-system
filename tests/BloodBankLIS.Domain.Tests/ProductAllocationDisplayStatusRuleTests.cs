using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ProductAllocationDisplayStatusRuleTests
{
    [Theory]
    [InlineData(true, null, false, ProductAllocationDisplayStatus.Reserved)]
    [InlineData(true, CrossmatchResult.NotPerformed, false, ProductAllocationDisplayStatus.Reserved)]
    [InlineData(true, CrossmatchResult.Compatible, false, ProductAllocationDisplayStatus.ReadyForIssue)]
    [InlineData(true, CrossmatchResult.Incompatible, false, ProductAllocationDisplayStatus.Exception)]
    [InlineData(false, null, false, ProductAllocationDisplayStatus.ReadyForIssue)]
    [InlineData(false, null, true, ProductAllocationDisplayStatus.Exception)]
    [InlineData(true, CrossmatchResult.Compatible, true, ProductAllocationDisplayStatus.Exception)]
    public void Evaluate_MapsExpectedStatus(
        bool requiresCrossmatch,
        CrossmatchResult? xm,
        bool hasException,
        ProductAllocationDisplayStatus expected)
    {
        var status = ProductAllocationDisplayStatusRule.Evaluate(requiresCrossmatch, xm, hasException);
        Assert.Equal(expected, status);
    }
}
