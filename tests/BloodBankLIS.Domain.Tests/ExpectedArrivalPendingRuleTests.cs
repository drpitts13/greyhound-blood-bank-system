using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ExpectedArrivalPendingRuleTests
{
    [Fact]
    public void Expected_IsPending()
    {
        Assert.True(ExpectedArrivalPendingRule.IsPending(UnitStatus.Expected));
    }

    [Fact]
    public void Quarantine_IsNotPending()
    {
        Assert.False(ExpectedArrivalPendingRule.IsPending(UnitStatus.Quarantine));
    }

    [Fact]
    public void BeforeDue_IsPass()
    {
        var now = DateTime.UtcNow;
        var result = ExpectedArrivalPendingRule.EvaluateOverdue(now.AddHours(1), now);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void AfterDue_IsWarning()
    {
        var now = DateTime.UtcNow;
        var result = ExpectedArrivalPendingRule.EvaluateOverdue(now.AddHours(-1), now);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(ExpectedArrivalPendingRule.Code, result.Code);
    }

    [Fact]
    public void MissingDue_IsPass()
    {
        var result = ExpectedArrivalPendingRule.EvaluateOverdue(null, DateTime.UtcNow);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
