using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class InventoryLocationPolicyRuleTests
{
    [Fact]
    public void Transfer_ToInactiveLocation_IsHardStopped()
    {
        var results = InventoryLocationPolicyRule.EvaluateTransfer(true, false, true, 1, 6);
        var evaluation = new RuleEvaluation(results);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == InventoryLocationPolicyRule.ActiveCode);
    }

    [Fact]
    public void Transfer_PlateletsToRefrigerator_IsHardStopped()
    {
        var fridge = new InventoryLocation { LocationType = LocationType.Refrigerator };
        InventoryLocationPolicyRule.ApplyTypeDefaults(fridge);

        Assert.False(InventoryLocationPolicyRule.AllowsComponent(fridge, ComponentClass.Platelets));
        var results = InventoryLocationPolicyRule.EvaluateTransfer(
            true, true, InventoryLocationPolicyRule.AllowsComponent(fridge, ComponentClass.Platelets), 1, 6);
        Assert.Contains(results, r => r.Severity == RuleSeverity.HardStop && r.Code == InventoryLocationPolicyRule.StorageCode);
    }

    [Fact]
    public void Transfer_PlasmaToFreezer_IsAllowed()
    {
        var freezer = new InventoryLocation { LocationType = LocationType.Freezer };
        InventoryLocationPolicyRule.ApplyTypeDefaults(freezer);
        Assert.True(InventoryLocationPolicyRule.AllowsComponent(freezer, ComponentClass.Plasma));
        var results = InventoryLocationPolicyRule.EvaluateTransfer(true, true, true, -30, -18);
        Assert.DoesNotContain(results, r => r.Severity == RuleSeverity.HardStop);
    }

    [Fact]
    public void RemoteIssue_WithoutExmEligibility_IsHardStopped()
    {
        var results = InventoryLocationPolicyRule.EvaluateIssue(
            locationKnown: true,
            locationActive: true,
            allowsComponent: true,
            allowsIssue: false,
            allowsRemoteIssue: true,
            allowsElectronicIssue: true,
            requiresSecondVerifier: false,
            hasSecondVerifier: false,
            isRemoteIssue: true,
            isElectronicIssue: false,
            isEmergencyRelease: false,
            electronicCrossmatchEligible: false);
        var evaluation = new RuleEvaluation(results);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == InventoryLocationPolicyRule.ExmEligibilityCode);
    }

    [Fact]
    public void RemoteIssue_WithExmEligibility_IsAllowed()
    {
        var results = InventoryLocationPolicyRule.EvaluateIssue(
            true, true, true, false, true, true, false, false, true, false, false, true);
        var evaluation = new RuleEvaluation(results);
        Assert.False(evaluation.IsHardStopped);
    }

    [Fact]
    public void ElectronicIssue_FromLocationThatDisallowsExm_IsHardStopped()
    {
        var results = InventoryLocationPolicyRule.EvaluateIssue(
            true, true, true, true, false, false, false, false, false, true, false, true);
        var evaluation = new RuleEvaluation(results);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == InventoryLocationPolicyRule.ElectronicCode);
    }

    [Fact]
    public void LocationRequiringSecondVerifier_WithoutVerifier_IsHardStopped()
    {
        var results = InventoryLocationPolicyRule.EvaluateIssue(
            true, true, true, true, false, true, true, false, false, false, false, false);
        var evaluation = new RuleEvaluation(results);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == InventoryLocationPolicyRule.SecondVerifierCode);
    }

    [Fact]
    public void SatelliteRefrigerator_DefaultsToRemoteIssueOnly()
    {
        var loc = new InventoryLocation { LocationType = LocationType.SatelliteRefrigerator };
        InventoryLocationPolicyRule.ApplyTypeDefaults(loc);
        Assert.True(loc.IsSatellite);
        Assert.True(loc.AllowsRemoteIssue);
        Assert.False(loc.AllowsIssue);
        Assert.True(loc.AllowsElectronicIssue);
        Assert.True(loc.AllowsRbc);
        Assert.False(loc.AllowsPlatelets);
    }
}
