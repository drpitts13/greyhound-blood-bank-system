namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Place a return on the unit timeline. The patient comes from the issue.
/// A missing issue does not invent a patient. A blank reason is omitted.
/// </summary>
public static class ReturnActivityCue
{
    public static string Format(bool reissueEligible, string? reason, string? patientLabel)
    {
        var head = reissueEligible ? "Returned (reissue eligible)" : "Returned";
        var why = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var withReason = why is null ? head : $"{head} — {why}";
        return string.IsNullOrWhiteSpace(patientLabel)
            ? $"{withReason}. The issuing record could not be read."
            : $"{withReason} from {patientLabel.Trim()}";
    }
}
