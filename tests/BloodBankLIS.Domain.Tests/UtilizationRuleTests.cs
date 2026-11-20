using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-298: crossmatch-to-transfusion arithmetic.
public class UtilizationRuleTests
{
    [Theory]
    [InlineData(TransfusionDisposition.Completed, true)]
    [InlineData(TransfusionDisposition.Stopped, true)]
    [InlineData(TransfusionDisposition.Wasted, false)]
    [InlineData(TransfusionDisposition.Returned, false)]
    public void CountsAsTransfused_OnlyCompletedOrStopped(TransfusionDisposition disposition, bool expected)
    {
        Assert.Equal(expected, UtilizationRule.CountsAsTransfused(disposition));
    }

    [Fact]
    public void Ratio_IsCrossmatchedOverTransfused_RoundedToTwoPlaces()
    {
        Assert.Equal(2.00m, UtilizationRule.CrossmatchToTransfusionRatio(4, 2));
        Assert.Equal(1.33m, UtilizationRule.CrossmatchToTransfusionRatio(4, 3));
        Assert.Equal(1.67m, UtilizationRule.CrossmatchToTransfusionRatio(5, 3));
        Assert.Null(UtilizationRule.CrossmatchToTransfusionRatio(3, 0));
    }

    [Fact]
    public void Percent_RoundsToOnePlace_AndIsNullWithNoBase()
    {
        Assert.Equal(66.7m, UtilizationRule.Percent(2, 3));
        Assert.Equal(100.0m, UtilizationRule.Percent(3, 3));
        Assert.Equal(0.0m, UtilizationRule.Percent(0, 3));
        Assert.Null(UtilizationRule.Percent(1, 0));
    }

    // TEST-BB-1381 (end-user testing pass 342): a line is above the facility's C:T target when its ratio exceeds it or
    // units were crossmatched and none transfused; with no target set nothing is above.
    [Fact]
    public void AboveCtTarget_FollowsTheTarget()
    {
        Assert.Equal(2.0m, UtilizationRule.CtTargetFromTenths(20));
        Assert.Null(UtilizationRule.CtTargetFromTenths(0));
        Assert.True(UtilizationRule.AboveCtTarget(7, 2, 2.0m));
        Assert.False(UtilizationRule.AboveCtTarget(4, 2, 2.0m));
        Assert.True(UtilizationRule.AboveCtTarget(1, 0, 2.0m));
        Assert.False(UtilizationRule.AboveCtTarget(0, 0, 2.0m));
        Assert.False(UtilizationRule.AboveCtTarget(7, 0, null));
    }
}
