using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class ProductSelectionOrderTests
{
    private static readonly AboRh GroupAPositive = new(AboGroup.A, RhType.Positive);

    [Fact]
    public void OtherPatientsRestrictedUnit_IsOmitted()
    {
        Assert.False(ProductSelectionOrder.TryPlace(
            DonationRestriction.Autologous, reservedPatientId: 9, patientId: 4,
            GroupAPositive, GroupAPositive, antigenNegFlag: null, out _));
        Assert.False(ProductSelectionOrder.TryPlace(
            DonationRestriction.Directed, reservedPatientId: null, patientId: 4,
            GroupAPositive, GroupAPositive, antigenNegFlag: null, out _));
    }

    [Fact]
    public void OwnRestrictedUnit_PrecedesIdentical_WhichPrecedesSubstitute()
    {
        var autologous = Place(DonationRestriction.Autologous, 4, GroupAPositive);
        var directed = Place(DonationRestriction.Directed, 4, GroupAPositive);
        var identical = Place(DonationRestriction.Allogeneic, null, GroupAPositive);
        var substitute = Place(DonationRestriction.Allogeneic, null, new AboRh(AboGroup.O, RhType.Positive));

        Assert.Equal(ProductSelectionOrder.PatientAutologousCue, autologous.Cue);
        Assert.Equal(ProductSelectionOrder.PatientDirectedCue, directed.Cue);
        Assert.Equal(ProductSelectionOrder.IdenticalCue, identical.Cue);
        Assert.Equal(ProductSelectionOrder.SubstituteCue, substitute.Cue);
        Assert.True(autologous.Band < directed.Band);
        Assert.True(directed.Band < identical.Band);
        Assert.True(identical.Band < substitute.Band);
    }

    [Fact]
    public void RestrictedUnitCue_NamesThisPatientsUnitOnly()
    {
        Assert.Equal(
            ProductSelectionOrder.PatientAutologousCue,
            ProductSelectionOrder.RestrictedUnitCue(DonationRestriction.Autologous, 4, 4));
        Assert.Equal(
            ProductSelectionOrder.PatientDirectedCue,
            ProductSelectionOrder.RestrictedUnitCue(DonationRestriction.Directed, 4, 4));
        Assert.Null(ProductSelectionOrder.RestrictedUnitCue(DonationRestriction.Allogeneic, 4, 4));
        Assert.Null(ProductSelectionOrder.RestrictedUnitCue(DonationRestriction.Autologous, 9, 4));
        Assert.Null(ProductSelectionOrder.RestrictedUnitCue(DonationRestriction.Directed, null, 4));
    }

    [Fact]
    public void AntigenNegative_PrecedesUntested_WhichPrecedesNotNegative()
    {
        var negative = ProductSelectionOrder.AntigenBand(null);
        var untested = ProductSelectionOrder.AntigenBand(BloodAttributeCompatibilityRule.UntestedFlag);
        var notNegative = ProductSelectionOrder.AntigenBand(BloodAttributeCompatibilityRule.NotNegativeFlag);
        var mixed = ProductSelectionOrder.AntigenBand(BloodAttributeCompatibilityRule.MixedFlag);

        Assert.True(negative < untested);
        Assert.True(untested < notNegative);
        Assert.Equal(notNegative, mixed);
    }

    [Fact]
    public void RhDifference_IsACompatibleSubstitute()
    {
        var placement = Place(DonationRestriction.Allogeneic, null, new AboRh(AboGroup.A, RhType.Negative));
        Assert.Equal(ProductSelectionOrder.SubstituteCue, placement.Cue);
    }

    private static ProductSelectionOrder.Placement Place(
        DonationRestriction restriction, long? reservedPatientId, AboRh unitType)
    {
        var placed = ProductSelectionOrder.TryPlace(
            restriction, reservedPatientId, patientId: 4, GroupAPositive, unitType, antigenNegFlag: null, out var placement);
        Assert.True(placed);
        return placement;
    }
}
