using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-404: one next step per reaction investigation, most urgent first.
public class ReactionNextStepRuleTests
{
    private static ReactionNextStepRule.Facts Facts(
        ReactionInvestigationStatus status = ReactionInvestigationStatus.Open,
        bool fatalityDue = false,
        bool held = true,
        string? holdNote = null,
        bool discrepancy = false,
        bool workupIncomplete = false,
        string? workupReason = null) =>
        new(status, fatalityDue, fatalityDue ? "CBER notification is due." : null, held, holdNote, discrepancy, workupIncomplete, workupReason);

    [Fact]
    public void FatalityFollowUp_LeadsEverything_EvenWhenClosed()
    {
        var step = ReactionNextStepRule.For(Facts(ReactionInvestigationStatus.Closed, fatalityDue: true, held: false));
        Assert.Equal(1, step.Priority);
        Assert.Equal("CBER notification is due.", step.Text);
    }

    [Fact]
    public void Steps_FollowTheUrgencyOrder()
    {
        Assert.Equal(2, ReactionNextStepRule.For(Facts(held: false, discrepancy: true, workupIncomplete: true)).Priority);
        Assert.Equal(3, ReactionNextStepRule.For(Facts(discrepancy: true, workupIncomplete: true)).Priority);
        var workup = ReactionNextStepRule.For(Facts(workupIncomplete: true, workupReason: "Post-transfusion DAT result has not been recorded."));
        Assert.Equal((4, "Post-transfusion DAT result has not been recorded."), (workup.Priority, workup.Text));
        Assert.Equal(5, ReactionNextStepRule.For(Facts()).Priority);
        Assert.Equal(ReactionNextStepRule.Done, ReactionNextStepRule.For(Facts(ReactionInvestigationStatus.Closed)).Priority);
    }

    [Fact]
    public void ARemainderThatCannotBeHeld_IsNotAskedToBeHeld()
    {
        // The unit was fully transfused or already gone: the hold note explains it, so the next step moves on.
        Assert.Equal(5, ReactionNextStepRule.For(Facts(held: false, holdNote: "Unit was fully transfused.")).Priority);
    }
}
