namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// An antibody reported by another facility (an antibody card, a transfer letter, a phone report): the facility and, when
/// known, the date it was identified there are kept with the record, so the chart says it was not found here. The
/// antibody counts the same as one found here for product selection; whether outside reports need confirming by this
/// laboratory is the medical director's decision (OCD-073).
/// </summary>
public static class OutsideAntibodyReportRule
{
    public const string Code = "AB-OUTSIDE";
    public const int MaxFacilityLength = 200;

    public static RuleResult Evaluate(string? reportingFacility, DateOnly? identifiedOn, DateOnly today)
    {
        var facility = reportingFacility?.Trim() ?? string.Empty;
        if (facility.Length > MaxFacilityLength)
        {
            return RuleResult.HardStop(Code, $"A facility name is at most {MaxFacilityLength} characters.");
        }

        if (identifiedOn is DateOnly on && on > today)
        {
            return RuleResult.HardStop(Code, "The date identified cannot be in the future.");
        }

        return identifiedOn is not null && facility.Length == 0
            ? RuleResult.HardStop(Code, "Name the facility that identified the antibody, or clear the date.")
            : RuleResult.Pass(Code);
    }

    /// <summary>How the source reads on the chart: "Reported by St. Mary's Hospital, identified 2019-04-02", or null when found here.</summary>
    /// <summary>
    /// One antibody in a patient's antibody history, as every warning words it (chart header, issue desk, reaction
    /// investigation): "Anti-K", "Anti-E (historical / currently undetectable)", "Anti-Jka (reported by St. Mary's,
    /// identified 2025-02-01)", both notes joined by "; " when both apply.
    /// </summary>
    public static string HistoryEntry(string specificity, bool isActive, string? reportingFacility, DateOnly? identifiedOn)
    {
        var notes = new List<string>();
        if (!isActive)
        {
            notes.Add("historical / currently undetectable");
        }

        if (SourceLabel(reportingFacility, identifiedOn) is { } source)
        {
            notes.Add(char.ToLowerInvariant(source[0]) + source[1..]);
        }

        return notes.Count == 0 ? specificity : $"{specificity} ({string.Join("; ", notes)})";
    }

    public static string? SourceLabel(string? reportingFacility, DateOnly? identifiedOn) =>
        string.IsNullOrWhiteSpace(reportingFacility)
            ? null
            : $"Reported by {reportingFacility.Trim()}" + (identifiedOn is DateOnly on ? $", identified {on:yyyy-MM-dd}" : string.Empty);
}
