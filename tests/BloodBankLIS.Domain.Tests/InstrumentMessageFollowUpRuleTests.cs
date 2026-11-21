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

    // TEST-BB-537: only an open failure held for an instrument error code can be released, and only with a reason.
    [Fact]
    public void Release_NeedsAHeldOpenFailure_AndAReason()
    {
        const string held = "Instrument error Q (Hold): ABORH for ACC1 held for review, not posted (INST-HOLD).";
        Assert.Equal(RuleSeverity.Pass, InstrumentMessageFollowUpRule.EvaluateRelease(InstrumentMessageStatus.Failed, null, held, "QC reviewed").Severity);
        Assert.Contains("Say why", InstrumentMessageFollowUpRule.EvaluateRelease(InstrumentMessageStatus.Failed, null, held, "  ").Message);
        Assert.Contains("another reason", InstrumentMessageFollowUpRule.EvaluateRelease(
            InstrumentMessageStatus.Failed, null, "Instrument code X has no translation to a test (INST-CODE)", "x").Message);
        Assert.Contains("already resolved", InstrumentMessageFollowUpRule.EvaluateRelease(InstrumentMessageStatus.Failed, Now, held, "x").Message);
        Assert.Equal(RuleSeverity.HardStop, InstrumentMessageFollowUpRule.EvaluateRelease(InstrumentMessageStatus.Processed, null, held, "x").Severity);
        Assert.True(InstrumentMessageFollowUpRule.IsHeld(InstrumentMessageStatus.Failed, null, held));
    }
}
