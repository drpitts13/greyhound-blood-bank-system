using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-640: a unit is ready at the earlier of the first reservation and the first issue; ready by needed-by is Yes
// at or before the time, No after it, and blank without a time or a unit; minutes are whole and blank when a time is
// missing.
public class BloodOrderTurnaroundRuleTests
{
    [Fact]
    public void FirstReady_ReadyByNeededBy_AndMinutes()
    {
        var t0 = new DateTime(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc);
        Assert.Equal(t0.AddMinutes(10), BloodOrderTurnaroundRule.FirstReady(t0.AddMinutes(10), t0.AddMinutes(30)));
        Assert.Equal(t0.AddMinutes(5), BloodOrderTurnaroundRule.FirstReady(t0.AddMinutes(10), t0.AddMinutes(5)));
        Assert.Equal(t0, BloodOrderTurnaroundRule.FirstReady(null, t0));
        Assert.Null(BloodOrderTurnaroundRule.FirstReady(null, null));

        Assert.Equal("Yes", BloodOrderTurnaroundRule.ReadyByNeededBy(t0, t0));
        Assert.Equal("No", BloodOrderTurnaroundRule.ReadyByNeededBy(t0.AddMinutes(1), t0));
        Assert.Null(BloodOrderTurnaroundRule.ReadyByNeededBy(null, t0));
        Assert.Null(BloodOrderTurnaroundRule.ReadyByNeededBy(t0, null));

        Assert.Equal(90, BloodOrderTurnaroundRule.Minutes(t0, t0.AddMinutes(89.6)));
        Assert.Null(BloodOrderTurnaroundRule.Minutes(t0, null));
    }
}
