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

    /// <summary>One inbox row: a single alert, or several of the same kind raised together.</summary>
    /// <param name="Heading">The shared title before ": " (a group), or the alert's own title.</param>
    /// <param name="Items">For a group, what each alert is about (the title after ": "), with its alert.</param>
    public sealed record Row(string Heading, IReadOnlyList<AlertDto> Alerts, IReadOnlyList<(string Label, AlertDto Alert)> Items)
    {
        public bool IsGroup => Alerts.Count > 1;

        public AlertSeverity Severity => Alerts.Max(a => a.Severity);

        public DateTime RaisedUtc => Alerts.Min(a => a.RaisedUtc);
    }

    public const int GroupFrom = 3;
    public static readonly TimeSpan RaisedTogether = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The open list as rows: three or more alerts of one kind and severity whose titles share the text before ": "
    /// ("Reagent QC overdue: lot AHG-24033", "…: lot SC-24415") and that were raised within five minutes of each other
    /// (one scan) become one row naming each item. Critical alerts always stand alone. The order of the list is kept,
    /// each row placed where its first alert was. Acknowledging still records each alert.
    /// </summary>
    public static IReadOnlyList<Row> Rows(IEnumerable<AlertDto> open)
    {
        var list = open.ToList();
        static (string Prefix, string Item)? Split(string title)
        {
            var at = title.IndexOf(": ", StringComparison.Ordinal);
            return at <= 0 || at + 2 >= title.Length ? null : (title[..at], title[(at + 2)..]);
        }

        var grouped = new Dictionary<long, List<AlertDto>>();
        foreach (var kind in list
            .Where(a => a.Severity != AlertSeverity.Critical && Split(a.Title) is not null)
            .GroupBy(a => (a.Source, a.Severity, Split(a.Title)!.Value.Prefix)))
        {
            // Within a kind, alerts raised within the window of the previous one are one batch.
            var batch = new List<AlertDto>();
            foreach (var alert in kind.OrderBy(a => a.RaisedUtc).ThenBy(a => a.Id))
            {
                if (batch.Count > 0 && alert.RaisedUtc - batch[^1].RaisedUtc > RaisedTogether)
                {
                    Keep(batch);
                    batch = new List<AlertDto>();
                }

                batch.Add(alert);
            }

            Keep(batch);
        }

        void Keep(List<AlertDto> batch)
        {
            if (batch.Count >= GroupFrom)
            {
                foreach (var alert in batch)
                {
                    grouped[alert.Id] = batch;
                }
            }
        }

        var rows = new List<Row>();
        var placed = new HashSet<List<AlertDto>>();
        foreach (var alert in list)
        {
            if (!grouped.TryGetValue(alert.Id, out var batch))
            {
                rows.Add(new Row(alert.Title, [alert], []));
                continue;
            }

            if (placed.Add(batch))
            {
                var ordered = list.Where(a => batch.Contains(a)).ToList();
                rows.Add(new Row(Split(alert.Title)!.Value.Prefix, ordered, ordered.Select(a => (Split(a.Title)!.Value.Item, a)).ToList()));
            }
        }

        return rows;
    }

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
