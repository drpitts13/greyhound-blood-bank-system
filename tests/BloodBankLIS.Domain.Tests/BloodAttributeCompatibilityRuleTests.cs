using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BloodAttributeCompatibilityRuleTests
{
    private static BloodAttributeCompatibilityRule.AntibodyRef Ab(string code, string name) => new(code, name);
    private static BloodAttributeCompatibilityRule.AntigenRef Ag(string code, AntigenResult result) => new(code, result);

    [Fact]
    public void RbcProduct_PatientAntiK_UnitKPositive_WarnsAntigenNeg()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.RedBloodCells,
            [Ab("K", "anti-K")],
            [],
            [],
            [Ag("K", AntigenResult.Positive)]);

        Assert.Single(results);
        Assert.Equal(RuleSeverity.Warning, results[0].Severity);
        Assert.Equal(BloodAttributeCompatibilityRule.AntigenNegCode, results[0].Code);
    }

    [Fact]
    public void WholeBlood_PatientAntiK_MissingUnitTyping_WarnsAntigenNeg()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.WholeBlood,
            [Ab("K", "anti-K")],
            [],
            [],
            []);

        Assert.Single(results);
        Assert.Equal(RuleSeverity.Warning, results[0].Severity);
        Assert.Equal(BloodAttributeCompatibilityRule.AntigenNegCode, results[0].Code);
    }

    [Fact]
    public void Granulocytes_PatientAntiK_UnitKPositive_Warns()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.Granulocytes,
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
    public void Rbc_MissingUnitAntigen_Warns()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.RedBloodCells,
            [Ab("K", "anti-K")],
            [],
            [],
            []);

        Assert.Single(results);
        Assert.Equal(RuleSeverity.Warning, results[0].Severity);
        Assert.Equal(BloodAttributeCompatibilityRule.AntigenNegCode, results[0].Code);
    }

    [Fact]
    public void CellularProduct_DistinguishesRhCapitalCFromLittleC()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.RedBloodCells,
            [Ab("c", "anti-c")],
            [],
            [],
            [
                Ag("C", AntigenResult.Positive),
                Ag("c", AntigenResult.Negative)
            ]);

        Assert.Empty(results);
    }

    [Fact]
    public void CellularProduct_AntiC_DoesNotMatchLittleCNegative()
    {
        var results = BloodAttributeCompatibilityRule.Evaluate(
            ComponentClass.RedBloodCells,
            [Ab("C", "anti-C")],
            [],
            [],
            [Ag("c", AntigenResult.Negative)]);

        Assert.Single(results);
        Assert.Equal(RuleSeverity.Warning, results[0].Severity);
        Assert.Equal(BloodAttributeCompatibilityRule.AntigenNegCode, results[0].Code);
        Assert.Contains("anti-C", results[0].Message, StringComparison.Ordinal);
    }
}
