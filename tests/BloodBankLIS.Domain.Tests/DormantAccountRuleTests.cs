using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-479: an active person's account not signed in within the chosen days (or never) is dormant; inactive and
// service accounts are not, and a zero day count flags nothing.
public class DormantAccountRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(true, false, 100, 90, true)]
    [InlineData(true, false, 30, 90, false)]
    [InlineData(true, false, null, 90, true)]
    [InlineData(false, false, 400, 90, false)]
    [InlineData(true, true, 400, 90, false)]
    [InlineData(true, false, 400, 0, false)]
    public void Dormant_WhenActivePersonNotSignedInWithinTheDays(bool active, bool service, int? daysAgo, int days, bool expected) =>
        Assert.Equal(expected, DormantAccountRule.IsDormant(active, service, daysAgo is int d ? Now.AddDays(-d) : null, Now, days));
}
