using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>End-user testing pass 87: a storage location whose temperature reading is due.</summary>
public class TemperatureReadingDueRuleTests
{
    // TEST-BB-1101: due when the interval is set, the location has a range, and the last reading is at least the interval
    // old or there is none; never while the interval is not set (0) or for a location without a range.
    [Fact]
    public void IsDue_OnlyWithAnIntervalAndARange()
    {
        var now = new DateTime(2026, 11, 14, 20, 0, 0, DateTimeKind.Utc);

        Assert.True(TemperatureReadingDueRule.IsDue(true, now.AddHours(-4), 4, now));
        Assert.False(TemperatureReadingDueRule.IsDue(true, now.AddHours(-3).AddMinutes(-59), 4, now));
        Assert.Equal("No reading recorded", TemperatureReadingDueRule.Text(true, null, 4, now));
        Assert.StartsWith("Due since ", TemperatureReadingDueRule.Text(true, now.AddDays(-2), 4, now));
        Assert.False(TemperatureReadingDueRule.IsDue(true, null, 0, now));
        Assert.Null(TemperatureReadingDueRule.Text(false, null, 4, now));
    }
}
