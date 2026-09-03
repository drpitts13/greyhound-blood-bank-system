using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class InTransitPendingRuleTests
{
    [Fact]
    public void IssuedWithoutWardReceipt_IsPending()
    {
        Assert.True(InTransitPendingRule.IsPending(IssueStatus.Issued, wardReceivedUtc: null));
    }

    [Fact]
    public void WardReceived_IsNotPending()
    {
        Assert.False(InTransitPendingRule.IsPending(IssueStatus.Issued, DateTime.UtcNow));
    }

    [Fact]
    public void Returned_IsNotPending()
    {
        Assert.False(InTransitPendingRule.IsPending(IssueStatus.Returned, wardReceivedUtc: null));
    }

    [Fact]
    public void BeforeDue_IsPass()
    {
        var now = DateTime.UtcNow;
        var result = InTransitPendingRule.EvaluateOverdue(now.AddHours(1), now);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void AfterDue_IsWarning()
    {
        var now = DateTime.UtcNow;
        var result = InTransitPendingRule.EvaluateOverdue(now.AddHours(-1), now);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(InTransitPendingRule.Code, result.Code);
    }
}
