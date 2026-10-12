using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class ProductAboRhPolicyTests
{
    public static IEnumerable<object[]> ComponentClasses() =>
        Enum.GetValues<ComponentClass>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(ComponentClasses))]
    public void StandardPolicy_MatchesBuiltInRule_ForEverySex(ComponentClass componentClass)
    {
        var policy = StandardProductAboRhPolicy.For(componentClass);
        Assert.True(policy.IsComplete);

        foreach (var patientAbo in StandardProductAboRhPolicy.AboGroups)
        {
            foreach (var unitAbo in StandardProductAboRhPolicy.AboGroups)
            {
                foreach (var patientRh in StandardProductAboRhPolicy.RhTypes)
                {
                    foreach (var unitRh in StandardProductAboRhPolicy.RhTypes)
                    {
                        foreach (var sex in Enum.GetValues<Sex>())
                        {
                            var recipient = new AboRh(patientAbo, patientRh);
                            var donor = new AboRh(unitAbo, unitRh);
                            var builtIn = AboCompatibilityRule.Evaluate(recipient, donor, componentClass);
                            var fromPolicy = AboCompatibilityRule.Evaluate(recipient, donor, componentClass, sex, policy);
                            Assert.Equal(Severity(builtIn, AboCompatibilityRule.AboCode), Severity(fromPolicy, AboCompatibilityRule.AboCode));
                            Assert.Equal(Severity(builtIn, AboCompatibilityRule.RhCode), Severity(fromPolicy, AboCompatibilityRule.RhCode));
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void MaleRhAllowance_DoesNotOpenFemaleOrUnknown()
    {
        var policy = StandardProductAboRhPolicy.For(ComponentClass.RedBloodCells)
            .WithRh(RhCompatibilitySex.Male, RhType.Negative, RhType.Positive, true);
        var recipient = new AboRh(AboGroup.A, RhType.Negative);
        var donor = new AboRh(AboGroup.O, RhType.Positive);

        Assert.Equal(RuleSeverity.Pass, Severity(
            AboCompatibilityRule.Evaluate(recipient, donor, ComponentClass.RedBloodCells, Sex.Male, policy),
            AboCompatibilityRule.RhCode));
        Assert.Equal(RuleSeverity.HardStop, Severity(
            AboCompatibilityRule.Evaluate(recipient, donor, ComponentClass.RedBloodCells, Sex.Female, policy),
            AboCompatibilityRule.RhCode));
        Assert.Equal(RuleSeverity.HardStop, Severity(
            AboCompatibilityRule.Evaluate(recipient, donor, ComponentClass.RedBloodCells, Sex.Other, policy),
            AboCompatibilityRule.RhCode));
        Assert.Equal(RuleSeverity.Pass, Severity(
            AboCompatibilityRule.Evaluate(recipient, donor, ComponentClass.RedBloodCells, Sex.Male, policy),
            AboCompatibilityRule.AboCode));
    }

    [Fact]
    public void PlasmaStandardPolicy_AllowsAbPlasmaForGroupO()
    {
        var policy = StandardProductAboRhPolicy.For(ComponentClass.Plasma);
        var results = AboCompatibilityRule.Evaluate(
            new AboRh(AboGroup.O, RhType.Positive),
            new AboRh(AboGroup.AB, RhType.Negative),
            ComponentClass.Plasma,
            Sex.Female,
            policy);
        Assert.Equal(RuleSeverity.Pass, Severity(results, AboCompatibilityRule.AboCode));
        Assert.Equal(RuleSeverity.Pass, Severity(results, AboCompatibilityRule.RhCode));
    }

    [Fact]
    public void UnknownRh_IsHardStopOnlyWhenTheGridDeniesAPair()
    {
        var rbc = StandardProductAboRhPolicy.For(ComponentClass.RedBloodCells);
        var plasma = StandardProductAboRhPolicy.For(ComponentClass.Plasma);
        var recipient = new AboRh(AboGroup.O, RhType.Unknown);
        var donor = new AboRh(AboGroup.O, RhType.Positive);

        Assert.Equal(RuleSeverity.HardStop, Severity(
            AboCompatibilityRule.Evaluate(recipient, donor, ComponentClass.RedBloodCells, Sex.Male, rbc),
            AboCompatibilityRule.RhCode));
        Assert.Equal(RuleSeverity.Pass, Severity(
            AboCompatibilityRule.Evaluate(recipient, donor, ComponentClass.Plasma, Sex.Female, plasma),
            AboCompatibilityRule.RhCode));
    }

    [Fact]
    public void MissingCell_InASavedPolicy_IsHardStop()
    {
        var policy = new ProductAboRhPolicy(
            [new ProductAboRhPolicy.AboCell(AboGroup.A, AboGroup.A, true)],
            []);
        var results = AboCompatibilityRule.Evaluate(
            new AboRh(AboGroup.A, RhType.Positive),
            new AboRh(AboGroup.O, RhType.Positive),
            ComponentClass.RedBloodCells,
            Sex.Male,
            policy);
        Assert.Equal(RuleSeverity.HardStop, Severity(results, AboCompatibilityRule.AboCode));
        Assert.Equal(RuleSeverity.HardStop, Severity(results, AboCompatibilityRule.RhCode));
    }

    private static RuleSeverity Severity(IReadOnlyList<RuleResult> results, string code) =>
        results.Single(r => r.Code == code).Severity;
}
