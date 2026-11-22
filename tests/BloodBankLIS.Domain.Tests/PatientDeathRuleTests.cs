using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class PatientDeathRuleTests
{
    // TEST-BB-1123: PID-30 Y records the death and N clears it; a PID-29 time alone records it; empty changes nothing
    // (end-user testing pass 107).
    [Fact]
    public void Indicator_ReadsPid30_ThenPid29()
    {
        var when = new DateTime(2026, 5, 30, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(PatientDeathRule.Indicator("Y", null));
        Assert.True(PatientDeathRule.Indicator(" y ", null));
        Assert.False(PatientDeathRule.Indicator("N", when));
        Assert.True(PatientDeathRule.Indicator(null, when));
        Assert.True(PatientDeathRule.Indicator("", when));
        Assert.Null(PatientDeathRule.Indicator("", null));
        Assert.Null(PatientDeathRule.Indicator("U", null));

        var none = PatientDeathRule.Alert(1, "M1", "Doe, J", when, [], "C1");
        Assert.Equal(AlertSeverity.Info, none.Severity);
        Assert.Contains("No unit is reserved", none.Message);
        var two = PatientDeathRule.Alert(1, "M1", "Doe, J", null, ["U-1", "U-2"], "C1");
        Assert.Equal(AlertSeverity.Warning, two.Severity);
        Assert.Contains("2 units are still reserved for the patient (U-1, U-2)", two.Message);
    }
}
