namespace BloodBankLIS.Web.Services;

/// <summary>
/// The Locations table on Storage temperatures (end-user testing pass 223: FREEZER-1 held 6 units with no reading ever
/// recorded and read only "None", and a reading five days old gave its time but not its age). Wording only; whether a
/// reading is due still comes from the facility's reading interval.
/// </summary>
public static class StorageReadingText
{
    /// <summary>"5 d ago" for the last reading's time; null when there is none.</summary>
    public static string? Age(DateTime? lastReadingUtc, DateTime nowUtc) =>
        lastReadingUtc is DateTime at ? $"{ElapsedText.FromMinutes((int)Math.Max(0, (nowUtc - at).TotalMinutes))} ago" : null;

    /// <summary>A location holding units with no reading ever recorded: say so, since nothing shows its temperature.</summary>
    public static string? NoReadingWithUnits(DateTime? lastReadingUtc, int unitsStored) =>
        lastReadingUtc is null && unitsStored > 0
            ? $"No reading on record; {unitsStored} unit{(unitsStored == 1 ? "" : "s")} stored here"
            : null;
}
