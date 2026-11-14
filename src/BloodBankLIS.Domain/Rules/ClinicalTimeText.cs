namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// How a stored UTC time is written into a message (Cycle 605). A message shown on screen and not kept uses
/// <see cref="Local"/>, the same local time the pages show. Text that is saved with a record (a status reason, a
/// deviation description) uses <see cref="Utc"/> and says so, so it reads correctly on any workstation later. Before,
/// messages used the "u" format ("2026-11-09 12:49:00Z") or no label, and read as if local.
/// </summary>
public static class ClinicalTimeText
{
    public static string Local(DateTime utc) => AsUtc(utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    /// <summary>A time that may be missing: empty when there is none, as the formatted value was.</summary>
    public static string Local(DateTime? utc) => utc is DateTime value ? Local(value) : string.Empty;

    public static string Utc(DateTime utc) => $"{AsUtc(utc):yyyy-MM-dd HH:mm} UTC";

    // Stored times come back from the database without a kind; they are UTC.
    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
}
