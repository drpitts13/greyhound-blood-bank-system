using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AlertLocalTimeTests
{
    // TEST-BB-1212: alert messages give times in local time like the rest of the screen, not UTC (end-user testing
    // pass 189: "read 7.2 °C at 2026-11-12 17:31 UTC" beside a Raised column in local time).
    [Fact]
    public void AlertMessages_GiveLocalTimes()
    {
        var start = new DateTime(2026, 11, 12, 17, 31, 0, DateTimeKind.Utc);

        var transfusion = TransfusionCompletionRule.Alert(7, "W0001", "MRN0001", start);
        Assert.Contains($"Started {ClinicalTimeText.Local(start)} for MRN MRN0001", transfusion.Message);
        Assert.DoesNotContain("UTC", transfusion.Message);

        var death = PatientDeathRule.Alert(1, "MRN0001", "Demo, Patricia", start, [], "CTRL-1");
        Assert.Contains(ClinicalTimeText.Local(start), death.Message);
        Assert.DoesNotContain("UTC", death.Message);
    }
}
