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

    // TEST-BB-360: an expired compatible crossmatch no longer shows the unit as ready for issue.
    [Theory]
    [InlineData(true, CrossmatchResult.Compatible, false, ProductAllocationDisplayStatus.CrossmatchExpired)]
    [InlineData(true, CrossmatchResult.Compatible, true, ProductAllocationDisplayStatus.ReadyForIssue)]
    [InlineData(true, CrossmatchResult.Incompatible, false, ProductAllocationDisplayStatus.Exception)]
    [InlineData(true, null, false, ProductAllocationDisplayStatus.Reserved)]
    [InlineData(false, null, false, ProductAllocationDisplayStatus.ReadyForIssue)]
    public void Evaluate_AnExpiredCrossmatch_IsNotReadyForIssue(
        bool requiresCrossmatch,
        CrossmatchResult? xm,
        bool hasCurrentCompatible,
        ProductAllocationDisplayStatus expected)
    {
        Assert.Equal(expected, ProductAllocationDisplayStatusRule.Evaluate(requiresCrossmatch, xm, false, hasCurrentCompatible));
    }
}
