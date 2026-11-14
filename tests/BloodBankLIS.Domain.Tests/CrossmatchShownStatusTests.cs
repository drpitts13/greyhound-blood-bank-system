using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class CrossmatchShownStatusTests
{
    [Fact]
    public void ARecordedResult_IsShown_OtherwiseTheClinicalStatus()
    {
        Assert.Equal(CrossmatchClinicalStatus.Compatible,
            CrossmatchValidityRule.Shown(CrossmatchResult.Compatible, CrossmatchClinicalStatus.NotPerformed));
        Assert.Equal(CrossmatchClinicalStatus.Incompatible,
            CrossmatchValidityRule.Shown(CrossmatchResult.Incompatible, CrossmatchClinicalStatus.NotPerformed));
        Assert.Equal(CrossmatchClinicalStatus.Incompatible,
            CrossmatchValidityRule.Shown(CrossmatchResult.NotPerformed, CrossmatchClinicalStatus.Incompatible));
        Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency,
            CrossmatchValidityRule.Shown(CrossmatchResult.NotPerformed, CrossmatchClinicalStatus.NotCrossmatchedEmergency));
        Assert.Equal(CrossmatchClinicalStatus.NotPerformed, CrossmatchValidityRule.ClinicalStatusFor(CrossmatchResult.NotPerformed));
    }
}
