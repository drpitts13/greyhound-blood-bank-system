namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Advisory cue that a fatality investigation still needs CBER notification or the
/// written report. Closing the workup does not clear this cue. This is not an FDA filing.
/// </summary>
public static class ReactionFatalityFollowUpRule
{
    public const string Code = "RXN-FATALITY-FOLLOWUP";

    public static RuleResult Evaluate(
        bool isFatality,
        DateTime? cberNotifiedUtc,
        DateTime? writtenReportSubmittedUtc)
    {
        if (!isFatality)
        {
            return RuleResult.Pass(Code);
        }

        if (cberNotifiedUtc is null && writtenReportSubmittedUtc is null)
        {
            return RuleResult.Warning(
                Code,
                "Fatality follow-up is still due: record CBER notification and the written report.");
        }

        if (cberNotifiedUtc is null)
        {
            return RuleResult.Warning(Code, "Fatality follow-up is still due: record CBER notification.");
        }

        if (writtenReportSubmittedUtc is null)
        {
            return RuleResult.Warning(Code, "Fatality follow-up is still due: record the written report.");
        }

        return RuleResult.Pass(Code);
    }
}
