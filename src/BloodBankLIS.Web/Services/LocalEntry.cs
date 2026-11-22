namespace BloodBankLIS.Web.Services;

/// <summary>
/// Date and time fields are typed in local time, the time on the clock and on the label, and stored in UTC
/// (end-user testing pass 57: specimen collection, RhIG, reagent and delivery forms took UTC, so a time typed from the
/// wall clock was five hours off in Eastern time).
/// </summary>
public static class LocalEntry
{
    /// <summary>A stored UTC moment for a datetime-local input.</summary>
    public static DateTime FromUtc(DateTime utc) =>
        DateTime.SpecifyKind(utc.Kind == DateTimeKind.Local ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime(), DateTimeKind.Unspecified);

    /// <summary>What was typed in a datetime-local input, as UTC.</summary>
    public static DateTime ToUtc(DateTime local) =>
        local.Kind == DateTimeKind.Utc ? local : DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    /// <summary>Now, for a datetime-local input's default.</summary>
    public static DateTime Now => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
}
