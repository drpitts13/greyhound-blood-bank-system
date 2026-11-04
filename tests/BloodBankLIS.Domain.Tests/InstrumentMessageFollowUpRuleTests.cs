using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-400
public class InstrumentMessageFollowUpRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(InstrumentMessageStatus.Failed, false, RuleSeverity.Pass)]
    [InlineData(InstrumentMessageStatus.Failed, true, RuleSeverity.HardStop)]
    [InlineData(InstrumentMessageStatus.Processed, false, RuleSeverity.HardStop)]
    [InlineData(InstrumentMessageStatus.Replayed, false, RuleSeverity.HardStop)]
    [InlineData(InstrumentMessageStatus.Received, false, RuleSeverity.HardStop)]
    public void OnlyAnOpenFailure_CanBeReplayed(InstrumentMessageStatus status, bool resolved, RuleSeverity expected) =>
        Assert.Equal(expected, InstrumentMessageFollowUpRule.EvaluateReplay(status, resolved ? Now : null).Severity);

    [Fact]
    public void Resolve_NeedsAnOpenFailure_AndAReason()
    {
        Assert.Equal(RuleSeverity.Pass, InstrumentMessageFollowUpRule.EvaluateResolve(InstrumentMessageStatus.Failed, null, "Entered by hand").Severity);
        Assert.Equal(RuleSeverity.HardStop, InstrumentMessageFollowUpRule.EvaluateResolve(InstrumentMessageStatus.Failed, null, " ").Severity);
        Assert.Contains("already", InstrumentMessageFollowUpRule.EvaluateResolve(InstrumentMessageStatus.Failed, Now, "x").Message);
        Assert.Equal(RuleSeverity.HardStop, InstrumentMessageFollowUpRule.EvaluateResolve(InstrumentMessageStatus.Processed, null, "x").Severity);
    }
}
