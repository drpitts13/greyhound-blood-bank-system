using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BloodAttributeCompatibilityRuleTests
{
    private static BloodAttributeCompatibilityRule.AntibodyRef Ab(string code, string name) => new(code, name);
    private static BloodAttributeCompatibilityRule.AntigenRef Ag(string code, AntigenResult result) => new(code, result);

    [Fact]
    public void CellularProduct_PatientAntiK_UnitKPositive_Warns()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.RedBloodCells,
            [Ab("K", "anti-K")],
            [],
            [],
            [Ag("K", AntigenResult.Positive)]);

        Assert.Single(results);
        Assert.Equal(RuleSeverity.Warning, results[0].Severity);
        Assert.Equal("COMPAT-ATTR-K", results[0].Code);
    }

    [Fact]
    public void CellularProduct_PatientAntiK_UnitKNegative_Passes()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.Granulocytes,
            [Ab("K", "anti-K")],
            [],
            [],
            [Ag("K", AntigenResult.Negative)]);

        Assert.Empty(results);
    }

    [Fact]
    public void PlasmaProduct_UnitAntiK_PatientKPositive_Warns()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.Plasma,
            [],
            [Ag("K", AntigenResult.Positive)],
            [Ab("K", "anti-K")],
            []);

        Assert.Single(results);
        Assert.Equal(RuleSeverity.Warning, results[0].Severity);
    }

    [Fact]
    public void PlateletProduct_UnitAntiK_PatientKNegative_Passes()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.Platelets,
            [],
            [Ag("K", AntigenResult.Negative)],
            [Ab("K", "anti-K")],
            []);

        Assert.Empty(results);
    }

    [Fact]
    public void CryoProduct_NoBloodAttributeRules()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.Cryoprecipitate,
            [Ab("K", "anti-K")],
            [],
            [],
            [Ag("K", AntigenResult.Positive)]);

        Assert.Empty(results);
    }

    [Fact]
    public void MissingUnitAntigen_TreatedAsMismatch()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.RedBloodCells,
            [Ab("K", "anti-K")],
            [],
            [],
            []);

        Assert.Single(results);
        Assert.Equal(RuleSeverity.Warning, results[0].Severity);
    }
}
