using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-629: a needed-by time is optional and not before the order was placed (a minute of clock skew allowed); it has
// passed once now reaches it.
public class OrderNeededByRuleTests
{
    private static readonly DateTime Ordered = new(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Evaluate_AndPassed()
    {
        Assert.Equal(RuleSeverity.Pass, OrderNeededByRule.Evaluate(Ordered, null).Severity);
        Assert.Equal(RuleSeverity.Pass, OrderNeededByRule.Evaluate(Ordered, Ordered.AddHours(4)).Severity);
        Assert.Equal(RuleSeverity.Pass, OrderNeededByRule.Evaluate(Ordered, Ordered.AddSeconds(-30)).Severity);
        Assert.Contains("before the order was placed", OrderNeededByRule.Evaluate(Ordered, Ordered.AddHours(-1)).Message);

        Assert.True(OrderNeededByRule.Passed(Ordered, Ordered));
        Assert.False(OrderNeededByRule.Passed(Ordered.AddMinutes(1), Ordered));
        Assert.False(OrderNeededByRule.Passed(null, Ordered));
    }
}
