using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class UntypedPatientAllocationRuleTests
{
    // TEST-BB-1176: for a patient with no ABO/Rh on file, only group O red cells (or whole blood) and group AB or A
    // plasma can be reserved; other groups are refused; other components are not affected (end-user testing pass 154).
    [Theory]
    [InlineData(ComponentClass.RedBloodCells, AboGroup.O, RuleSeverity.Pass)]
    [InlineData(ComponentClass.RedBloodCells, AboGroup.B, RuleSeverity.HardStop)]
    [InlineData(ComponentClass.WholeBlood, AboGroup.A, RuleSeverity.HardStop)]
    [InlineData(ComponentClass.Plasma, AboGroup.AB, RuleSeverity.Pass)]
    [InlineData(ComponentClass.Plasma, AboGroup.A, RuleSeverity.Pass)]
    [InlineData(ComponentClass.Plasma, AboGroup.O, RuleSeverity.HardStop)]
    [InlineData(ComponentClass.Platelets, AboGroup.B, RuleSeverity.Pass)]
    public void Evaluate_AllowsOnlyUnitsThatSuitAnyRecipient(ComponentClass component, AboGroup unitAbo, RuleSeverity expected)
    {
        var result = UntypedPatientAllocationRule.Evaluate(component, unitAbo);
        Assert.Equal(expected, result.Severity);
        if (expected == RuleSeverity.HardStop)
        {
            Assert.Contains("no ABO/Rh on file", result.Message);
            Assert.Contains($"group {unitAbo}", result.Message);
        }
    }
}
