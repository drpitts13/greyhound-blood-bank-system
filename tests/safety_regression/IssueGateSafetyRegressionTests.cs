using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Integration.Tests.SafetyRegression;

/// <summary>
/// Permanent negative tests for the issue gate. A corrected safety defect must
/// never be capable of silently returning.
/// </summary>
public class IssueGateSafetyRegressionTests
{
    private static readonly DateTime Now = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    private static IssueGateContext Passing() => new()
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
        SpecialRequirementsMet = true,
        UnresolvedAboRhDiscrepancy = false,
        HasSecondConcordantAboRh = true,
        IssuePatientId = 1,
        NowUtc = Now
    };

    [Fact]
    public void CompatibleUnit_IsAllowed()
    {
        Assert.True(IssueGate.Evaluate(Passing()).IsAllowed);
    }

    [Fact]
    public void AboIncompatible_IsHardStopped()
    {
        var evaluation = IssueGate.Evaluate(Passing() with { UnitAboRh = new AboRh(AboGroup.B, RhType.Positive) });
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == AboCompatibilityRule.AboCode);
    }

    [Fact]
    public void ExpiredUnit_IsHardStopped()
    {
        var evaluation = IssueGate.Evaluate(Passing() with { UnitExpiresUtc = Now.AddMinutes(-1) });
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == BloodUnitExpirationRule.ExpiredCode);
    }

    [Fact]
    public void QuarantinedUnit_IsHardStopped()
    {
        var evaluation = IssueGate.Evaluate(Passing() with { UnitStatus = UnitStatus.Quarantine });
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.UnitStatusCode);
    }

    [Fact]
    public void RecalledUnit_IsHardStopped()
    {
        var evaluation = IssueGate.Evaluate(Passing() with { UnitStatus = UnitStatus.Recalled });
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.UnitStatusCode);
    }

    [Fact]
    public void ExpiredSpecimen_IsHardStopped()
    {
        var evaluation = IssueGate.Evaluate(Passing() with { SpecimenExpiresUtc = Now.AddHours(-1) });
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == SpecimenExpirationRule.ExpiredCode);
    }

    [Fact]
    public void MissingSpecimen_IsHardStopped()
    {
        var evaluation = IssueGate.Evaluate(Passing() with { SpecimenExists = false, SpecimenBelongsToPatient = false });
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.SpecExistsCode);
    }

    [Fact]
    public void HistoricalAntibodyAntigenMismatch_IsNotSilentPass()
    {
        var evaluation = IssueGate.Evaluate(Passing() with
        {
            PatientSignificantAntibodies = [new BloodAttributeCompatibilityRule.AntibodyRef("K", "anti-K")],
            UnitAntigens = [new BloodAttributeCompatibilityRule.AntigenRef("K", AntigenResult.Positive)]
        });
        Assert.False(evaluation.IsAllowed);
        Assert.Contains(evaluation.Warnings, r => r.Code == BloodAttributeCompatibilityRule.AntigenNegCode);
    }

    [Fact]
    public void MissingCrossmatch_WithoutEmergency_IsHardStopped()
    {
        var evaluation = IssueGate.Evaluate(Passing() with { HasValidCrossmatch = false });
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == CrossmatchValidityRule.Code);
    }

    [Fact]
    public void EmergencyWithoutOverrideFacts_StillSurfacesWarnings()
    {
        var evaluation = IssueGate.Evaluate(Passing() with
        {
            HasValidCrossmatch = false,
            IsEmergencyRelease = true
        });
        Assert.True(evaluation.RequiresOverride);
        Assert.False(evaluation.IsHardStopped);
    }

    [Fact]
    public void IdentityNotConfirmed_IsHardStopped()
    {
        var evaluation = IssueGate.Evaluate(Passing() with { IdentityConfirmed = false });
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == IssueGate.IdentityCode);
    }

    [Fact]
    public void AutologousReservedToOtherPatient_IsHardStopped()
    {
        var evaluation = IssueGate.Evaluate(Passing() with
        {
            DonationRestriction = DonationRestriction.Autologous,
            ReservedPatientId = 10,
            IssuePatientId = 20
        });
        Assert.True(evaluation.IsHardStopped);
        Assert.Contains(evaluation.HardStops, r => r.Code == AutologousDirectedRule.IssueCode);
    }
}
