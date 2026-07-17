using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class InventoryStatusTransitionTests
{
    [Theory]
    [InlineData(UnitStatus.Quarantine, UnitStatus.Available)]
    [InlineData(UnitStatus.Available, UnitStatus.Allocated)]
    [InlineData(UnitStatus.Allocated, UnitStatus.Issued)]
    [InlineData(UnitStatus.Issued, UnitStatus.Transfused)]
    [InlineData(UnitStatus.Issued, UnitStatus.Returned)]
    [InlineData(UnitStatus.Returned, UnitStatus.Available)]
    [InlineData(UnitStatus.Quarantine, UnitStatus.Expired)]
    [InlineData(UnitStatus.Returned, UnitStatus.Expired)]
    [InlineData(UnitStatus.Expired, UnitStatus.Discarded)]
    public void AllowedTransitions_Pass(UnitStatus from, UnitStatus to)
    {
        Assert.True(InventoryStatusTransition.IsAllowed(from, to));
        Assert.Equal(RuleSeverity.Pass, InventoryStatusTransition.Evaluate(from, to).Severity);
    }

    [Theory]
    [InlineData(UnitStatus.Quarantine, UnitStatus.Issued)]
    [InlineData(UnitStatus.Available, UnitStatus.Transfused)]
    [InlineData(UnitStatus.Transfused, UnitStatus.Available)]
    [InlineData(UnitStatus.Discarded, UnitStatus.Available)]
    [InlineData(UnitStatus.Issued, UnitStatus.Available)]
    public void DisallowedTransitions_AreHardStop(UnitStatus from, UnitStatus to)
    {
        Assert.False(InventoryStatusTransition.IsAllowed(from, to));
        Assert.Equal(RuleSeverity.HardStop, InventoryStatusTransition.Evaluate(from, to).Severity);
    }

    [Fact]
    public void SameStatus_IsAllowed()
    {
        Assert.True(InventoryStatusTransition.IsAllowed(UnitStatus.Available, UnitStatus.Available));
    }
}
