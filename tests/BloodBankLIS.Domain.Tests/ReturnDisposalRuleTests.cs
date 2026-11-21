using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-340
public class ReturnDisposalRuleTests
{
    [Theory]
    [InlineData(true, false, UnitQuarantineReason.ReturnedForDisposal)]
    [InlineData(true, true, UnitQuarantineReason.FailedReturnInspection)]
    [InlineData(false, true, UnitQuarantineReason.FailedReturnInspection)]
    [InlineData(false, false, UnitQuarantineReason.FailedReturnInspection)]
    public void QuarantineReason_SaysWhyTheUnitIsHeld(bool disposal, bool failed, UnitQuarantineReason expected) =>
        Assert.Equal(expected, ReturnDisposalRule.QuarantineReason(disposal, failed));

    [Fact]
    public void Notice_IsAWarningPointingToTheCodedDiscard()
    {
        var r = ReturnDisposalRule.Notice("W1", disposalRequested: true, inspectionFailed: false);
        Assert.Equal(RuleSeverity.Warning, r.Severity);
        Assert.Contains("discard it on the unit page with a discard reason", r.Message);
        Assert.DoesNotContain("failed", r.Message);
    }

    [Fact]
    public void Notice_ForFailedInspection_SaysSo() =>
        Assert.Contains("failed return inspection", ReturnDisposalRule.Notice("W1", false, true).Message);

    [Fact]
    public void QuarantinedUnit_CanStillBeDiscarded() =>
        Assert.NotEqual(RuleSeverity.HardStop, InventoryStatusTransition.Evaluate(UnitStatus.Quarantine, UnitStatus.Discarded).Severity);
}
