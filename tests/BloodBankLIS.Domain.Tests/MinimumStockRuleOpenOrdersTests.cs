using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class MinimumStockRuleOpenOrdersTests
{
    [Fact]
    public void OpenOrdersExceedOnHand_OnlyWhenThereAreMoreOrdersThanUnits()
    {
        Assert.True(MinimumStockRule.OpenOrdersExceedOnHand(onHand: 1, openOrders: 2));
        Assert.True(MinimumStockRule.OpenOrdersExceedOnHand(onHand: 0, openOrders: 1));
        Assert.False(MinimumStockRule.OpenOrdersExceedOnHand(onHand: 2, openOrders: 2));
        Assert.False(MinimumStockRule.OpenOrdersExceedOnHand(onHand: 0, openOrders: 0));
    }
}
