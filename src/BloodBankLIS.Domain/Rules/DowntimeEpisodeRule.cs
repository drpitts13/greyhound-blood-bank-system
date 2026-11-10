using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Recorded outages (Cycle 662). An outage is a record of fact, not a clinical rule: it starts in the past, ends after it
/// starts and not in the future, and does not overlap another. A paper back-entry whose time falls outside every recorded
/// outage is only a warning to check the paper record (OCD-081 asks whether it should be refused).
/// </summary>
public static class DowntimeEpisodeRule
{
    public const string DescriptionCode = "DT-EPISODE-DESCRIPTION";
    public const string TimeCode = "DT-EPISODE-TIME";
    public const string OverlapCode = "DT-EPISODE-OVERLAP";
    public const string OutsideCode = "DT-EPISODE-OUTSIDE";
    public const int DescriptionMaxLength = 200;

    public static IReadOnlyList<RuleResult> Validate(
        DateTime startedUtc, DateTime? endedUtc, string? description, DateTime nowUtc,
        IEnumerable<DowntimeEpisode> others)
    {
        var results = new List<RuleResult>();
        var text = description?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            results.Add(RuleResult.HardStop(DescriptionCode, "Say what was down, e.g. \"LIS upgrade\" or \"Network outage\"."));
        }
        else if (text.Length > DescriptionMaxLength)
        {
            results.Add(RuleResult.HardStop(DescriptionCode, $"Keep the description to {DescriptionMaxLength} characters."));
        }

        if (startedUtc > nowUtc + IssueTimeRule.ClockAllowance)
        {
            results.Add(RuleResult.HardStop(TimeCode, "The outage cannot start in the future."));
        }

        if (endedUtc is { } end)
        {
            if (end <= startedUtc)
            {
                results.Add(RuleResult.HardStop(TimeCode, "The outage must end after it starts."));
            }
            else if (end > nowUtc + IssueTimeRule.ClockAllowance)
            {
                results.Add(RuleResult.HardStop(TimeCode, "The outage cannot end in the future. Leave the end blank while it is still going on."));
            }
        }

        var thisEnd = endedUtc ?? DateTime.MaxValue;
        var clash = others.FirstOrDefault(o => o.StartedUtc < thisEnd && startedUtc < (o.EndedUtc ?? DateTime.MaxValue));
        if (clash is not null)
        {
            results.Add(RuleResult.HardStop(OverlapCode,
                clash.EndedUtc is null
                    ? $"Outage #{clash.Id} ({clash.Description}) is still open and overlaps this one. End it, or record this as part of it."
                    : $"This overlaps outage #{clash.Id} ({clash.Description}). Outages cannot overlap."));
        }

        return results;
    }

    /// <summary>The recorded outage a time falls in, if any; an open outage runs to now.</summary>
    public static DowntimeEpisode? Covering(IEnumerable<DowntimeEpisode> episodes, DateTime timeUtc) =>
        episodes.Where(e => e.StartedUtc <= timeUtc && (e.EndedUtc is null || timeUtc <= e.EndedUtc))
            .OrderByDescending(e => e.StartedUtc)
            .FirstOrDefault();

    /// <summary>A paper record's time outside every recorded outage warns; with no outage recorded there is nothing to check.</summary>
    public static RuleResult CheckPaperTime(IReadOnlyCollection<DowntimeEpisode> episodes, DateTime paperUtc) =>
        episodes.Count == 0 || Covering(episodes, paperUtc) is not null
            ? RuleResult.Pass(OutsideCode)
            : RuleResult.Warning(OutsideCode,
                "The time on the paper record is outside every recorded outage. Check the record's date and time (OCD-081).");

    /// <summary>The late-entry reason a back-entry carries when its paper time falls in a recorded outage.</summary>
    public static string BackEntryReason(DowntimeEpisode episode) =>
        $"Issued on paper during downtime #{episode.Id}: {episode.Description}";

    /// <summary>The outage to offer for back-entry: the open one, else the most recently ended.</summary>
    public static DowntimeEpisode? Current(IEnumerable<DowntimeEpisode> episodes) =>
        episodes.OrderBy(e => e.EndedUtc is null ? 0 : 1).ThenByDescending(e => e.StartedUtc).FirstOrDefault();
}
