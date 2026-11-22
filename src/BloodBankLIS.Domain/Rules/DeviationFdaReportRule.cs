using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The FDA report date belongs only to a deviation determined reportable, and cannot be after today (end-user testing
/// pass 73). OCD-090 (decided 2026-11-14, option B): a Major or Critical deviation cannot be closed until its FDA
/// reportability is determined; a Minor one can.
/// </summary>
public static class DeviationFdaReportRule
{
    public const string Code = "DEV-FDA-REPORT";
    public const string CloseCode = "DEV-FDA-CLOSE";

    /// <summary>The facility setting's default (OCD-090 as decided): major and critical deviations.</summary>
    public const int DefaultRequiredFrom = 2;

    /// <summary>
    /// Whether a deviation of this severity needs its reportability determined to close, under the facility setting
    /// <c>Deviation.FdaReportabilityToClose</c>: 0 none, 1 every deviation, 2 major and critical, 3 critical only.
    /// </summary>
    public static bool RequiredToClose(DeviationSeverity severity, int requiredFrom) => requiredFrom switch
    {
        1 => true,
        2 => severity is DeviationSeverity.Major or DeviationSeverity.Critical,
        3 => severity == DeviationSeverity.Critical,
        _ => false
    };

    public static RuleResult EvaluateClose(DeviationStatus status, DeviationSeverity severity, DeviationFdaReportability reportability,
        int requiredFrom = DefaultRequiredFrom) =>
        status == DeviationStatus.Closed
        && RequiredToClose(severity, requiredFrom)
        && reportability == DeviationFdaReportability.NotDetermined
            ? RuleResult.HardStop(CloseCode, $"A {severity.ToString().ToLowerInvariant()} deviation cannot be closed until its FDA reportability is determined (reportable or not reportable).")
            : RuleResult.Pass(CloseCode);

    public static RuleResult Evaluate(DeviationFdaReportability reportability, DateOnly? reportedOn, DateOnly latestAllowed)
    {
        if (reportedOn is DateOnly on && reportability != DeviationFdaReportability.Reportable)
        {
            return RuleResult.HardStop(Code, "An FDA report date can be recorded only for a deviation determined reportable.");
        }

        if (reportedOn is DateOnly date && date > latestAllowed)
        {
            return RuleResult.HardStop(Code, "The FDA report date is in the future.");
        }

        return RuleResult.Pass(Code);
    }
}
