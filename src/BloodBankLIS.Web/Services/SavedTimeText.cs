using System.Globalization;
using System.Text.RegularExpressions;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// Text saved with a record writes its times in UTC and says so ("read 8.5 °C at 2026-11-12 16:43 UTC"), so it reads
/// correctly on any workstation (ClinicalTimeText.Utc). On screen it is shown in local time, the clock every other column
/// uses (end-user testing pass 302: deviation DEV-3 read "16:43 UTC" beside its reported time of 11:43, so the same event
/// appeared five hours apart). The saved text is not changed.
/// </summary>
public static partial class SavedTimeText
{
    [GeneratedRegex(@"\b(\d{4}-\d{2}-\d{2} \d{2}:\d{2}) UTC\b")]
    private static partial Regex UtcTime();

    public static string Local(string? text) =>
        string.IsNullOrEmpty(text)
            ? text ?? string.Empty
            : UtcTime().Replace(text, m =>
                DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc)
                    ? utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                    : m.Value);
}
