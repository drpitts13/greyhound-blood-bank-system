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

    [Fact]
    public void DocumentTransfusion_WithoutPermission_IsHardStop()
    {
        var result = IssueAuthorizationRule.EvaluateDocumentTransfusion(hasTransfusionDocument: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IssueAuthorizationRule.DocumentTransfusionCode, result.Code);
    }

    [Fact]
    public void DocumentTransfusion_WithPermission_Passes()
    {
        var result = IssueAuthorizationRule.EvaluateDocumentTransfusion(hasTransfusionDocument: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void WardReceipt_WithoutPermission_IsHardStop()
    {
        var result = IssueAuthorizationRule.EvaluateWardReceipt(hasTransfusionDocument: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IssueAuthorizationRule.WardReceiptCode, result.Code);
    }

    [Fact]
    public void WardReceipt_WithPermission_Passes()
    {
        var result = IssueAuthorizationRule.EvaluateWardReceipt(hasTransfusionDocument: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void InterfaceDocument_WithoutPermission_IsHardStop()
    {
        var result = IssueAuthorizationRule.EvaluateInterfaceDocument(hasTransfusionDocument: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(IssueAuthorizationRule.InterfaceDocumentCode, result.Code);
    }

    [Fact]
    public void InterfaceDocument_WithPermission_Passes()
    {
        var result = IssueAuthorizationRule.EvaluateInterfaceDocument(hasTransfusionDocument: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
