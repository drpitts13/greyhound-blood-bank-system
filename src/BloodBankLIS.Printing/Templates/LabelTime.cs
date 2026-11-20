using System.Globalization;

namespace BloodBankLIS.Printing.Templates;

/// <summary>
/// A time printed on a label or tag (end-user testing pass 314: specimen labels, component labels and compatibility tags
/// printed their times in UTC, "Unit expires: 2026-12-06 14:49 UTC", so the nurse checking expiry at the bedside against a
/// local clock read a different hour). With the facility's time zone the time is local, with its UTC offset so it cannot
/// be mistaken: "2026-12-06 09:49 (UTC-05:00)". Without one, it stays UTC and says so.
/// </summary>
public static class LabelTime
{
    public static string Format(DateTime utc, string? timeZoneId)
    {
        var asUtc = utc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : utc.ToUniversalTime();
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                var local = TimeZoneInfo.ConvertTimeFromUtc(asUtc, zone);
                var offset = zone.GetUtcOffset(asUtc);
                var sign = offset < TimeSpan.Zero ? "-" : "+";
                return $"{local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} (UTC{sign}{offset.Duration().Hours:00}:{offset.Duration().Minutes:00})";
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
            }
        }

        return asUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
    }
}
