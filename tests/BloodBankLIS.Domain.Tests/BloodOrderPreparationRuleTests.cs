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

    // TEST-BB-625: with a usable specimen the next step opens the reserve form on the order and specimen; without one, the
    // patient's specimens.
    [Fact]
    public void NextStepHref_ReserveFormOrSpecimens()
    {
        Assert.Equal("/patients/7?tab=products&reserveOrder=42&reserveSpecimen=9", BloodOrderPreparationRule.NextStepHref(7, 42, 9));
        Assert.Equal("/patients/7?tab=specimens", BloodOrderPreparationRule.NextStepHref(7, 42, null));
    }

    // TEST-BB-627: a usable specimen without a completed type and screen: finish it first, on the chart's tests.
    [Fact]
    public void TypeAndScreenPending_FinishesItFirst()
    {
        Assert.Equal("Finish type and screen", BloodOrderPreparationRule.NextStep(2, hasUsableSpecimen: true, typeAndScreenComplete: false));
        Assert.Equal("Collect a specimen", BloodOrderPreparationRule.NextStep(2, hasUsableSpecimen: false, typeAndScreenComplete: false));
        Assert.Equal("/patients/7?tab=tests", BloodOrderPreparationRule.NextStepHref(7, 42, 9, typeAndScreenComplete: false));
    }
}
