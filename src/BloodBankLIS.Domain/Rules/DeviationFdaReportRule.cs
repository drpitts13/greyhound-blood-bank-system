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

    public static RuleResult EvaluateClose(DeviationStatus status, DeviationSeverity severity, DeviationFdaReportability reportability) =>
        status == DeviationStatus.Closed
        && severity is DeviationSeverity.Major or DeviationSeverity.Critical
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
