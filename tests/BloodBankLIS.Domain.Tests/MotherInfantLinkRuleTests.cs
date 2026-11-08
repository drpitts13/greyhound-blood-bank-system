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
}
