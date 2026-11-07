using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-315: appearance at issue, ward receipt, and receipt is recorded by a person (OCD-040).
public class AppearanceAttestationRuleTests
{
    [Theory]
    [InlineData(UnitAppearance.Acceptable)]
    [InlineData(UnitAppearance.Hemolysis)]
    public void RecordedAppearance_IsKept_AndNotADefault(UnitAppearance recorded)
    {
        var r = AppearanceAttestationRule.Resolve(recorded, facilityDefaultAcceptable: true, "issuing the unit");

        Assert.Equal(RuleSeverity.Pass, r.Result.Severity);
        Assert.Equal(recorded, r.Appearance);
        Assert.False(r.UsedDefault);
    }

    [Fact]
    public void Unrecorded_IsAHardStop_NamingTheStep()
    {
        var r = AppearanceAttestationRule.Resolve(null, facilityDefaultAcceptable: false, "acknowledging receipt");

        Assert.Equal(RuleSeverity.HardStop, r.Result.Severity);
        Assert.Equal(AppearanceAttestationRule.Code, r.Result.Code);
        Assert.Equal("Look at the bag and record its appearance before acknowledging receipt.", r.Result.Message);
    }

    [Fact]
    public void Unrecorded_WithFacilityDefault_IsAcceptable_AndFlagged()
    {
        var r = AppearanceAttestationRule.Resolve(null, facilityDefaultAcceptable: true, "issuing the unit");

        Assert.Equal(RuleSeverity.Pass, r.Result.Severity);
        Assert.Equal(UnitAppearance.Acceptable, r.Appearance);
        Assert.True(r.UsedDefault);
    }

    [Fact]
    public void Unrecorded_WhereInspectionIsNotRequired_KeepsTheEarlierBehavior()
    {
        var r = AppearanceAttestationRule.Resolve(null, facilityDefaultAcceptable: false, "receiving the unit", inspectionRequired: false);

        Assert.Equal(RuleSeverity.Pass, r.Result.Severity);
        Assert.Equal(UnitAppearance.Acceptable, r.Appearance);
        Assert.True(r.UsedDefault);
    }
}
