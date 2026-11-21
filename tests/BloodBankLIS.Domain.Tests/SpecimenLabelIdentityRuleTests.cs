using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-349
public class SpecimenLabelIdentityRuleTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("MRN1", null)]
    [InlineData(" ", "1980-01-01")]
    public void MissingLabelIdentifier_IsAHardStop_UnlessTheFacilityPrefills(string? id1, string? id2)
    {
        Assert.Equal(RuleSeverity.HardStop, SpecimenLabelIdentityRule.Evaluate(id1, id2, facilityPrefills: false).Severity);
        Assert.Equal(RuleSeverity.Pass, SpecimenLabelIdentityRule.Evaluate(id1, id2, facilityPrefills: true).Severity);
    }

    [Fact]
    public void BothLabelIdentifiers_Pass() =>
        Assert.Equal(RuleSeverity.Pass, SpecimenLabelIdentityRule.Evaluate("MRN1", "1980-01-01", facilityPrefills: false).Severity);
}
