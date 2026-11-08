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
    public static string? SourceLabel(string? reportingFacility, DateOnly? identifiedOn) =>
        string.IsNullOrWhiteSpace(reportingFacility)
            ? null
            : $"Reported by {reportingFacility.Trim()}" + (identifiedOn is DateOnly on ? $", identified {on:yyyy-MM-dd}" : string.Empty);
}
