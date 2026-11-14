using BloodBankLIS.Application.Inventory;

namespace BloodBankLIS.Application.Tests;

public class EquipmentTestNoticeTests
{
    private static EquipmentCheckStatusDto Check(string name, bool needsAction, string status) =>
        new(1, name, 1, null, true, needsAction ? "Overdue" : "Ok", null, needsAction, status, null, null, null, null);

    // End-user testing pass 74: equipment used for tests on the worklist is named when it is out of service or has a check
    // needing action; equipment for other tests, retired equipment, and equipment in order are not.
    [Fact]
    public void For_NamesOutOfServiceAndOverdueEquipmentForListedTests()
    {
        var oos = new EquipmentDto(1, "HB-01", "Heat block", "Heat block", "Bench 2", null, true, DateTime.UtcNow, "Read 33.0 °C", "demo",
            [Check("Temperature check", true, "Failed")], "abSC, xm");
        var overdue = new EquipmentDto(2, "CENT-01", "Serologic centrifuge", "Centrifuge", "Bench 1", null, true, null, null, null,
            [Check("Timer and speed check", true, "Overdue 8 days (due 2026-11-06)")], "ABORH, ABSC, XM, DAT");
        var fine = new EquipmentDto(3, "CW-01", "Cell washer", "Cell washer", "Bench 1", null, true, null, null, null,
            [Check("Daily check", false, "Due 2026-11-15")], "ABSC");
        var otherTest = new EquipmentDto(4, "IRR-01", "Irradiator", "Irradiator", null, null, true, DateTime.UtcNow, "Down", "demo", [], "IRR");
        var retired = new EquipmentDto(5, "OLD-01", "Old centrifuge", "Centrifuge", null, null, false, DateTime.UtcNow, "Retired", "demo", [], "ABSC");

        var lines = EquipmentTestNotice.For([oos, overdue, fine, otherTest, retired], ["ABSC", "abso", "XM"]);

        Assert.Equal(["CENT-01", "HB-01"], lines.Select(l => l.EquipmentCode));
        Assert.Equal(["ABSC", "XM"], lines[1].Tests);
        Assert.Equal("out of service: Read 33.0 °C", lines[1].Problem);
        Assert.Equal("Timer and speed check: Overdue 8 days (due 2026-11-06)", lines[0].Problem);
        Assert.Equal("ABSC, XM", EquipmentTestNotice.Normalize(" absc; xm ,ABSC "));
        Assert.Null(EquipmentTestNotice.Normalize("  "));
    }

    // TEST-BB-1094 (end-user testing pass 81): the device named for a modification is flagged when a check needs action;
    // matched case-insensitively; nothing for equipment in order, unregistered or out of service (refused when saved).
    [Fact]
    public void ForDevice_NamesChecksNeedingAction()
    {
        var washer = new EquipmentDto(2, "CW-01", "Cell washer", "Cell washer", "Bench 1", null, true, null, null, null,
            [Check("Daily check", true, "Overdue 2 days (due 2026-11-12)"), Check("Monthly check", false, "Due 2026-12-01")]);
        var fine = new EquipmentDto(3, "IRR-01", "Irradiator", "Irradiator", null, null, true, null, null, null,
            [Check("Dose check", false, "Due 2027-01-10")]);
        var down = new EquipmentDto(4, "TH-01", "Thawer", "Thawer", null, null, true, DateTime.UtcNow, "Leaking", "demo",
            [Check("Daily check", true, "Failed")]);

        Assert.Equal("Daily check: Overdue 2 days (due 2026-11-12)", EquipmentTestNotice.ForDevice([washer, fine, down], " cw-01 "));
        Assert.Null(EquipmentTestNotice.ForDevice([washer, fine, down], "IRR-01"));
        Assert.Null(EquipmentTestNotice.ForDevice([washer, fine, down], "TH-01"));
        Assert.Null(EquipmentTestNotice.ForDevice([washer, fine, down], "OTHER"));
        Assert.Null(EquipmentTestNotice.ForDevice([washer], null));
    }
}
