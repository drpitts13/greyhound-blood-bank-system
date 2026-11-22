using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-383 (OCD-052): the issue gate reads the active compatibility table, stricter only.
public class CompatibilityTablePolicyTests
{
    private static RuleEvaluation Gate(params RuleResult[] results) => new(results);

    private static CompatibilityTableRow Row(string code, ComponentClass cls, string severity, bool active = true) =>
        new(code, cls, severity, active);

    [Fact]
    public void ActiveHardStopRow_MakesAMatchingWarningAHardStop()
    {
        var result = CompatibilityTablePolicy.Apply(
            Gate(RuleResult.Warning("ISS-ABO-COMPAT", "Platelet ABO mismatch.")),
            ComponentClass.Platelets,
            [Row("ISS-ABO-COMPAT.PLT", ComponentClass.Platelets, "HardStop")]);

        var stop = Assert.Single(result.HardStops);
        Assert.Equal("ISS-ABO-COMPAT", stop.Code);
        Assert.Equal("Platelet ABO mismatch." + CompatibilityTablePolicy.EscalatedNote, stop.Message);
    }

    [Fact]
    public void SeededRowsEqualToTheBuiltInDefault_ChangeNothing()
    {
        // The catalog lists antigen-negative as HardStop, while the gate lets a supervisor override it as a warning.
        var seeded = CompatibilityRuleCatalog.Defaults
            .Select(d => Row(d.RuleCode, d.ComponentClass, d.Severity))
            .ToList();
        var result = CompatibilityTablePolicy.Apply(
            Gate(RuleResult.Warning(BloodAttributeCompatibilityRule.AntigenNegCode, "Unit not antigen-negative.")),
            ComponentClass.RedBloodCells,
            seeded);

        Assert.Equal(RuleSeverity.Warning, Assert.Single(result.Results).Severity);
    }

    [Fact]
    public void Table_NeverSoftensAHardStop()
    {
        var result = CompatibilityTablePolicy.Apply(
            Gate(RuleResult.HardStop("ISS-ABO-COMPAT", "ABO incompatible.")),
            ComponentClass.RedBloodCells,
            [Row("ISS-ABO-COMPAT", ComponentClass.RedBloodCells, "Warning")]);

        Assert.Equal(RuleSeverity.HardStop, Assert.Single(result.Results).Severity);
    }

    [Fact]
    public void InactiveRow_OtherComponentClass_OtherCode_OrPass_LeaveTheResultAlone()
    {
        var warning = RuleResult.Warning("ISS-ABO-COMPAT", "Platelet ABO mismatch.");
        Assert.Equal(RuleSeverity.Warning, CompatibilityTablePolicy.Apply(Gate(warning), ComponentClass.Platelets,
            [Row("ISS-ABO-COMPAT.PLT", ComponentClass.Platelets, "HardStop", active: false)]).Results[0].Severity);
        Assert.Equal(RuleSeverity.Warning, CompatibilityTablePolicy.Apply(Gate(warning), ComponentClass.Platelets,
            [Row("ISS-ABO-COMPAT.CRYO", ComponentClass.Cryoprecipitate, "HardStop")]).Results[0].Severity);
        Assert.Equal(RuleSeverity.Warning, CompatibilityTablePolicy.Apply(Gate(warning), ComponentClass.Platelets,
            [Row("ISS-ABO-COMPATX", ComponentClass.Platelets, "HardStop")]).Results[0].Severity);
        Assert.Equal(RuleSeverity.Pass, CompatibilityTablePolicy.Apply(Gate(RuleResult.Pass("ISS-ABO-COMPAT")), ComponentClass.Platelets,
            [Row("ISS-ABO-COMPAT.PLT", ComponentClass.Platelets, "HardStop")]).Results[0].Severity);
        Assert.Equal(RuleSeverity.Warning, CompatibilityTablePolicy.Apply(Gate(warning), ComponentClass.Platelets, []).Results[0].Severity);
    }

    // TEST-BB-384: the admin table says what each row does at issue, the same decision Apply makes.
    [Theory]
    [InlineData("ISS-ABO-COMPAT.PLT", ComponentClass.Platelets, "HardStop", true, CompatibilityTablePolicy.AtIssueRaises)]
    [InlineData("ISS-ABO-COMPAT.PLT", ComponentClass.Platelets, "Warning", true, CompatibilityTablePolicy.AtIssueSame)]
    [InlineData("ISS-ANTIGEN-NEG", ComponentClass.RedBloodCells, "HardStop", true, CompatibilityTablePolicy.AtIssueSame)]
    [InlineData("UNIT-NEAR-EXPIRY", ComponentClass.RedBloodCells, "HardStop", true, CompatibilityTablePolicy.AtIssueRaisesIfReported)]
    [InlineData("RBC-ABO-BASE", ComponentClass.RedBloodCells, "HardStop", true, CompatibilityTablePolicy.AtIssueRaisesIfReported)]
    [InlineData("UNIT-NEAR-EXPIRY", ComponentClass.RedBloodCells, "Warning", true, CompatibilityTablePolicy.AtIssueNoEffect)]
    [InlineData("ISS-ABO-COMPAT.PLT", ComponentClass.Platelets, "HardStop", false, CompatibilityTablePolicy.AtIssueOff)]
    public void DescribeAtIssue_MatchesWhatTheGateDoes(string code, ComponentClass cls, string severity, bool active, string expected)
    {
        Assert.Equal(expected, CompatibilityTablePolicy.DescribeAtIssue(Row(code, cls, severity, active)));
    }

    [Theory]
    [InlineData("ISS-ABO-COMPAT", "Warning", true)]
    [InlineData("ISS-ABO-COMPAT", "Pass", true)]
    [InlineData("ISS-ABO-COMPAT", "HardStop", false)]
    [InlineData("ISS-ABO-COMPAT.PLT", "HardStop", false)]
    [InlineData("ISS-ABO-COMPAT.PLT", "Pass", true)]
    [InlineData("LOCAL-RULE", "Pass", false)]
    public void Admin_CannotSaveARowSofterThanTheBuiltInRule(string code, string severity, bool refused)
    {
        var result = CompatibilityTablePolicy.EvaluateRowSeverity(code, severity);
        Assert.Equal(refused ? RuleSeverity.HardStop : RuleSeverity.Pass, result.Severity);
    }
}
