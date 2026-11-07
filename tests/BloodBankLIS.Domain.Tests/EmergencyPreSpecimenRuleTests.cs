using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-386 (OCD-053): emergency or MTP release before a specimen is drawn, group O red cells and AB or A plasma only.
public class EmergencyPreSpecimenRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private static IssueGateContext Emergency(ComponentClass cls, AboGroup unitAbo) => new()
    {
        IdentityConfirmed = true,
        SpecimenExists = false,
        SpecimenBelongsToPatient = false,
        SpecimenExpiresUtc = null,
        PatientBloodTypeKnown = false,
        PatientAboRh = new AboRh(AboGroup.Unknown, RhType.Unknown),
        UnitAboRh = new AboRh(unitAbo, RhType.Negative),
        ComponentClass = cls,
        UnitStatus = UnitStatus.Allocated,
        UnitExpiresUtc = Now.AddDays(10),
        AllocatedToThisPatient = true,
        RequiresCrossmatch = cls is ComponentClass.RedBloodCells or ComponentClass.WholeBlood,
        HasValidCrossmatch = false,
        IsEmergencyRelease = true,
        ProductTypeMatchesOrder = true,
        PatientSignificantAntibodies = [],
        PatientAntigens = [],
        UnitSignificantAntibodies = [],
        UnitAntigens = [],
        SpecialRequirementsMet = true,
        UnresolvedAboRhDiscrepancy = false,
        HasSecondConcordantAboRh = false,
        NowUtc = Now
    };

    [Theory]
    [InlineData(ComponentClass.RedBloodCells, AboGroup.O, RuleSeverity.Warning)]
    [InlineData(ComponentClass.WholeBlood, AboGroup.O, RuleSeverity.Warning)]
    [InlineData(ComponentClass.RedBloodCells, AboGroup.A, RuleSeverity.HardStop)]
    [InlineData(ComponentClass.Plasma, AboGroup.AB, RuleSeverity.Warning)]
    [InlineData(ComponentClass.Plasma, AboGroup.A, RuleSeverity.Warning)]
    [InlineData(ComponentClass.Plasma, AboGroup.O, RuleSeverity.HardStop)]
    [InlineData(ComponentClass.Platelets, AboGroup.O, RuleSeverity.HardStop)]
    [InlineData(ComponentClass.Cryoprecipitate, AboGroup.AB, RuleSeverity.HardStop)]
    public void OnlyGroupORedCellsAndAbOrAPlasma(ComponentClass cls, AboGroup abo, RuleSeverity expected)
    {
        Assert.Equal(expected, EmergencyPreSpecimenRule.Evaluate(cls, new AboRh(abo, RhType.Negative)).Severity);
    }

    [Fact]
    public void Applies_OnlyToEmergency_WithNoUsableSpecimen()
    {
        Assert.True(EmergencyPreSpecimenRule.Applies(true, false, null, Now));
        Assert.True(EmergencyPreSpecimenRule.Applies(true, true, Now.AddMinutes(-1), Now));
        Assert.False(EmergencyPreSpecimenRule.Applies(true, true, Now.AddHours(1), Now));
        Assert.False(EmergencyPreSpecimenRule.Applies(false, false, null, Now));
    }

    [Fact]
    public void Gate_EmergencyGroupORedCells_WithoutSpecimen_IsAnOverridableWarning_ThatSaysToCollectOne()
    {
        var evaluation = IssueGate.Evaluate(Emergency(ComponentClass.RedBloodCells, AboGroup.O));

        Assert.False(evaluation.IsHardStopped, string.Join("; ", evaluation.HardStops.Select(h => $"{h.Code}: {h.Message}")));
        var warning = Assert.Single(evaluation.Warnings, w => w.Code == EmergencyPreSpecimenRule.Code);
        Assert.Contains(EmergencyPreSpecimenRule.CollectNow, warning.Message);
        Assert.DoesNotContain(evaluation.Results, r => r.Code == IssueGate.SpecExistsCode);
    }

    [Fact]
    public void Gate_EmergencyGroupARedCells_WithoutSpecimen_IsAHardStop()
    {
        var evaluation = IssueGate.Evaluate(Emergency(ComponentClass.RedBloodCells, AboGroup.A));
        Assert.Contains(evaluation.HardStops, h => h.Code == EmergencyPreSpecimenRule.Code);
    }

    [Fact]
    public void Gate_RoutineIssue_WithoutSpecimen_StaysAHardStop()
    {
        var evaluation = IssueGate.Evaluate(Emergency(ComponentClass.RedBloodCells, AboGroup.O) with { IsEmergencyRelease = false });
        Assert.Contains(evaluation.HardStops, h => h.Code == IssueGate.SpecExistsCode);
        Assert.DoesNotContain(evaluation.Results, r => r.Code == EmergencyPreSpecimenRule.Code);
    }

    [Fact]
    public void Gate_Emergency_WithAnotherPatientsSpecimen_StaysAHardStop()
    {
        var evaluation = IssueGate.Evaluate(Emergency(ComponentClass.RedBloodCells, AboGroup.O) with
        {
            SpecimenExists = true,
            SpecimenBelongsToPatient = false,
            SpecimenExpiresUtc = Now.AddDays(2)
        });
        Assert.Contains(evaluation.HardStops, h => h.Code == IssueGate.SpecPatientCode);
    }

    [Fact]
    public void Gate_Emergency_WithAnExpiredSpecimen_UsesThePreSpecimenRule()
    {
        var evaluation = IssueGate.Evaluate(Emergency(ComponentClass.RedBloodCells, AboGroup.O) with
        {
            SpecimenExists = true,
            SpecimenBelongsToPatient = true,
            SpecimenExpiresUtc = Now.AddHours(-2)
        });
        Assert.DoesNotContain(evaluation.HardStops, h => h.Code == SpecimenExpirationRule.ExpiredCode);
        Assert.Contains(evaluation.Warnings, w => w.Code == EmergencyPreSpecimenRule.Code);
    }
}
