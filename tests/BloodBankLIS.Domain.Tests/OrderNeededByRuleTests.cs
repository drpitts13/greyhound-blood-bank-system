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

    // TEST-BB-647: needed soon only with a warning window set, while the time is ahead and within it (its last minute
    // included); a passed time is passed, not soon. Minutes until it are rounded up.
    [Fact]
    public void DueSoon_OnlyWithinTheWindow_AndAhead()
    {
        Assert.True(OrderNeededByRule.DueSoon(Ordered.AddMinutes(30), Ordered, 60));
        Assert.True(OrderNeededByRule.DueSoon(Ordered.AddMinutes(60), Ordered, 60));
        Assert.False(OrderNeededByRule.DueSoon(Ordered.AddMinutes(61), Ordered, 60));
        Assert.False(OrderNeededByRule.DueSoon(Ordered, Ordered, 60));
        Assert.False(OrderNeededByRule.DueSoon(Ordered.AddMinutes(-5), Ordered, 60));
        Assert.False(OrderNeededByRule.DueSoon(Ordered.AddMinutes(30), Ordered, 0));
        Assert.False(OrderNeededByRule.DueSoon(Ordered.AddMinutes(30), Ordered, -10));
        Assert.False(OrderNeededByRule.DueSoon(null, Ordered, 60));

        Assert.Equal(30, OrderNeededByRule.MinutesUntil(Ordered.AddMinutes(30), Ordered));
        Assert.Equal(1, OrderNeededByRule.MinutesUntil(Ordered.AddSeconds(10), Ordered));
        Assert.Null(OrderNeededByRule.MinutesUntil(null, Ordered));
    }

    // TEST-BB-653: validity that ends before a needed-by time still ahead is flagged; ending at or after it, a passed
    // needed-by, or an unknown time is not. The earlier of two validity ends is the one that counts.
    [Fact]
    public void ExpiresBeforeNeeded_AndEarliestEnd()
    {
        Assert.True(OrderNeededByRule.ExpiresBeforeNeeded(Ordered.AddDays(2), Ordered.AddDays(3), Ordered));
        Assert.False(OrderNeededByRule.ExpiresBeforeNeeded(Ordered.AddDays(3), Ordered.AddDays(3), Ordered));
        Assert.False(OrderNeededByRule.ExpiresBeforeNeeded(Ordered.AddDays(4), Ordered.AddDays(3), Ordered));
        Assert.False(OrderNeededByRule.ExpiresBeforeNeeded(Ordered.AddDays(-2), Ordered.AddDays(-1), Ordered));
        Assert.False(OrderNeededByRule.ExpiresBeforeNeeded(null, Ordered.AddDays(3), Ordered));
        Assert.False(OrderNeededByRule.ExpiresBeforeNeeded(Ordered.AddDays(2), null, Ordered));

        Assert.Equal(Ordered.AddDays(1), OrderNeededByRule.EarliestEnd(Ordered.AddDays(1), Ordered.AddDays(2)));
        Assert.Equal(Ordered.AddDays(1), OrderNeededByRule.EarliestEnd(Ordered.AddDays(3), Ordered.AddDays(1)));
        Assert.Equal(Ordered.AddDays(2), OrderNeededByRule.EarliestEnd(null, Ordered.AddDays(2)));
        Assert.Null(OrderNeededByRule.EarliestEnd(null, null));
    }
}
