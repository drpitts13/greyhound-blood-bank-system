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

    /// <summary>The facility's calendar day of a UTC time: what "today", an expiry date or "given on" means at the bench.</summary>
    DateOnly Day(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));
}

public static class FacilityDay
{
    /// <summary>
    /// The facility's day of a UTC time, or the UTC day when no facility time zone is available. End-user testing pass 36:
    /// an expiry date compared with the UTC day made a lot expired from 19:00 local on its last day.
    /// </summary>
    public static DateOnly Of(DateTime utc, IFacilityTimeZone? zone) => zone?.Day(utc) ?? DateOnly.FromDateTime(utc);
}
