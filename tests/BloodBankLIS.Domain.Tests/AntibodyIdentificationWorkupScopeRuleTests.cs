using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyIdentificationWorkupScopeRuleTests
{
    [Fact]
    public void MissingSpecimen_IsWarning()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenScope(hasSpecimen: false);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.UnscopedCode, result.Code);
    }

    [Fact]
    public void LinkedSpecimen_IsPass()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenScope(hasSpecimen: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void OpenUnscoped_BlocksAnotherWorkup()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateOverlappingOpen(
            creatingUnscoped: false,
            hasOpenUnscoped: true,
            hasOpenOnSameSpecimen: false,
            hasAnyOpen: true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.OverlappingOpenCode, result.Code);
    }

    [Fact]
    public void UnscopedCreate_BlockedWhenAnyOpen()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateOverlappingOpen(
            creatingUnscoped: true,
            hasOpenUnscoped: false,
            hasOpenOnSameSpecimen: false,
            hasAnyOpen: true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void SameSpecimenOpen_IsHardStop()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateOverlappingOpen(
            creatingUnscoped: false,
            hasOpenUnscoped: false,
            hasOpenOnSameSpecimen: true,
            hasAnyOpen: true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void DifferentSpecimen_IsAllowed()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateOverlappingOpen(
            creatingUnscoped: false,
            hasOpenUnscoped: false,
            hasOpenOnSameSpecimen: false,
            hasAnyOpen: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void OpenWorkup_CanLinkSpecimen()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateCanLinkSpecimen(AntibodyWorkupStatus.InProgress);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.SpecimenLinkCode, result.Code);
    }

    [Fact]
    public void CompletedWorkup_CannotLinkSpecimen()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateCanLinkSpecimen(AntibodyWorkupStatus.Completed);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.SpecimenLinkCode, result.Code);
    }

    [Fact]
    public void VoidedWorkup_CannotLinkSpecimen()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateCanLinkSpecimen(AntibodyWorkupStatus.Voided);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void RejectedSpecimen_IsHardStop()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenUsable(SpecimenStatus.Rejected, completing: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.SpecimenUnusableCode, result.Code);
    }

    [Fact]
    public void CancelledSpecimen_IsHardStopAtComplete()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenUsable(SpecimenStatus.Cancelled, completing: true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void AcceptedSpecimen_IsPass()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenUsable(SpecimenStatus.Accepted, completing: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void ClockExpired_HardStopsLink()
    {
        var now = new DateTime(2026, 9, 4, 18, 0, 0, DateTimeKind.Utc);
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenExpiration(now.AddMinutes(-1), now, completing: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.SpecimenExpiredCode, result.Code);
    }

    [Fact]
    public void ClockExpired_WarnsAtComplete()
    {
        var now = new DateTime(2026, 9, 4, 18, 0, 0, DateTimeKind.Utc);
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenExpiration(now.AddMinutes(-1), now, completing: true);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.SpecimenExpiredCode, result.Code);
    }

    [Fact]
    public void ExpiredStatus_WarnsAtComplete()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenUsable(SpecimenStatus.Expired, completing: true);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.SpecimenExpiredCode, result.Code);
    }

    [Fact]
    public void CollectedSpecimen_IsHardStop()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenReadiness(SpecimenStatus.Collected, completing: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.SpecimenNotReadyCode, result.Code);
    }

    [Fact]
    public void CollectedSpecimen_HardStopsAtComplete()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenReadiness(SpecimenStatus.Collected, completing: true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.SpecimenNotReadyCode, result.Code);
    }

    [Fact]
    public void ReceivedSpecimen_IsWarning()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenReadiness(SpecimenStatus.Received, completing: false);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.SpecimenUnacceptedCode, result.Code);
    }

    [Fact]
    public void ReceivedSpecimen_WarnsAtComplete()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenReadiness(SpecimenStatus.Received, completing: true);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
        Assert.Equal(AntibodyIdentificationWorkupScopeRule.SpecimenUnacceptedCode, result.Code);
    }

    [Fact]
    public void AcceptedSpecimen_ReadinessIsPass()
    {
        var result = AntibodyIdentificationWorkupScopeRule.EvaluateSpecimenReadiness(SpecimenStatus.Accepted, completing: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
