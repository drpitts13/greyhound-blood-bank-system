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
}
