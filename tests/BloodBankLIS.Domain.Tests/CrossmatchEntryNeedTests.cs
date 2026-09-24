using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class CrossmatchEntryNeedTests
{
    [Fact]
    public void ReservedRedCell_WithoutResult_NeedsEntry()
    {
        Assert.True(CrossmatchEntryNeed.ForAllocation(
            requiresCrossmatch: true,
            result: null,
            testCode: null,
            AllocationStatus.Reserved,
            issueId: null));
    }

    [Fact]
    public void KnownSerologicTest_StillNeedsEntryUntilResulted()
    {
        Assert.True(CrossmatchEntryNeed.ForAllocation(
            true, null, "XM", AllocationStatus.Reserved, null));
    }

    [Theory]
    [InlineData(false, null, null)]
    [InlineData(true, CrossmatchResult.Compatible, null)]
    [InlineData(true, CrossmatchResult.Incompatible, "CXM")]
    [InlineData(true, null, "EXM")]
    public void HidesEntryWhenNoManualCrossmatchIsNeeded(
        bool requiresCrossmatch,
        CrossmatchResult? result,
        string? testCode)
    {
        Assert.False(CrossmatchEntryNeed.ForAllocation(
            requiresCrossmatch, result, testCode, AllocationStatus.Reserved, null));
    }

    [Fact]
    public void IssuedUnit_DoesNotNeedEntry()
    {
        Assert.False(CrossmatchEntryNeed.ForAllocation(
            true, null, "XM", AllocationStatus.Reserved, issueId: 4));
    }
}
