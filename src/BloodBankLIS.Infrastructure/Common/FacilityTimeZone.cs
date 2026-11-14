using BloodBankLIS.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace BloodBankLIS.Infrastructure.Common;

/// <summary>
/// Reads <c>Facility:TimeZone</c> (a Windows or IANA id, for example "Central Standard Time" or "America/Chicago").
/// Blank uses the server's time zone. An id the server does not know also falls back to the server's zone, and the
/// label says so, so a typo never prints times silently in the wrong zone.
/// </summary>
public sealed class FacilityTimeZone : IFacilityTimeZone
{
    public const string ConfigurationKey = "Facility:TimeZone";

    public FacilityTimeZone(TimeZoneInfo zone, string label)
    {
        Zone = zone;
        Label = label;
    }

    public TimeZoneInfo Zone { get; }

    public string Label { get; }

    public static FacilityTimeZone FromConfiguration(IConfiguration? configuration) =>
        FromId(configuration?[ConfigurationKey]);

    public static FacilityTimeZone FromId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new FacilityTimeZone(TimeZoneInfo.Local, $"{TimeZoneInfo.Local.DisplayName} (server time zone)");
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            return new FacilityTimeZone(zone, zone.DisplayName);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new FacilityTimeZone(
                TimeZoneInfo.Local,
                $"{TimeZoneInfo.Local.DisplayName} (server time zone; configured zone \"{id.Trim()}\" was not found)");
        }
    }
}
