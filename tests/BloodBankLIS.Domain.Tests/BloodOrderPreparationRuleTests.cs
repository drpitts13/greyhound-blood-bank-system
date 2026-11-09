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
        Assert.Equal("/patients/7?tab=specimens&panel=specimen", BloodOrderPreparationRule.NextStepHref(7, 42, null));
    }

    // TEST-BB-627: a usable specimen without a completed type and screen: finish it first, on the chart's tests.
    [Fact]
    public void TypeAndScreenPending_FinishesItFirst()
    {
        Assert.Equal("Finish type and screen", BloodOrderPreparationRule.NextStep(2, hasUsableSpecimen: true, typeAndScreenComplete: false));
        Assert.Equal("Collect a specimen", BloodOrderPreparationRule.NextStep(2, hasUsableSpecimen: false, typeAndScreenComplete: false));
        Assert.Equal("/patients/7?tab=tests", BloodOrderPreparationRule.NextStepHref(7, 42, 9, typeAndScreenComplete: false));
    }

    // TEST-BB-656: when the usable specimen expires before the order is needed, the next step is a new specimen (ahead of
    // finishing the type and screen on it), on the patient's specimens; with no usable specimen it is the usual first
    // collection; with nothing left to prepare there is no step.
    [Fact]
    public void SpecimenExpiresBeforeNeeded_CollectsANewOne()
    {
        Assert.Equal("Collect a new specimen", BloodOrderPreparationRule.NextStep(2, hasUsableSpecimen: true, specimenExpiresBeforeNeeded: true));
        Assert.Equal("Collect a new specimen", BloodOrderPreparationRule.NextStep(2, hasUsableSpecimen: true, typeAndScreenComplete: false, specimenExpiresBeforeNeeded: true));
        Assert.Equal("Collect a specimen", BloodOrderPreparationRule.NextStep(2, hasUsableSpecimen: false, specimenExpiresBeforeNeeded: true));
        Assert.Null(BloodOrderPreparationRule.NextStep(0, hasUsableSpecimen: true, specimenExpiresBeforeNeeded: true));
        Assert.Equal("/patients/7?tab=specimens&panel=specimen", BloodOrderPreparationRule.NextStepHref(7, 42, 9, specimenExpiresBeforeNeeded: true));
        Assert.Equal("/patients/7?tab=products&reserveOrder=42&reserveSpecimen=9", BloodOrderPreparationRule.NextStepHref(7, 42, 9));
    }

    // TEST-BB-636: the downtime sheet line names the order, priority, products, what is still needed, and by when.
    [Fact]
    public void DowntimeLine_SaysWhatAndByWhen()
    {
        Assert.Equal("ORD-1 (Stat): 2 × Red cells, 1 still needed, needed by 2026-11-08 10:00 UTC",
            BloodOrderPreparationRule.DowntimeLine("ORD-1", "Stat", "2 × Red cells", 1, new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("ORD-2 (Routine): 1 × Plasma, 1 still needed", BloodOrderPreparationRule.DowntimeLine("ORD-2", "Routine", "1 × Plasma", 1, null));
    }

    // TEST-BB-638: the reserve form's line says how many more units the order needs, or that it is covered.
    [Fact]
    public void ReserveProgress_SaysWhatIsLeft()
    {
        Assert.Equal("2 more units needed for this order (1 of 3 reserved or issued).", BloodOrderPreparationRule.ReserveProgress(3, 2));
        Assert.Equal("1 more unit needed for this order (0 of 1 reserved or issued).", BloodOrderPreparationRule.ReserveProgress(1, 1));
        Assert.Equal("All 2 units for this order are reserved or issued.", BloodOrderPreparationRule.ReserveProgress(2, 0));
        Assert.Equal("This order has no units ordered.", BloodOrderPreparationRule.ReserveProgress(0, 0));
    }
}
