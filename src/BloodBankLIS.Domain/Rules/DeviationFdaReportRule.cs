using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The FDA report date belongs only to a deviation determined reportable, and cannot be after today (end-user testing
/// pass 73). Whether a determination is required to close is OCD-090; nothing here requires one.
/// </summary>
public static class DeviationFdaReportRule
{
    public const string Code = "DEV-FDA-REPORT";

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
