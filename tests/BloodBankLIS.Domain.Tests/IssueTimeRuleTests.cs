using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-331: an issue time cannot be in the future; an earlier time is a late entry that needs a reason.
public class IssueTimeRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, null, false)]
    [InlineData(4, null, false)]     // within the clock allowance
    [InlineData(-4, null, false)]
    [InlineData(10, null, true)]     // future
    [InlineData(-180, null, true)]   // late, no reason
    [InlineData(-180, " ", true)]
    [InlineData(-180, "Issued on paper during downtime", false)]
    public void Evaluate(int minutesFromNow, string? reason, bool stopped) =>
        Assert.Equal(stopped, IssueTimeRule.Evaluate(Now.AddMinutes(minutesFromNow), Now, reason).Severity == RuleSeverity.HardStop);

    [Theory]
    [InlineData(-6, true)]
    [InlineData(-5, false)]
    [InlineData(0, false)]
    public void IsLateEntry_BeyondTheAllowance(int minutesFromNow, bool late) =>
        Assert.Equal(late, IssueTimeRule.IsLateEntry(Now.AddMinutes(minutesFromNow), Now));
}
