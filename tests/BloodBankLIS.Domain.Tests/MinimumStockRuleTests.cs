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
}
