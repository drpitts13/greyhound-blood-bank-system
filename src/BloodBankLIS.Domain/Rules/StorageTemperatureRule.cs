namespace BloodBankLIS.Domain.Rules;

public enum StorageTemperatureStatus
{
    InRange = 0,
    BelowRange = 1,
    AboveRange = 2,
    NoRange = 3
}

/// <summary>
/// Compares a storage reading with the location's configured range. The range is
/// facility configuration on <c>InventoryLocation</c>; this rule does not supply
/// product storage limits. A location with no range cannot raise an excursion.
/// </summary>
public static class StorageTemperatureRule
{
    public const string ImplausibleCode = "TEMP-IMPLAUSIBLE";
    public const string FutureCode = "TEMP-FUTURE";

    /// <summary>Entry guard against typing errors only, not a storage limit.</summary>
    public const decimal LowestPlausibleC = -100m;

    public const decimal HighestPlausibleC = 60m;

    /// <summary>Clock drift allowed between a workstation and the server.</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    public static StorageTemperatureStatus Evaluate(decimal readingC, decimal? minC, decimal? maxC)
    {
        if (minC is null && maxC is null)
        {
            return StorageTemperatureStatus.NoRange;
        }

        if (minC is decimal min && readingC < min)
        {
            return StorageTemperatureStatus.BelowRange;
        }

        if (maxC is decimal max && readingC > max)
        {
            return StorageTemperatureStatus.AboveRange;
        }

        return StorageTemperatureStatus.InRange;
    }

    /// <summary>Statuses of a unit still physically held at its current location.</summary>
    public static readonly IReadOnlySet<Enums.UnitStatus> StoredStatuses = new HashSet<Enums.UnitStatus>
    {
        Enums.UnitStatus.Quarantine,
        Enums.UnitStatus.Available,
        Enums.UnitStatus.Allocated,
        Enums.UnitStatus.Received,
        Enums.UnitStatus.Selected,
        Enums.UnitStatus.Assigned,
        Enums.UnitStatus.Crossmatched,
        Enums.UnitStatus.Returned,
        Enums.UnitStatus.Recalled,
        Enums.UnitStatus.Damaged
    };

    /// <summary>
    /// Start of the excursion still in progress at a location: the earliest reading of the
    /// unbroken run of out-of-range readings that ends with the latest reading. Null when
    /// the latest reading is in range or has no range.
    /// </summary>
    public static DateTime? OngoingExcursionStart(IEnumerable<(DateTime ReadingUtc, StorageTemperatureStatus Status)> readings)
    {
        DateTime? start = null;
        foreach (var reading in readings.OrderByDescending(r => r.ReadingUtc))
        {
            if (!IsExcursion(reading.Status))
            {
                break;
            }

            start = reading.ReadingUtc;
        }

        return start;
    }

    /// <summary>
    /// OCD-036 (decided 2026-10-31): units are quarantined once an excursion has lasted
    /// longer than the configured minutes. Zero turns automatic quarantine off.
    /// </summary>
    public static bool RequiresQuarantine(DateTime? excursionStartUtc, DateTime nowUtc, int thresholdMinutes) =>
        thresholdMinutes > 0
        && excursionStartUtc is DateTime start
        && nowUtc - start > TimeSpan.FromMinutes(thresholdMinutes);

    public static bool IsExcursion(StorageTemperatureStatus status) =>
        status is StorageTemperatureStatus.BelowRange or StorageTemperatureStatus.AboveRange;

    public static bool IsPlausible(decimal readingC) =>
        readingC >= LowestPlausibleC && readingC <= HighestPlausibleC;

    public static bool IsInFuture(DateTime readingUtc, DateTime nowUtc) =>
        readingUtc > nowUtc + FutureTolerance;
}
