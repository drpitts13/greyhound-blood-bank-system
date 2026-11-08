using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-621: units still needed never go below zero; the next step is a specimen first, then the units to reserve.
public class BloodOrderPreparationRuleTests
{
    [Fact]
    public void Remaining_AndNextStep()
    {
        Assert.Equal(2, BloodOrderPreparationRule.Remaining(4, 1, 1));
        Assert.Equal(0, BloodOrderPreparationRule.Remaining(2, 2, 1));
        Assert.Equal("Collect a specimen", BloodOrderPreparationRule.NextStep(2, hasUsableSpecimen: false));
        Assert.Equal("Reserve 1 unit", BloodOrderPreparationRule.NextStep(1, hasUsableSpecimen: true));
        Assert.Equal("Reserve 3 units", BloodOrderPreparationRule.NextStep(3, hasUsableSpecimen: true));
        Assert.Null(BloodOrderPreparationRule.NextStep(0, hasUsableSpecimen: false));
    }
}
