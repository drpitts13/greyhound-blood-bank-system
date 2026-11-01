using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-296: temperature history report summary.
public class StorageTemperatureSummaryRuleTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoReadings_ReportsWholePeriodAsGap()
    {
        var s = StorageTemperatureSummaryRule.Summarize([], Start, Start.AddDays(1));

        Assert.Equal(0, s.Readings);
        Assert.Null(s.MinC);
        Assert.Null(s.PercentInRange);
        Assert.Equal(24, s.LongestGapHours);
    }

    [Fact]
    public void CountsExcursionRuns_AndPercentOfRangedReadings()
    {
        var s = StorageTemperatureSummaryRule.Summarize(
            [
                (Start.AddHours(1), 4m, StorageTemperatureStatus.InRange),
                (Start.AddHours(2), 7m, StorageTemperatureStatus.AboveRange),
                (Start.AddHours(3), 8m, StorageTemperatureStatus.AboveRange),
                (Start.AddHours(4), 5m, StorageTemperatureStatus.InRange),
                (Start.AddHours(14), 0.5m, StorageTemperatureStatus.BelowRange),
                (Start.AddHours(15), 3m, StorageTemperatureStatus.NoRange)
            ],
            Start,
            Start.AddHours(16));

        Assert.Equal(6, s.Readings);
        Assert.Equal(0.5m, s.MinC);
        Assert.Equal(8m, s.MaxC);
        Assert.Equal(4.6m, s.MeanC);
        Assert.Equal(3, s.ExcursionReadings);
        Assert.Equal(2, s.ExcursionEvents);
        Assert.Equal(40m, s.PercentInRange);
        Assert.Equal(10, s.LongestGapHours);
        Assert.Equal(Start.AddHours(15), s.LastReadingUtc);
    }

    [Fact]
    public void GapFromLastReadingToPeriodEnd_Counts()
    {
        var s = StorageTemperatureSummaryRule.Summarize(
            [(Start.AddHours(1), 4m, StorageTemperatureStatus.InRange)],
            Start,
            Start.AddHours(30));

        Assert.Equal(29, s.LongestGapHours);
        Assert.Equal(100m, s.PercentInRange);
    }
}
