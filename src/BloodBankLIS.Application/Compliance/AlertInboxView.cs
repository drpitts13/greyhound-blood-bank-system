using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

/// <summary>How the alert inbox groups and dates open alerts. Display only; acknowledging is unchanged.</summary>
public static class AlertInboxView
{
    /// <summary>One filter per kind of open alert, named as the help page names it, most severe kind first.</summary>
    public static IReadOnlyList<(string Source, string Label, int Count)> Kinds(IEnumerable<AlertDto> open) =>
        open.GroupBy(a => a.Source)
            .Select(g => (Source: g.Key, Label: AlertRule.HelpName(g.Key) ?? g.Key, Count: g.Count(), Worst: g.Max(a => a.Severity)))
            .OrderByDescending(k => k.Worst)
            .ThenBy(k => k.Label, StringComparer.OrdinalIgnoreCase)
            .Select(k => (k.Source, k.Label, k.Count))
            .ToList();

    /// <summary>How long an alert has been open: minutes under an hour, hours under two days, then days.</summary>
    public static string OpenFor(DateTime raisedUtc, DateTime nowUtc)
    {
        var age = nowUtc - raisedUtc;
        if (age < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes} min";
        }

        return age < TimeSpan.FromDays(2) ? $"{(int)age.TotalHours} h" : $"{(int)age.TotalDays} d";
    }
}
