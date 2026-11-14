using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class EquipmentCheckRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 14, 0, 0, DateTimeKind.Utc);

    // TEST-BB-968: a check is due its interval after it last passed (by date), due today on that date, overdue after it
    // with the days counted; never done is due now; a failed latest record stays failed whatever the last pass.
    [Fact]
    public void Evaluate_DueFromTheLastPass()
    {
        var current = EquipmentCheckRule.Evaluate(30, Now.AddDays(-10), latestFailed: false, Now);
        Assert.Equal((EquipmentCheckRule.State.Current, new DateOnly(2026, 12, 1)), (current.State, current.DueOn!.Value));
        Assert.False(current.NeedsAction);
        Assert.Equal("Next due 2026-12-01", EquipmentCheckRule.Describe(current));

        // A daily check done late yesterday is due today, not 24 hours later.
        var daily = EquipmentCheckRule.Evaluate(1, new DateTime(2026, 11, 10, 23, 30, 0, DateTimeKind.Utc), false, Now);
        Assert.Equal(EquipmentCheckRule.State.DueToday, daily.State);
        Assert.True(daily.NeedsAction);

        var overdue = EquipmentCheckRule.Evaluate(90, Now.AddDays(-95), false, Now);
        Assert.Equal((EquipmentCheckRule.State.Overdue, 5), (overdue.State, overdue.DaysOverdue!.Value));
        Assert.StartsWith("Overdue 5 days", EquipmentCheckRule.Describe(overdue));

        Assert.Equal(EquipmentCheckRule.State.NeverDone, EquipmentCheckRule.Evaluate(7, null, false, Now).State);
        var failed = EquipmentCheckRule.Evaluate(30, Now.AddDays(-1), latestFailed: true, Now);
        Assert.Equal(EquipmentCheckRule.State.Failed, failed.State);
        Assert.True(failed.NeedsAction);
    }

    // TEST-BB-969: equipment needs a code, name and kind and a code not already used; a check needs a name and an
    // interval of 1 to 3660 days; a failed record needs a note; inactive equipment or checks take no records; out of
    // service and back need a reason and a change.
    [Fact]
    public void Validation_RefusesIncompleteEntries()
    {
        Assert.NotEmpty(EquipmentCheckRule.EvaluateSave("CENT-01", "", "Centrifuge", false));
        Assert.Contains("already registered", Assert.Single(EquipmentCheckRule.EvaluateSave("cent-01", "Centrifuge", "Centrifuge", true)).Message);
        Assert.NotEmpty(EquipmentCheckRule.EvaluateSave(new string('X', 41), "N", "K", false));
        Assert.Empty(EquipmentCheckRule.EvaluateSave("CENT-01", "Centrifuge", "Centrifuge", false));

        Assert.Equal(RuleSeverity.HardStop, EquipmentCheckRule.EvaluateCheck("Daily QC", 0).Severity);
        Assert.Equal(RuleSeverity.HardStop, EquipmentCheckRule.EvaluateCheck("Daily QC", EquipmentCheckRule.MaxIntervalDays + 1).Severity);
        Assert.Equal(RuleSeverity.HardStop, EquipmentCheckRule.EvaluateCheck(" ", 1).Severity);
        Assert.Equal(RuleSeverity.Pass, EquipmentCheckRule.EvaluateCheck("Daily QC", 1).Severity);

        Assert.Equal(RuleSeverity.HardStop, EquipmentCheckRule.EvaluateRecord(true, true, passed: false, note: "").Severity);
        Assert.Equal(RuleSeverity.Pass, EquipmentCheckRule.EvaluateRecord(true, true, passed: false, note: "Read 33 °C").Severity);
        Assert.Equal(RuleSeverity.HardStop, EquipmentCheckRule.EvaluateRecord(false, true, passed: true, note: null).Severity);
        Assert.Equal(RuleSeverity.Pass, EquipmentCheckRule.EvaluateRecord(true, true, passed: true, note: null).Severity);

        Assert.Contains("already out of service", EquipmentCheckRule.EvaluateService("x", takingOut: true, alreadyThatWay: true).Message);
        Assert.Equal(RuleSeverity.HardStop, EquipmentCheckRule.EvaluateService("", takingOut: false, alreadyThatWay: false).Severity);
        Assert.Equal(RuleSeverity.Pass, EquipmentCheckRule.EvaluateService("Repaired, QC passed", takingOut: false, alreadyThatWay: false).Severity);
    }

    // TEST-BB-975: registered equipment used for a modification must be in service: retired or out of service is a
    // hard stop that names since when and why.
    [Fact]
    public void ModificationDevice_MustBeInService()
    {
        Assert.Equal(RuleSeverity.Pass, ModificationDeviceRule.EvaluateRegistered("IRR-1", true, null, null).Severity);
        var outOfService = ModificationDeviceRule.EvaluateRegistered("IRR-1", true, Now, "Dose map failed");
        Assert.Equal(RuleSeverity.HardStop, outOfService.Severity);
        Assert.Equal("IRR-1 is out of service since 2026-11-11 (Dose map failed). Use equipment in service, or return it to service on Equipment once it may be used.", outOfService.Message);
        Assert.Contains("retired", ModificationDeviceRule.EvaluateRegistered("IRR-1", false, null, null).Message);
    }
}
