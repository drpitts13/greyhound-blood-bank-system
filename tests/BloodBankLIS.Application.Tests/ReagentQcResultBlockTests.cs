using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-495: the bench is told resulting is blocked exactly when Save would stop it (policy on and QC work open),
// with the open work listed.
public class ReagentQcResultBlockTests
{
    private static readonly ReagentQcWorkItemDto[] Open = [new(ReagentQcWorklistRule.Overdue, 1, "LOT-7", null, "Daily QC not recorded today.")];

    [Fact]
    public void Blocked_OnlyWhenPolicyOnAndWorkOpen()
    {
        Assert.Null(ReagentQcResultBlock.For(policyOn: false, Open));
        Assert.Null(ReagentQcResultBlock.For(policyOn: true, []));

        var block = ReagentQcResultBlock.For(policyOn: true, Open);
        Assert.NotNull(block);
        Assert.Equal(ReagentQcResultGate.Evaluate(true, true).Message, block!.Message);
        Assert.Equal("LOT-7", Assert.Single(block.Work).LotNumber);
    }

    // End-user testing pass 64: the alert title read "Reagent QC reviewresults"; kinds read as words.
    [Fact]
    public void KindText_ReadsAsWords()
    {
        Assert.Equal("results to review", ReagentQcWorklistRule.KindText(ReagentQcWorklistRule.ReviewResults));
        Assert.Equal("overdue", ReagentQcWorklistRule.KindText(ReagentQcWorklistRule.Overdue));
        Assert.Equal("unreviewed", ReagentQcWorklistRule.KindText(ReagentQcWorklistRule.Unreviewed));
    }
}
