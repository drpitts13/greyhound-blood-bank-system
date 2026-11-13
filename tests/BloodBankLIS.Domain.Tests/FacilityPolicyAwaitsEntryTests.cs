using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>Settings decided to be used whose values are not entered yet (Cycle 677).</summary>
public class FacilityPolicyAwaitsEntryTests
{
    private static FacilityPolicyDefinition Def(string key) => FacilityPolicyCatalog.All.Single(d => d.Key == key);

    // TEST-BB-885: the RhIG dose settings and the specimen retention days await entry while at their "not set" default
    // (a stored "0", or none stored); any entered value ends it; a setting that is off by choice (the neonatal rules,
    // the needed-soon warning) never awaits entry.
    [Fact]
    public void AwaitsEntry_OnlyForDecidedSettingsStillAtTheirDefault()
    {
        Assert.Equal(
            [FacilityPolicyKeys.SpecimenRetentionDays, FacilityPolicyKeys.RhigMlPerVial, FacilityPolicyKeys.RhigMaternalBloodVolumeMl, FacilityPolicyKeys.RhigDoseRounding, FacilityPolicyKeys.RhigAdministrationWindowHours, FacilityPolicyKeys.LookbackNotifyWithinDays],
            FacilityPolicyCatalog.All.Where(d => d.AwaitingEntryAtDefault).Select(d => d.Key));

        Assert.True(FacilityPolicyCatalog.AwaitsEntry(Def(FacilityPolicyKeys.SpecimenRetentionDays), "0"));
        Assert.True(FacilityPolicyCatalog.AwaitsEntry(Def(FacilityPolicyKeys.SpecimenRetentionDays), " 0 "));
        Assert.True(FacilityPolicyCatalog.AwaitsEntry(Def(FacilityPolicyKeys.RhigMlPerVial), null));
        Assert.False(FacilityPolicyCatalog.AwaitsEntry(Def(FacilityPolicyKeys.SpecimenRetentionDays), "14"));
        Assert.False(FacilityPolicyCatalog.AwaitsEntry(Def(FacilityPolicyKeys.NeonatalRulesEnabled), "false"));
        Assert.False(FacilityPolicyCatalog.AwaitsEntry(Def(FacilityPolicyKeys.BloodOrderNeededBySoonMinutes), "0"));
    }
}
