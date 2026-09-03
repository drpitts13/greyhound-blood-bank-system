using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class RetrospectiveCrossmatchPendingRuleTests
{
    [Fact]
    public void IncompleteUncrossmatchedIssued_IsPending()
    {
        Assert.True(RetrospectiveCrossmatchPendingRule.IsPending(
            true, CrossmatchClinicalStatus.NotCrossmatchedEmergency, null, IssueStatus.Issued));
    }

    [Fact]
    public void CompatibleOrCompleted_IsNotPending()
    {
        Assert.False(RetrospectiveCrossmatchPendingRule.IsPending(
            true, CrossmatchClinicalStatus.Compatible, null, IssueStatus.Issued));
        Assert.False(RetrospectiveCrossmatchPendingRule.IsPending(
            true, CrossmatchClinicalStatus.NotCrossmatchedEmergency, new DateTime(2026, 9, 3), IssueStatus.Issued));
    }

    [Fact]
    public void Returned_IsNotPending()
    {
        Assert.False(RetrospectiveCrossmatchPendingRule.IsPending(
            true, CrossmatchClinicalStatus.NotCrossmatchedEmergency, null, IssueStatus.Returned));
    }

    [Fact]
    public void Overdue_IsWarning()
    {
        var due = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);
        var now = due.AddHours(1);
        var result = RetrospectiveCrossmatchPendingRule.EvaluateOverdue(due, now);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(RetrospectiveCrossmatchPendingRule.Code, result.Code);
    }

    [Fact]
    public void NotYetDue_Passes()
    {
        var due = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(RuleSeverity.Pass, RetrospectiveCrossmatchPendingRule.EvaluateOverdue(due, due).Severity);
    }
}
