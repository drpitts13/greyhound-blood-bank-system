using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-549: a test can accept several specimen types: the first is preferred, any listed type may be resulted, an
// empty list accepts any type, and every listed type must be in the active catalog.
public class AcceptedSpecimenTypesTests
{
    [Fact]
    public void List_IsParsedNormalizedAndDescribed()
    {
        Assert.Equal(["EDTA", "CLOT"], AcceptedSpecimenTypes.Parse(" edta; CLOT, edta "));
        Assert.Equal("EDTA, CLOT", AcceptedSpecimenTypes.Normalize("edta clot"));
        Assert.Null(AcceptedSpecimenTypes.Normalize(" , "));
        Assert.Equal("EDTA", AcceptedSpecimenTypes.Preferred("EDTA, CLOT"));
        Assert.Null(AcceptedSpecimenTypes.Preferred(null));
        Assert.Equal("EDTA", AcceptedSpecimenTypes.Describe("EDTA"));
        Assert.Equal("EDTA or CLOT", AcceptedSpecimenTypes.Describe("EDTA, CLOT"));
        Assert.Equal("EDTA, CLOT, or HEP", AcceptedSpecimenTypes.Describe("EDTA,CLOT,HEP"));
    }

    [Theory]
    [InlineData("EDTA, CLOT", "clot", true)]
    [InlineData("EDTA, CLOT", "EDTA", true)]
    [InlineData("EDTA, CLOT", "HEP", false)]
    [InlineData("EDTA", "EDTA", true)]
    [InlineData(null, "HEP", true)]
    [InlineData("EDTA", null, false)]
    public void Accepts_AnyListedType(string? stored, string? type, bool expected) =>
        Assert.Equal(expected, AcceptedSpecimenTypes.Accepts(stored, type));

    [Fact]
    public void ResultEntry_AcceptsAnyListedType_AndNamesThemWhenRefused()
    {
        var none = new HashSet<string>();
        Assert.False(SpecimenTypeCompatibilityRule.Evaluate("CLOT", "ABSC", "EDTA, CLOT", none).IsHardStopped);
        var refused = SpecimenTypeCompatibilityRule.Evaluate("HEP", "ABSC", "EDTA, CLOT", none);
        Assert.True(refused.IsHardStopped);
        Assert.Equal("Test ABSC requires specimen type EDTA or CLOT but specimen is 'HEP'.", refused.HardStops.Single().Message);
    }

    [Fact]
    public void Validator_RefusesAListedTypeMissingFromTheCatalog()
    {
        var def = new TestDefinition { Code = "ABSC", Name = "Screen", ResultValueType = ResultValueType.Coded, AllowedResultValues = "Negative\nPositive", RequiredSpecimenType = "EDTA, CLOT, URINE" };

        var eval = TestDefinitionValidator.Validate(def, duplicateActiveCode: false, activeSpecimenTypeCodes: new HashSet<string> { "EDTA", "CLOT" });

        var stop = Assert.Single(eval.HardStops, h => h.Code == "TESTDEF.SPECTYPE.MISSING");
        Assert.Contains("URINE", stop.Message);
    }
}
