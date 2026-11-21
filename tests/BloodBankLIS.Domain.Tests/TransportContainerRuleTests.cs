using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-326: transport containers (none shipped) and MTP packing: a pack needs an active protocol, an active
// container, a plausible temperature, and eligible units; an out-of-range reading is recorded, not blocked.
public class TransportContainerRuleTests
{
    private static TransportContainer Cooler(decimal? min = 1m, decimal? max = 10m, int? hours = 4, bool active = true) =>
        new() { Code = "COOLER-L", Name = "Large cooler", MinTemperatureC = min, MaxTemperatureC = max, ValidatedHours = hours, IsActive = active };

    [Fact]
    public void ValidContainer_Passes() => Assert.False(TransportContainerRule.Validate(Cooler(), false).IsHardStopped);

    [Fact]
    public void ContainerWithoutRangeOrHours_Passes() =>
        Assert.False(TransportContainerRule.Validate(Cooler(null, null, null), false).IsHardStopped);

    [Theory]
    [InlineData(10, 1, null)]
    [InlineData(5, 5, null)]
    [InlineData(-50, 10, null)]
    [InlineData(1, 70, null)]
    [InlineData(1, 10, 0)]
    [InlineData(1, 10, 169)]
    public void BadRangeOrHours_IsRefused(double min, double max, int? hours) =>
        Assert.True(TransportContainerRule.Validate(Cooler((decimal)min, (decimal)max, hours), false).IsHardStopped);

    [Fact]
    public void DuplicateOrMissingCode_IsRefused()
    {
        Assert.True(TransportContainerRule.Validate(Cooler(), duplicateCode: true).IsHardStopped);
        var noCode = Cooler();
        noCode.Code = " ";
        Assert.True(TransportContainerRule.Validate(noCode, false).IsHardStopped);
    }

    [Theory]
    [InlineData(0.9, true)]
    [InlineData(1.0, false)]
    [InlineData(10.0, false)]
    [InlineData(10.1, true)]
    public void OutsideRange_IsInclusiveOfTheLimits(double t, bool outside) =>
        Assert.Equal(outside, TransportContainerRule.IsOutsideRange(Cooler(), (decimal)t));

    [Fact]
    public void NoRange_IsNeverOutside() =>
        Assert.False(TransportContainerRule.IsOutsideRange(Cooler(null, null), 40m));

    [Fact]
    public void Packing_AllGood_Passes() =>
        Assert.False(TransportContainerRule.EvaluatePacking(MtpActivationStatus.Active, Cooler(), 4m, 2, []).IsHardStopped);

    [Fact]
    public void Packing_OutOfRange_IsWarningNotStop()
    {
        var evaluation = TransportContainerRule.EvaluatePacking(MtpActivationStatus.Active, Cooler(), 12m, 2, []);
        Assert.False(evaluation.IsHardStopped);
        Assert.Contains(evaluation.Warnings, r => r.Code == TransportContainerRule.PackCode);
    }

    [Theory]
    [InlineData(MtpActivationStatus.Closed, true, 4.0, 1, false)]
    [InlineData(MtpActivationStatus.Active, false, 4.0, 1, false)]
    [InlineData(MtpActivationStatus.Active, true, null, 1, false)]
    [InlineData(MtpActivationStatus.Active, true, 99.0, 1, false)]
    [InlineData(MtpActivationStatus.Active, true, 4.0, 0, false)]
    [InlineData(MtpActivationStatus.Active, true, 4.0, 1, true)]
    public void Packing_Refusals(MtpActivationStatus status, bool activeContainer, double? temperature, int units, bool ineligible)
    {
        var evaluation = TransportContainerRule.EvaluatePacking(
            status, Cooler(active: activeContainer), (decimal?)temperature, units, ineligible ? ["W0001"] : []);
        Assert.True(evaluation.IsHardStopped);
    }

    [Fact]
    public void Packing_NoContainer_IsRefused() =>
        Assert.True(TransportContainerRule.EvaluatePacking(MtpActivationStatus.Active, null, 4m, 1, []).IsHardStopped);
}
