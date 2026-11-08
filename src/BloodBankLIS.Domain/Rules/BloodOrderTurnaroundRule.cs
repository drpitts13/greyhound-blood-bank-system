namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Blood order turnaround measures for the report: when the first unit was ready (reserved or issued, whichever came
/// first) and whether that was by the order's needed-by time. A measure only; no check depends on it.
/// </summary>
public static class BloodOrderTurnaroundRule
{
    public const string Met = "Yes";
    public const string Missed = "No";

    /// <summary>The earlier of the first reservation and the first issue, or null when neither happened.</summary>
    public static DateTime? FirstReady(DateTime? firstReservedUtc, DateTime? firstIssuedUtc) =>
        firstReservedUtc is DateTime reserved && firstIssuedUtc is DateTime issued
            ? (reserved <= issued ? reserved : issued)
            : firstReservedUtc ?? firstIssuedUtc;

    /// <summary>
    /// "Yes" when the first unit was ready at or before the needed-by time, "No" when after it; null when the order has
    /// no needed-by time or no unit has been ready yet.
    /// </summary>
    public static string? ReadyByNeededBy(DateTime? firstReadyUtc, DateTime? neededByUtc) =>
        firstReadyUtc is DateTime ready && neededByUtc is DateTime needed
            ? (ready <= needed ? Met : Missed)
            : null;

    /// <summary>Whole minutes between two times, or null when either is missing.</summary>
    public static int? Minutes(DateTime? fromUtc, DateTime? toUtc) =>
        fromUtc is DateTime from && toUtc is DateTime to
            ? (int)Math.Round((to - from).TotalMinutes)
            : null;
}
