using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>End-user testing pass 85: when a hand-entered result was tested, if earlier than it is entered.</summary>
public class ResultTestedTimeRuleTests
{
    // TEST-BB-1098: no time, or a time between collection and now (with a minute's allowance), passes; a future time or
    // one before collection is a hard stop.
    [Fact]
    public void Evaluate_KeepsTheTimeBetweenCollectionAndNow()
    {
        var collected = new DateTime(2026, 11, 12, 6, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(RuleSeverity.Pass, ResultTestedTimeRule.Evaluate(null, collected, now).Severity);
        Assert.Equal(RuleSeverity.Pass, ResultTestedTimeRule.Evaluate(collected.AddHours(2), collected, now).Severity);
        Assert.Equal(RuleSeverity.Pass, ResultTestedTimeRule.Evaluate(now.AddSeconds(30), collected, now).Severity);

        var future = ResultTestedTimeRule.Evaluate(now.AddMinutes(5), collected, now);
        Assert.Equal((RuleSeverity.HardStop, ResultTestedTimeRule.Code), (future.Severity, future.Code));
        Assert.StartsWith("The time tested is before the specimen was collected",
            ResultTestedTimeRule.Evaluate(collected.AddMinutes(-5), collected, now).Message);
        Assert.StartsWith("Manual · tested ", ResultTestedTimeRule.SourceReference(collected));
        Assert.EndsWith(" (entered later)", ResultTestedTimeRule.SourceReference(collected));
    }
}
