using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class IssueGateTests
{
    private static readonly DateTime Now = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A fully compatible, ready-to-issue context for an RBC unit.</summary>
    private static IssueGateContext PassingContext() => new()
    {
        IdentityConfirmed = true,
        SpecimenExists = true,
        SpecimenBelongsToPatient = true,
        SpecimenExpiresUtc = Now.AddDays(2),
        PatientBloodTypeKnown = true,
        PatientAboRh = new AboRh(AboGroup.A, RhType.Positive),
        UnitAboRh = new AboRh(AboGroup.O, RhType.Positive),
        ComponentClass = ComponentClass.RedBloodCells,
        UnitStatus = UnitStatus.Allocated,
        UnitExpiresUtc = Now.AddDays(10),
        AllocatedToThisPatient = true,
        RequiresCrossmatch = true,
        HasValidCrossmatch = true,
        IsEmergencyRelease = false,
        ProductTypeMatchesOrder = true,
        PatientSignificantAntibodies = [],
        PatientAntigens = [],
        UnitSignificantAntibodies = [],
        UnitAntigens = [],
        SpecialRequirementsMet = true,
        UnresolvedAboRhDiscrepancy = false,
        HasSecondConcordantAboRh = true,
        NowUtc = Now
    };

    [Fact]
    public void FullyCompatible_IsAllowed()
    {
        var evaluation = IssueGate.Evaluate(PassingContext());
        Assert.True(evaluation.IsAllowed);
    }

    [Fact]
    public void IncompatibleAbo_IsHardStopped()
    {
        var context = PassingContext() with { UnitAboRh = new AboRh(AboGroup.B, RhType.Positive) };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == AboCompatibilityRule.AboCode);
    }

    [Fact]
    public void NotAllocated_IsHardStopped()
    {
        var context = PassingContext() with { AllocatedToThisPatient = false };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.AllocationCode);
    }

    [Fact]
    public void ExpiredSpecimen_IsHardStopped()
    {
        var context = PassingContext() with { SpecimenExpiresUtc = Now.AddHours(-1) };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == SpecimenExpirationRule.ExpiredCode);
    }

    [Fact]
    public void MissingCrossmatch_OutsideEmergency_IsHardStopped()
    {
        var context = PassingContext() with { HasValidCrossmatch = false };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == CrossmatchValidityRule.Code);
    }

    [Fact]
    public void IncompatibleCrossmatch_RequiresOverride()
    {
        var context = PassingContext() with
        {
            HasValidCrossmatch = false,
            HasIncompatibleCrossmatch = true
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.False(evaluation.IsHardStopped);
        Assert.True(evaluation.RequiresOverride);
        Assert.Contains(evaluation.Warnings, r => r.Code == CrossmatchValidityRule.Code);
    }

    [Fact]
    public void MissingCrossmatch_UnderEmergencyRelease_RequiresOverride()
    {
        var context = PassingContext() with { HasValidCrossmatch = false, IsEmergencyRelease = true };
        var evaluation = IssueGate.Evaluate(context);
        Assert.False(evaluation.IsHardStopped);
        Assert.True(evaluation.RequiresOverride);
        Assert.Contains(evaluation.Warnings, r => r.Code == CrossmatchValidityRule.Code);
    }

    [Fact]
    public void MissingRetype_OutsideEmergency_IsHardStopped()
    {
        var context = PassingContext() with { RetypeConfirmed = false };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == ProductRetypeAllocationRule.Code);
    }

    [Fact]
    public void MissingRetype_UnderEmergencyRelease_RequiresOverrideWithoutPriorAllocation()
    {
        var context = PassingContext() with
        {
            RetypeConfirmed = false,
            IsEmergencyRelease = true,
            AllocatedToThisPatient = false,
            UnitStatus = UnitStatus.Received,
            HasValidCrossmatch = false
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.False(evaluation.IsHardStopped);
        Assert.True(evaluation.RequiresOverride);
        Assert.Contains(evaluation.Warnings, r => r.Code == ProductRetypeAllocationRule.Code);
        Assert.DoesNotContain(evaluation.HardStops, r => r.Code == IssueGate.AllocationCode);
        Assert.DoesNotContain(evaluation.HardStops, r => r.Code == IssueGate.UnitStatusCode);
    }

    [Fact]
    public void BloodAttributeMismatch_OnRbc_RequiresOverride()
    {
        var context = PassingContext() with
        {
            PatientSignificantAntibodies = [new BloodAttributeCompatibilityRule.AntibodyRef("K", "anti-K")],
            UnitAntigens = [new BloodAttributeCompatibilityRule.AntigenRef("K", AntigenResult.Positive)]
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.False(evaluation.IsHardStopped);
        Assert.True(evaluation.RequiresOverride);
        Assert.Contains(evaluation.Warnings, r => r.Code == BloodAttributeCompatibilityRule.AntigenNegCode);
    }

    [Fact]
    public void RbcWithoutRequiresCrossmatchFlag_StillRequiresCompatibleXm()
    {
        var context = PassingContext() with
        {
            RequiresCrossmatch = false,
            HasValidCrossmatch = false,
            ComponentClass = ComponentClass.RedBloodCells
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == CrossmatchValidityRule.Code);
    }

    [Fact]
    public void NearExpiryUnit_OnNonCrossmatchProduct_IsOverridableWarning()
    {
        var context = PassingContext() with
        {
            RequiresCrossmatch = false,
            HasValidCrossmatch = false,
            ComponentClass = ComponentClass.Plasma,
            UnitAboRh = new AboRh(AboGroup.A, RhType.Positive),
            UnitExpiresUtc = Now.AddHours(6)
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.RequiresOverride);
        Assert.Contains(evaluation.Warnings, r => r.Code == BloodUnitExpirationRule.NearExpiryCode);
    }

    [Fact]
    public void UnresolvedDiscrepancy_OnCrossmatchProduct_IsHardStop()
    {
        var context = PassingContext() with { UnresolvedAboRhDiscrepancy = true };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.AboRhDiscrepancyCode);
    }

    [Fact]
    public void DescribeAboRhDiscrepancy_MatchesTheIssueGate()
    {
        var cellular = IssueGate.DescribeAboRhDiscrepancy(true, true);
        var hardStop = IssueGate.Evaluate(PassingContext() with { UnresolvedAboRhDiscrepancy = true })
            .HardStops.Single(r => r.Code == IssueGate.AboRhDiscrepancyCode);
        Assert.True(cellular.BlocksIssue);
        Assert.Equal(hardStop.Message, cellular.Notice);

        var plasma = IssueGate.DescribeAboRhDiscrepancy(true, false);
        Assert.False(plasma.BlocksIssue);
        Assert.Equal(
            IssueGate.EvaluateAboRhDiscrepancy(true, false).Message,
            plasma.Notice);

        var clear = IssueGate.DescribeAboRhDiscrepancy(false, true);
        Assert.False(clear.BlocksIssue);
        Assert.Null(clear.Notice);

        var known = new AboRh(AboGroup.O, RhType.Positive);
        var other = new AboRh(AboGroup.A, RhType.Positive);
        Assert.False(IssueGate.HasUnresolvedAboRhDiscrepancy([]));
        Assert.False(IssueGate.HasUnresolvedAboRhDiscrepancy([(known, true)]));
        Assert.False(IssueGate.HasUnresolvedAboRhDiscrepancy([(known, true), (known, false)]));
        Assert.True(IssueGate.HasUnresolvedAboRhDiscrepancy([(known, true), (other, false)]));
        Assert.False(IssueGate.HasUnresolvedAboRhDiscrepancy([(new AboRh(AboGroup.Unknown, RhType.Unknown), true), (other, false)]));
    }

    [Fact]
    public void PatientBannerNotice_UsesTheDisagreementWarning()
    {
        var known = new AboRh(AboGroup.O, RhType.Positive);
        var other = new AboRh(AboGroup.A, RhType.Positive);
        Assert.Equal(
            IssueGate.EvaluateAboRhDiscrepancy(true, false).Message,
            IssueGate.PatientBannerNotice([(known, true), (other, false)]));
        Assert.Null(IssueGate.PatientBannerNotice([(known, true), (known, false)]));
        Assert.Null(IssueGate.PatientBannerNotice([]));
    }

    [Fact]
    public void IdentityNotConfirmed_IsHardStopped()
    {
        var context = PassingContext() with { IdentityConfirmed = false };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.IdentityCode);
    }

    [Fact]
    public void SpecialRequirementsUnmet_IsHardStopped()
    {
        var context = PassingContext() with { SpecialRequirementsMet = false };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.SpecialReqCode);
    }

    [Fact]
    public void FailedVisualInspection_IsHardStopped()
    {
        var context = PassingContext() with { VisualInspectionAcceptable = false };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.VisualInspectionCode);
    }

    [Fact]
    public void CodedAppearanceDefect_IsHardStopped()
    {
        var context = PassingContext() with { Appearance = UnitAppearance.Hemolysis };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueAppearanceRule.Code);
    }

    [Fact]
    public void OnHold_IsHardStopped()
    {
        var context = PassingContext() with { UnitStatus = UnitStatus.OnHold };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.UnitStatusCode);
    }

    [Fact]
    public void MissingCrossmatch_WhenElectronicIssueEligible_IsAllowed()
    {
        var context = PassingContext() with
        {
            HasValidCrossmatch = false,
            IsElectronicIssue = true,
            ElectronicCrossmatchEligible = true
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsAllowed);
    }

    [Fact]
    public void RemoteIssue_WithoutEligibility_IsHardStopped()
    {
        var context = PassingContext() with
        {
            HasValidCrossmatch = false,
            LocationKnown = true,
            LocationAllowsIssue = false,
            LocationAllowsRemoteIssue = true,
            IsRemoteIssue = true,
            ElectronicCrossmatchEligible = false
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == InventoryLocationPolicyRule.ExmEligibilityCode);
    }

    [Fact]
    public void CellularIssue_WithoutSecondAbo_IsHardStopped()
    {
        var context = PassingContext() with { HasSecondConcordantAboRh = false };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == SecondAboDeterminationRule.IssueCode);
    }

    [Fact]
    public void EmergencyCellularIssue_WithoutSecondAbo_IsWarning()
    {
        var context = PassingContext() with
        {
            HasSecondConcordantAboRh = false,
            IsEmergencyRelease = true,
            HasValidCrossmatch = false
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.False(evaluation.IsHardStopped);
        Assert.Contains(evaluation.Warnings, r => r.Code == SecondAboDeterminationRule.IssueCode);
    }

    [Fact]
    public void Emergency_UnknownPatientType_IsWarning()
    {
        var context = PassingContext() with
        {
            PatientBloodTypeKnown = false,
            PatientAboRh = new AboRh(AboGroup.Unknown, RhType.Unknown),
            IsEmergencyRelease = true,
            HasValidCrossmatch = false
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.False(evaluation.IsHardStopped);
        Assert.Contains(evaluation.Warnings, r => r.Code == IssueGate.PatientAboRhCode);
    }

    [Fact]
    public void Emergency_AboMismatch_IsWarning()
    {
        var context = PassingContext() with
        {
            IsEmergencyRelease = true,
            HasValidCrossmatch = false,
            UnitAboRh = new AboRh(AboGroup.B, RhType.Positive)
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.False(evaluation.IsHardStopped);
        Assert.Contains(evaluation.Warnings, r => r.Code == AboCompatibilityRule.AboCode);
        Assert.Contains(evaluation.Warnings, r => r.Code == EmergencyUncrossmatchedAboRule.AboCode);
    }

    [Fact]
    public void Emergency_NonO_Unit_IsWarning()
    {
        var context = PassingContext() with
        {
            IsEmergencyRelease = true,
            HasValidCrossmatch = false,
            UnitAboRh = new AboRh(AboGroup.A, RhType.Positive)
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.Contains(evaluation.Warnings, r => r.Code == EmergencyUncrossmatchedAboRule.AboCode);
    }

    [Fact]
    public void Emergency_OPos_ToChildbearingFemale_IsRhWarning()
    {
        var context = PassingContext() with
        {
            IsEmergencyRelease = true,
            HasValidCrossmatch = false,
            PatientAboRh = new AboRh(AboGroup.O, RhType.Negative),
            UnitAboRh = new AboRh(AboGroup.O, RhType.Positive),
            PatientSex = Sex.Female,
            PatientAgeYears = 25
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.Contains(evaluation.Warnings, r => r.Code == EmergencyUncrossmatchedAboRule.RhCode);
    }

    [Fact]
    public void LinkedOrderOnHold_IsHardStopped()
    {
        var context = PassingContext() with { OrderLinked = true, OrderIsFulfillable = false };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == OrderControlRule.IssueCode);
    }

    [Theory]
    [InlineData(ComponentClass.Plasma)]
    [InlineData(ComponentClass.Platelets)]
    [InlineData(ComponentClass.Cryoprecipitate)]
    [InlineData(ComponentClass.Other)]
    public void NonCellularProduct_DoesNotRequireCrossmatch(ComponentClass componentClass)
    {
        var context = PassingContext() with
        {
            ComponentClass = componentClass,
            RequiresCrossmatch = true,
            HasValidCrossmatch = false,
            UnitAboRh = new AboRh(AboGroup.A, RhType.Positive)
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.False(evaluation.IsHardStopped);
        Assert.DoesNotContain(evaluation.Results, r => r.Code == CrossmatchValidityRule.Code && r.Severity == RuleSeverity.HardStop);
    }

    [Fact]
    public void PlasmaIssue_WithoutSecondAbo_IsAllowed()
    {
        var context = PassingContext() with
        {
            ComponentClass = ComponentClass.Plasma,
            UnitAboRh = new AboRh(AboGroup.A, RhType.Positive),
            RequiresCrossmatch = false,
            HasValidCrossmatch = false,
            HasSecondConcordantAboRh = false
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsAllowed);
    }

    [Fact]
    public void IssueFromStorageOnlyLocation_IsHardStopped()
    {
        var context = PassingContext() with
        {
            LocationKnown = true,
            LocationAllowsIssue = false,
            LocationAllowsRemoteIssue = false
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == InventoryLocationPolicyRule.IssueAllowedCode);
    }

    [Fact]
    public void AutologousUnit_ToReservedPatient_IsAllowed()
    {
        var context = PassingContext() with
        {
            DonationRestriction = DonationRestriction.Autologous,
            ReservedPatientId = 7,
            IssuePatientId = 7
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsAllowed);
    }

    [Fact]
    public void MergedPatientRecord_IsHardStopped()
    {
        var context = PassingContext() with { PatientStatus = PatientStatus.Merged };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == PatientMergeRule.ClinicalUseCode);
    }

    [Fact]
    public void AutologousUnit_ToDifferentPatient_IsHardStopped()
    {
        var context = PassingContext() with
        {
            DonationRestriction = DonationRestriction.Autologous,
            ReservedPatientId = 7,
            IssuePatientId = 99
        };
        var evaluation = IssueGate.Evaluate(context);
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == AutologousDirectedRule.IssueCode);
    }
}
