using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-323: units coming back to stock respect a pending retype.
public class RetypeStockStatusTests
{
    [Theory]
    [InlineData(false, false, UnitStatus.Available)]
    [InlineData(false, true, UnitStatus.Available)]
    [InlineData(true, true, UnitStatus.Available)]
    [InlineData(true, false, UnitStatus.Received)]
    public void ForStock(bool requiresRetype, bool confirmed, UnitStatus expected) =>
        Assert.Equal(expected, RetypeStockStatus.ForStock(requiresRetype, confirmed));

    [Theory]
    [InlineData(UnitStatus.Returned)]
    [InlineData(UnitStatus.Quarantine)]
    [InlineData(UnitStatus.OnHold)]
    public void BackToReceived_IsAnAllowedTransition(UnitStatus from) =>
        Assert.True(InventoryStatusTransition.IsAllowed(from, UnitStatus.Received));

    [Fact]
    public void IssuedUnit_CannotGoStraightToReceived() =>
        Assert.False(InventoryStatusTransition.IsAllowed(UnitStatus.Issued, UnitStatus.Received));
}
