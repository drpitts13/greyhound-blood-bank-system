namespace BloodBankLIS.Domain.Rules;

/// <summary>Summary of one storage device's readings over a reporting period.</summary>
public sealed record StorageTemperatureSummary(
    int Readings,
    decimal? MinC,
    decimal? MaxC,
    decimal? MeanC,
    int ExcursionReadings,
    int ExcursionEvents,
    decimal? PercentInRange,
    DateTime? FirstReadingUtc,
    DateTime? LastReadingUtc,
    double? LongestGapHours);

/// <summary>
/// Summarizes a device's temperature log for a report. Percent in range counts only readings
/// that had a range when taken. An excursion event is an unbroken run of out-of-range readings.
/// The longest gap is measured between consecutive readings and from the period start and end,
/// so a device that was not read for a stretch shows it.
/// </summary>
public static class StorageTemperatureSummaryRule
{
    public static StorageTemperatureSummary Summarize(
        IEnumerable<(DateTime ReadingUtc, decimal ReadingC, StorageTemperatureStatus Status)> readings,
        DateTime periodStartUtc,
        DateTime periodEndUtc)
    {
        ArgumentNullException.ThrowIfNull(readings);
        var ordered = readings.OrderBy(r => r.ReadingUtc).ToList();
        if (ordered.Count == 0)
        {
            return new StorageTemperatureSummary(0, null, null, null, 0, 0, null, null, null,
                periodEndUtc > periodStartUtc ? (periodEndUtc - periodStartUtc).TotalHours : null);
        }

        var ranged = ordered.Count(r => r.Status != StorageTemperatureStatus.NoRange);
        var inRange = ordered.Count(r => r.Status == StorageTemperatureStatus.InRange);
        var excursionReadings = 0;
        var events = 0;
        var inRun = false;
        foreach (var r in ordered)
        {
            var excursion = StorageTemperatureRule.IsExcursion(r.Status);
            if (excursion)
            {
                excursionReadings++;
                if (!inRun) events++;
            }

            inRun = excursion;
        }

        var gap = Math.Max((ordered[0].ReadingUtc - periodStartUtc).TotalHours, (periodEndUtc - ordered[^1].ReadingUtc).TotalHours);
        for (var i = 1; i < ordered.Count; i++)
        {
            gap = Math.Max(gap, (ordered[i].ReadingUtc - ordered[i - 1].ReadingUtc).TotalHours);
        }

        return new StorageTemperatureSummary(
            ordered.Count,
            ordered.Min(r => r.ReadingC),
            ordered.Max(r => r.ReadingC),
            Math.Round(ordered.Average(r => r.ReadingC), 1),
            excursionReadings,
            events,
            ranged == 0 ? null : Math.Round(100m * inRange / ranged, 1),
            ordered[0].ReadingUtc,
            ordered[^1].ReadingUtc,
            Math.Max(0, gap));
    }
}
