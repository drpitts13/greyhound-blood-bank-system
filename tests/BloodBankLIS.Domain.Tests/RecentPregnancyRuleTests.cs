using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class RecentPregnancyRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Dob = new(1992, 4, 9);

    // TEST-BB-1160: a pregnancy date may be today or earlier, not in the future or before birth; a date chosen on screen
    // is recorded as that day (now for today) (end-user testing pass 138).
    [Fact]
    public void Evaluate_AllowsAPastDate_RefusesFutureOrBeforeBirth()
    {
        Assert.Equal(RuleSeverity.Pass, RecentPregnancyRule.Evaluate(null, Dob, Now).Severity);
        Assert.Equal(RuleSeverity.Pass, RecentPregnancyRule.Evaluate(Now.AddDays(-42), Dob, Now).Severity);
        Assert.Equal(RuleSeverity.Pass, RecentPregnancyRule.Evaluate(Now, Dob, Now).Severity);
        Assert.Equal(RuleSeverity.HardStop, RecentPregnancyRule.Evaluate(Now.AddDays(3), Dob, Now).Severity);
        Assert.Equal(RuleSeverity.HardStop, RecentPregnancyRule.Evaluate(new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc), Dob, Now).Severity);
    }

    [Fact]
    public void ToUtc_IsNowForToday_AndTheStartOfAnEarlierLocalDay()
    {
        var today = DateOnly.FromDateTime(Now.ToLocalTime());
        Assert.Equal(Now, RecentPregnancyRule.ToUtc(today, Now));
        var earlier = today.AddDays(-42);
        var utc = RecentPregnancyRule.ToUtc(earlier, Now);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(earlier, DateOnly.FromDateTime(utc.ToLocalTime()));
        Assert.Equal(TimeSpan.Zero, utc.ToLocalTime().TimeOfDay);
    }
}
