namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// The facility's local time zone, for documents read at the bedside (for example the MTP pack report). Stored
/// times stay UTC; only display converts. Set by configuration <c>Facility:TimeZone</c>; blank means the server's
/// own time zone.
/// </summary>
public interface IFacilityTimeZone
{
    TimeZoneInfo Zone { get; }

    /// <summary>Printed next to converted times so nobody mistakes them for UTC.</summary>
    string Label { get; }

    DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
