namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// When a hand-entered result was actually tested, if earlier than it is entered (end-user testing pass 84/85: results
/// read on paper during an outage and entered afterwards showed only the entry time, as if tested hours later). The
/// time is kept with the result's source; it cannot be in the future or before the specimen was collected.
/// </summary>
public static class ResultTestedTimeRule
{
    public const string Code = "RESULT-TESTED-TIME";

    /// <summary>A minute's allowance for clocks.</summary>
    private static readonly TimeSpan Slack = TimeSpan.FromMinutes(1);

    public static RuleResult Evaluate(DateTime? testedUtc, DateTime specimenCollectedUtc, DateTime nowUtc)
    {
        if (testedUtc is not DateTime tested)
        {
            return RuleResult.Pass(Code);
        }

        if (tested > nowUtc + Slack)
        {
            return RuleResult.HardStop(Code, "The time tested cannot be in the future.");
        }

        return tested < specimenCollectedUtc - Slack
            ? RuleResult.HardStop(Code, $"The time tested is before the specimen was collected ({ClinicalTimeText.Local(specimenCollectedUtc)}).")
            : RuleResult.Pass(Code);
    }

    /// <summary>The source reference a hand-entered result carries: "Manual · tested 2026-11-12 04:00 (entered later)".</summary>
    public static string SourceReference(DateTime testedUtc, string? expiredSpecimenReason = null) =>
        $"Manual · tested {ClinicalTimeText.Local(testedUtc)} (entered later"
        + (string.IsNullOrWhiteSpace(expiredSpecimenReason) ? ")" : $"; specimen expired since: {expiredSpecimenReason.Trim()})");
}
