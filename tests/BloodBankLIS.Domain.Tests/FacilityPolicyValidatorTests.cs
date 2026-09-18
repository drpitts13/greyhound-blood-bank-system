using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class FacilityPolicyValidatorTests
{
    [Fact]
    public void RejectsMissingReason()
    {
        var setting = new SystemSetting { Key = FacilityPolicyKeys.RetentionYears, Value = "10" };
        var result = FacilityPolicyValidator.Validate(setting, "12", "short", isLegalHold: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == FacilityPolicyValidator.ReasonCode);
    }

    [Fact]
    public void RejectsLegalHold()
    {
        var setting = new SystemSetting { Key = FacilityPolicyKeys.RetentionYears, Value = "10", LegalHold = true };
        var result = FacilityPolicyValidator.Validate(setting, "12", "Court-ordered hold remains in effect.", isLegalHold: true);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == FacilityPolicyValidator.LegalHoldCode);
    }

    [Fact]
    public void RejectsAlloHoursAboveAabbMaximum()
    {
        var setting = new SystemSetting { Key = FacilityPolicyKeys.SpecimenAlloimmunizationHours, Value = "72" };
        var result = FacilityPolicyValidator.Validate(setting, "96", "Extend window for weekend staffing.", isLegalHold: false);
        Assert.True(result.IsHardStopped);
        Assert.Contains(result.HardStops, r => r.Code == FacilityPolicyValidator.RangeCode);
    }

    [Fact]
    public void AcceptsValidBooleanAndInteger()
    {
        var flag = new SystemSetting { Key = FacilityPolicyKeys.AllowElectronicCrossmatch, Value = "true" };
        var okFlag = FacilityPolicyValidator.Validate(flag, "false", "AABB 5.16 validation not yet complete.", isLegalHold: false);
        Assert.False(okFlag.IsHardStopped);

        var hours = new SystemSetting { Key = FacilityPolicyKeys.InTransitDueHours, Value = "4" };
        var okHours = FacilityPolicyValidator.Validate(hours, "6", "Cooler validation supports six hours.", isLegalHold: false);
        Assert.False(okHours.IsHardStopped);
    }

    [Fact]
    public void ElectronicCrossmatch_CatalogDefault_IsOff()
    {
        var def = FacilityPolicyCatalog.Find(FacilityPolicyKeys.AllowElectronicCrossmatch);
        Assert.NotNull(def);
        Assert.Equal("false", def!.DefaultValue);
        Assert.Contains("OCD-006", def.Citation);
    }

    [Fact]
    public void SecondVerifierFlags_RemainOptional()
    {
        Assert.Contains("Optional", FacilityPolicyCatalog.Find(FacilityPolicyKeys.RequireSecondVerifier)!.Description);
        Assert.Contains("Optional", FacilityPolicyCatalog.Find(FacilityPolicyKeys.RequireReceiveVerifier)!.Description);
        Assert.Contains("Optional", FacilityPolicyCatalog.Find(FacilityPolicyKeys.RequireDiscardVerifier)!.Description);
        Assert.Contains("Optional", FacilityPolicyCatalog.Find(FacilityPolicyKeys.RequireQuarantineReleaseVerifier)!.Description);
        Assert.Contains("Optional", FacilityPolicyCatalog.Find(FacilityPolicyKeys.RequireDirectedConversionVerifier)!.Description);
    }
}
