using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class UnitModificationRestrictionRuleTests
{
    [Fact]
    public void SingleAutologous_CopiesRestrictionAndPatient()
    {
        var inheritance = UnitModificationRestrictionRule.Evaluate(
            [new(DonationRestriction.Autologous, 42)]);

        Assert.False(inheritance.Evaluation.IsHardStopped);
        Assert.Equal(DonationRestriction.Autologous, inheritance.Restriction);
        Assert.Equal(42, inheritance.ReservedPatientId);
    }

    [Fact]
    public void AllogeneicPool_StaysAllogeneic()
    {
        var inheritance = UnitModificationRestrictionRule.Evaluate(
        [
            new(DonationRestriction.Allogeneic, null),
            new(DonationRestriction.Allogeneic, 99)
        ]);

        Assert.False(inheritance.Evaluation.IsHardStopped);
        Assert.Equal(DonationRestriction.Allogeneic, inheritance.Restriction);
        Assert.Null(inheritance.ReservedPatientId);
    }

    [Fact]
    public void SameDirectedPatient_CopiesDirected()
    {
        var inheritance = UnitModificationRestrictionRule.Evaluate(
        [
            new(DonationRestriction.Directed, 7),
            new(DonationRestriction.Directed, 7)
        ]);

        Assert.False(inheritance.Evaluation.IsHardStopped);
        Assert.Equal(DonationRestriction.Directed, inheritance.Restriction);
        Assert.Equal(7, inheritance.ReservedPatientId);
    }

    [Fact]
    public void DifferentPatients_HardStops()
    {
        var inheritance = UnitModificationRestrictionRule.Evaluate(
        [
            new(DonationRestriction.Autologous, 1),
            new(DonationRestriction.Autologous, 2)
        ]);

        Assert.True(inheritance.Evaluation.IsHardStopped);
        Assert.Contains(
            inheritance.Evaluation.HardStops,
            stop => stop.Code == UnitModificationRestrictionRule.DiscordantCode);
    }

    [Fact]
    public void AutologousWithAllogeneic_HardStops()
    {
        var inheritance = UnitModificationRestrictionRule.Evaluate(
        [
            new(DonationRestriction.Autologous, 1),
            new(DonationRestriction.Allogeneic, null)
        ]);

        Assert.True(inheritance.Evaluation.IsHardStopped);
        Assert.Contains(
            inheritance.Evaluation.HardStops,
            stop => stop.Code == UnitModificationRestrictionRule.DiscordantCode);
    }

    [Fact]
    public void RestrictedUnitWithoutPatient_HardStops()
    {
        var inheritance = UnitModificationRestrictionRule.Evaluate(
            [new(DonationRestriction.Directed, null)]);

        Assert.True(inheritance.Evaluation.IsHardStopped);
        Assert.Contains(
            inheritance.Evaluation.HardStops,
            stop => stop.Code == UnitModificationRestrictionRule.RecipientCode);
    }
}
