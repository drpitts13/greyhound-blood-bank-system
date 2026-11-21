using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class MinimumStockRuleTests
{
    // TEST-BB-509: still to order is the shortfall less units on submitted orders, never below zero.
    [Theory]
    [InlineData(4, 0, 4)]
    [InlineData(4, 1, 3)]
    [InlineData(4, 4, 0)]
    [InlineData(2, 5, 0)]
    [InlineData(0, 3, 0)]
    [InlineData(-1, -2, 0)]
    public void StillToOrder_IsTheShortfallLessWhatIsOnOrder(int shortfall, int onOrder, int expected) =>
        Assert.Equal(expected, MinimumStockRule.StillToOrder(shortfall, onOrder));

    // TEST-BB-1119: a minimum names its group with Rh when set ("O Neg", "Rh Neg"), and a supplier order line counts
    // toward it only within that group (end-user testing pass 105).
    [Fact]
    public void GroupText_AndCovers_IncludeRh()
    {
        Assert.Equal("Any", MinimumStockRule.GroupText(null, null));
        Assert.Equal("any ABO", MinimumStockRule.GroupText(null, null, "any ABO"));
        Assert.Equal("O", MinimumStockRule.GroupText(AboGroup.O, null));
        Assert.Equal("O Neg", MinimumStockRule.GroupText(AboGroup.O, RhType.Negative));
        Assert.Equal("Rh Neg", MinimumStockRule.GroupText(null, RhType.Negative));

        Assert.True(MinimumStockRule.Covers(1, AboGroup.O, RhType.Negative, 1, AboGroup.O, RhType.Negative));
        Assert.False(MinimumStockRule.Covers(1, AboGroup.O, RhType.Negative, 1, AboGroup.O, RhType.Positive));
        Assert.False(MinimumStockRule.Covers(1, AboGroup.O, RhType.Negative, 1, AboGroup.O, null));
        Assert.True(MinimumStockRule.Covers(1, AboGroup.O, null, 1, AboGroup.O, RhType.Positive));
        Assert.False(MinimumStockRule.Covers(1, AboGroup.O, null, 2, AboGroup.O, null));
    }
}
