using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class IssueAuthorizationRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = IssueAuthorizationRule.EvaluateCreate(hasIssueCreate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IssueAuthorizationRule.CreateCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = IssueAuthorizationRule.EvaluateCreate(hasIssueCreate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void StandardIssue_DoesNotRequireEmergencyPermission()
    {
        var result = IssueAuthorizationRule.EvaluateEmergency(IssueType.Standard, hasEmergencyReleasePermission: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Theory]
    [InlineData(IssueType.EmergencyRelease)]
    [InlineData(IssueType.MassiveTransfusion)]
    public void EmergencyOrMtp_WithoutPermission_IsHardStop(IssueType issueType)
    {
        var result = IssueAuthorizationRule.EvaluateEmergency(issueType, hasEmergencyReleasePermission: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IssueAuthorizationRule.EmergencyCode, result.Code);
    }

    [Fact]
    public void Emergency_WithPermission_Passes()
    {
        var result = IssueAuthorizationRule.EvaluateEmergency(IssueType.EmergencyRelease, hasEmergencyReleasePermission: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void WarningOverride_WithoutPermission_IsHardStop()
    {
        var result = IssueAuthorizationRule.EvaluateOverride(
            requiresOverride: true, IssueType.Standard, hasOverridePermission: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IssueAuthorizationRule.OverrideCode, result.Code);
    }

    [Fact]
    public void EmergencyOverride_DoesNotAlsoRequireIssueOverride()
    {
        var result = IssueAuthorizationRule.EvaluateOverride(
            requiresOverride: true, IssueType.EmergencyRelease, hasOverridePermission: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void NoOverrideNeeded_PassesWithoutOverridePermission()
    {
        var result = IssueAuthorizationRule.EvaluateOverride(
            requiresOverride: false, IssueType.Standard, hasOverridePermission: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Return_WithoutPermission_IsHardStop()
    {
        var result = IssueAuthorizationRule.EvaluateReturn(hasIssueReturn: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IssueAuthorizationRule.ReturnCode, result.Code);
    }

    [Fact]
    public void Return_WithPermission_Passes()
    {
        var result = IssueAuthorizationRule.EvaluateReturn(hasIssueReturn: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
