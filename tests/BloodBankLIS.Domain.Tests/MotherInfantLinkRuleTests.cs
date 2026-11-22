using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-605: the mother is another active chart, born before the infant, and not the infant's own child.
public class MotherInfantLinkRuleTests
{
    private static readonly DateOnly Born = new(2026, 11, 7);

    [Fact]
    public void Evaluate_RefusesWhatCannotBeTheMother()
    {
        Assert.Equal(RuleSeverity.Pass, MotherInfantLinkRule.Evaluate(1, PatientStatus.Active, Born, 2, PatientStatus.Active, new DateOnly(1994, 3, 14), null).Severity);
        Assert.Contains("merged into another; link the mother", MotherInfantLinkRule.Evaluate(1, PatientStatus.Merged, Born, 2, PatientStatus.Active, new DateOnly(1994, 3, 14), null).Message);
        Assert.Contains("No patient has that MRN", MotherInfantLinkRule.Evaluate(1, PatientStatus.Active, Born, null, null, null, null).Message);
        Assert.Contains("its own mother", MotherInfantLinkRule.Evaluate(1, PatientStatus.Active, Born, 1, PatientStatus.Active, Born, null).Message);
        Assert.Contains("merged into another; enter", MotherInfantLinkRule.Evaluate(1, PatientStatus.Active, Born, 2, PatientStatus.Merged, new DateOnly(1994, 3, 14), null).Message);
        Assert.Contains("linked as this patient's infant", MotherInfantLinkRule.Evaluate(1, PatientStatus.Active, new DateOnly(1970, 1, 1), 2, PatientStatus.Active, new DateOnly(1960, 1, 1), 1).Message);
        Assert.Contains("must be before the infant's", MotherInfantLinkRule.Evaluate(1, PatientStatus.Active, Born, 2, PatientStatus.Active, Born, null).Message);
    }

    // TEST-BB-608: Link mother is offered under one year; a delivery RhIG evaluation is offered the Rh of the one linked
    // infant born within two days, when typed; not for antepartum, twins, an untyped infant, or a birth far from the event.
    [Fact]
    public void OfferLink_AndSuggestInfantRh()
    {
        var today = new DateOnly(2026, 11, 8);
        Assert.True(MotherInfantLinkRule.OfferLink(today.AddDays(-1), today));
        Assert.False(MotherInfantLinkRule.OfferLink(today.AddYears(-1), today));
        Assert.False(MotherInfantLinkRule.OfferLink(new DateOnly(1994, 3, 14), today));

        var delivered = new DateTime(2026, 11, 7, 22, 0, 0, DateTimeKind.Utc);
        (DateOnly, RhType?) Baby(int daysAfter, RhType? rh) => (DateOnly.FromDateTime(delivered).AddDays(daysAfter), rh);
        Assert.Equal(RhType.Positive, MotherInfantLinkRule.SuggestInfantRh(RhigContext.Postpartum, delivered, [Baby(0, RhType.Positive)]));
        Assert.Equal(RhType.Negative, MotherInfantLinkRule.SuggestInfantRh(RhigContext.Postpartum, delivered, [Baby(1, RhType.Negative), Baby(-700, RhType.Positive)]));
        Assert.Null(MotherInfantLinkRule.SuggestInfantRh(RhigContext.Antepartum, delivered, [Baby(0, RhType.Positive)]));
        Assert.Null(MotherInfantLinkRule.SuggestInfantRh(RhigContext.Postpartum, delivered, [Baby(0, RhType.Positive), Baby(0, RhType.Negative)]));
        Assert.Null(MotherInfantLinkRule.SuggestInfantRh(RhigContext.Postpartum, delivered, [Baby(0, null)]));
        Assert.Null(MotherInfantLinkRule.SuggestInfantRh(RhigContext.Postpartum, delivered, [Baby(0, RhType.Unknown)]));
        Assert.Null(MotherInfantLinkRule.SuggestInfantRh(RhigContext.Postpartum, delivered, [Baby(5, RhType.Positive)]));
        Assert.Null(MotherInfantLinkRule.SuggestInfantRh(RhigContext.Postpartum, delivered, []));
    }
}
