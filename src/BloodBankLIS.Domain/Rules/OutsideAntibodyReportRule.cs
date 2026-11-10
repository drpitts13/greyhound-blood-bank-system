using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// An antibody reported by another facility (an antibody card, a transfer letter, a phone report): the facility and, when
/// known, the date it was identified there are kept with the record, so the chart says it was not found here. The
/// antibody counts the same as one found here for product selection (the safer side). OCD-073 (decided 2026-11-10): the
/// report must be documented (a copy kept, or who called with a phone report) and stays "not yet confirmed here" until
/// this laboratory confirms it by its own testing.
/// </summary>
public static class OutsideAntibodyReportRule
{
    public const string Code = "AB-OUTSIDE";
    public const int MaxFacilityLength = 200;

    public const int MaxDocumentationLength = 500;
    public const string ConfirmCode = "AB-OUTSIDE-CONFIRM";

    public static RuleResult Evaluate(string? reportingFacility, DateOnly? identifiedOn, DateOnly today) =>
        Evaluate(reportingFacility, identifiedOn, today, null, null, documentationRequired: false);

    /// <summary>
    /// The facility, date and (OCD-073) how the report is documented: required for every outside report, a copy naming
    /// what it is and where it is kept, a phone report naming who called.
    /// </summary>
    public static RuleResult Evaluate(
        string? reportingFacility, DateOnly? identifiedOn, DateOnly today, OutsideReportKind? kind, string? documentation,
        bool documentationRequired = true)
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

        if (identifiedOn is not null && facility.Length == 0)
        {
            return RuleResult.HardStop(Code, "Name the facility that identified the antibody, or clear the date.");
        }

        if (facility.Length == 0 || !documentationRequired)
        {
            return RuleResult.Pass(Code);
        }

        var detail = documentation?.Trim() ?? string.Empty;
        if (kind is null)
        {
            return RuleResult.HardStop(Code, "Say how the report is documented: a copy kept, or a phone report (OCD-073).");
        }

        if (detail.Length == 0)
        {
            return RuleResult.HardStop(Code, kind == OutsideReportKind.Phone
                ? "For a phone report, record who called (name, role and number) (OCD-073)."
                : "For a copy, record what it is and where it is kept (OCD-073).");
        }

        return detail.Length > MaxDocumentationLength
            ? RuleResult.HardStop(Code, $"Keep the documentation to {MaxDocumentationLength} characters.")
            : RuleResult.Pass(Code);
    }

    /// <summary>
    /// One antibody in a patient's antibody history, as every warning words it (chart header, issue desk, reaction
    /// investigation): "Anti-K", "Anti-E (historical / currently undetectable)", "Anti-Jka (reported by St. Mary's,
    /// identified 2025-02-01, not yet confirmed here)", both notes joined by "; " when both apply.
    /// </summary>
    public static string HistoryEntry(string specificity, bool isActive, string? reportingFacility, DateOnly? identifiedOn,
        bool confirmedHere = false)
    {
        var notes = new List<string>();
        if (!isActive)
        {
            notes.Add("historical / currently undetectable");
        }

        if (SourceLabel(reportingFacility, identifiedOn) is { } source)
        {
            notes.Add(char.ToLowerInvariant(source[0]) + source[1..] + (confirmedHere ? ", confirmed here" : ", not yet confirmed here"));
        }

        return notes.Count == 0 ? specificity : $"{specificity} ({string.Join("; ", notes)})";
    }

    /// <summary>An active outside report not yet confirmed by this laboratory (OCD-073).</summary>
    public static bool NeedsConfirmation(string? reportingFacility, DateTime? confirmedUtc, bool isActive) =>
        isActive && !string.IsNullOrWhiteSpace(reportingFacility) && confirmedUtc is null;

    /// <summary>Confirming needs an outside report not already confirmed, and the testing here that confirmed it.</summary>
    public static RuleResult EvaluateConfirm(string? reportingFacility, DateTime? confirmedUtc, bool isActive, string? note)
    {
        if (string.IsNullOrWhiteSpace(reportingFacility))
        {
            return RuleResult.HardStop(ConfirmCode, "Only an antibody reported by another facility is confirmed here.");
        }

        if (confirmedUtc is not null)
        {
            return RuleResult.HardStop(ConfirmCode, "This outside report is already confirmed.");
        }

        if (!isActive)
        {
            return RuleResult.HardStop(ConfirmCode, "This antibody record is inactive.");
        }

        var text = note?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return RuleResult.HardStop(ConfirmCode, "Name the testing here that confirmed it (for example the panel or workup).");
        }

        return text.Length > MaxDocumentationLength
            ? RuleResult.HardStop(ConfirmCode, $"Keep the note to {MaxDocumentationLength} characters.")
            : RuleResult.Pass(ConfirmCode);
    }

    /// <summary>How the source reads on the chart: "Reported by St. Mary's Hospital, identified 2019-04-02", or null when found here.</summary>
    public static string? SourceLabel(string? reportingFacility, DateOnly? identifiedOn) =>
        string.IsNullOrWhiteSpace(reportingFacility)
            ? null
            : $"Reported by {reportingFacility.Trim()}" + (identifiedOn is DateOnly on ? $", identified {on:yyyy-MM-dd}" : string.Empty);
}
