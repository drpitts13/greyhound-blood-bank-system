namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// How long a quarantined unit has been held and when it expires (end-user testing pass 88: the supervisor queue listed
/// eight held units with neither, so a thawed plasma due to expire in days looked the same as a frozen unit good for
/// months).
/// </summary>
public static class QuarantineHoldText
{
    public static string For(DateTime heldSinceUtc, DateTime expiresUtc, DateTime nowUtc) =>
        $"Held {RhigEvaluationRule.AgeText(nowUtc - heldSinceUtc)} (since {ClinicalTimeText.Local(heldSinceUtc)}); "
        + (expiresUtc <= nowUtc
            ? $"expired {ClinicalTimeText.Local(expiresUtc)}"
            : $"expires {ClinicalTimeText.Local(expiresUtc)}, in {RhigEvaluationRule.AgeText(expiresUtc - nowUtc)}");
}
