using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-346
public class TransfusionCompletionRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 18, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-7, 6, true)]
    [InlineData(-6, 6, true)]
    [InlineData(-5, 6, false)]
    [InlineData(-30, 0, false)]
    public void IsOverdue_AfterTheReminderHours_AndNeverWhenOff(int startedHoursAgo, int hours, bool expected) =>
        Assert.Equal(expected, TransfusionCompletionRule.IsOverdue(Now.AddHours(startedHoursAgo), Now, hours));

    [Fact]
    public void IsOverdue_WithoutAStart_IsFalse() =>
        Assert.False(TransfusionCompletionRule.IsOverdue(null, Now, 6));

    [Fact]
    public void Alert_IsAWarningKeyedByIssue_LinkingToComplete()
    {
        var a = TransfusionCompletionRule.Alert(7, "W1", "MRN1", Now.AddHours(-7));
        Assert.Equal(AlertSeverity.Warning, a.Severity);
        Assert.Equal("TXINCOMPLETE:ISSUE-7", a.Key);
        Assert.Equal("/issuing?desk=transfuse&issueId=7", a.Link);
        Assert.Contains("MRN MRN1", a.Message);
    }
}
