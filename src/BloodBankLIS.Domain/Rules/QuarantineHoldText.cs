namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// How long a quarantined unit has been held and when it expires (end-user testing pass 88: the supervisor queue listed
/// eight held units with neither, so a thawed plasma due to expire in days looked the same as a frozen unit good for
/// months).
/// </summary>
public static class QuarantineHoldText
{
    /// <summary>"Missing 50 hours (since 2026-11-12 13:59)" for a missing or damaged unit (end-user testing pass 90).</summary>
    public static string Since(string what, DateTime sinceUtc, DateTime nowUtc) =>
        $"{what} {RhigEvaluationRule.AgeText(nowUtc - sinceUtc)} (since {ClinicalTimeText.Local(sinceUtc)})";

    public static string For(DateTime heldSinceUtc, DateTime expiresUtc, DateTime nowUtc) =>
        $"Held {RhigEvaluationRule.AgeText(nowUtc - heldSinceUtc)} (since {ClinicalTimeText.Local(heldSinceUtc)}); "
        + (expiresUtc <= nowUtc
            ? $"expired {ClinicalTimeText.Local(expiresUtc)}"
            : $"expires {ClinicalTimeText.Local(expiresUtc)}, in {RhigEvaluationRule.AgeText(expiresUtc - nowUtc)}");
}
