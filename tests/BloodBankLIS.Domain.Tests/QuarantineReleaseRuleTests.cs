using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-406
public class QuarantineReleaseRuleTests
{
    [Fact]
    public void OrdinaryHolds_CanBeReleased()
    {
        foreach (var reason in new[] { UnitQuarantineReason.VisualDefect, UnitQuarantineReason.StorageTemperatureExcursion, UnitQuarantineReason.SupervisorReview })
        {
            var decision = QuarantineReleaseRule.Evaluate(reason, false, false, false);
            Assert.True(decision.CanRelease);
            Assert.Equal(RuleSeverity.Pass, decision.Result.Severity);
        }
    }

    [Fact]
    public void ReactionRemainder_WaitsForTheInvestigation()
    {
        Assert.False(QuarantineReleaseRule.Evaluate(UnitQuarantineReason.ReactionRemainder, true, false, false).CanRelease);
        Assert.True(QuarantineReleaseRule.Evaluate(UnitQuarantineReason.ReactionRemainder, false, false, false).CanRelease);
    }

    [Fact]
    public void Expired_RetypeMismatch_AndDisposal_AreNeverReleased_AndSayWhatToDo()
    {
        var expired = QuarantineReleaseRule.Evaluate(UnitQuarantineReason.VisualDefect, false, false, true);
        Assert.Equal((false, RuleSeverity.HardStop), (expired.CanRelease, expired.Result.Severity));
        Assert.Contains("discard", expired.NextStep, StringComparison.OrdinalIgnoreCase);

        var mismatch = QuarantineReleaseRule.Evaluate(UnitQuarantineReason.RetypeDiscrepancy, false, true, false);
        Assert.False(mismatch.CanRelease);
        Assert.Contains("supplier", mismatch.NextStep);

        Assert.False(QuarantineReleaseRule.Evaluate(UnitQuarantineReason.ReturnedForDisposal, false, false, false).CanRelease);
    }
}
