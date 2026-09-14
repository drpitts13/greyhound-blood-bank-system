using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class DowntimeReconciliationAuthorizationRuleTests
{
    [Fact]
    public void Snapshot_WithoutAuditRead_IsHardStop()
    {
        var result = DowntimeReconciliationAuthorizationRule.Evaluate(hasAuditRead: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(DowntimeReconciliationAuthorizationRule.PermissionCode, result.Code);
    }

    [Fact]
    public void Snapshot_WithAuditRead_Passes()
    {
        var result = DowntimeReconciliationAuthorizationRule.Evaluate(hasAuditRead: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
        Assert.Equal(DowntimeReconciliationAuthorizationRule.PermissionCode, result.Code);
    }
}
