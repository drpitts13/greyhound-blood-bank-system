using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-643: the history opens for the one order whose number matches (case and spaces ignored); none for no match,
// a blank number, or two orders with the same number.
public class OrderHistoryLinkRuleTests
{
    [Fact]
    public void HistoryToOpen_OnlyForTheOneExactMatch()
    {
        var orders = new[] { (1L, "ORD0023"), (2L, "ORD00231"), (3L, "DUP-1"), (4L, "dup-1") };
        Assert.Equal(1L, OrderHistoryLinkRule.HistoryToOpen(orders, " ord0023 "));
        Assert.Null(OrderHistoryLinkRule.HistoryToOpen(orders, "ORD002"));
        Assert.Null(OrderHistoryLinkRule.HistoryToOpen(orders, "DUP-1"));
        Assert.Null(OrderHistoryLinkRule.HistoryToOpen(orders, "  "));
        Assert.Null(OrderHistoryLinkRule.HistoryToOpen([], "ORD0023"));
    }
}
