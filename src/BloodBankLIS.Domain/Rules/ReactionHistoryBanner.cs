namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Chart-header copy for a transfusion already recorded as reaction suspected.
/// Does not decide issue eligibility.
/// </summary>
public static class ReactionHistoryBanner
{
    public const string RecordedReaction =
        "This patient has a recorded transfusion reaction.";

    public static string? Notice(bool hasRecordedReaction) =>
        hasRecordedReaction ? RecordedReaction : null;

    /// <summary>
    /// A reaction still under investigation (Cycle 632): said instead of the recorded-reaction line, on every chart tab,
    /// including the one units are issued from. A warning, not a block (OCD-078).
    /// </summary>
    public const string UnderInvestigationAdvice =
        "Do not issue or start another unit unless the physician has decided to continue.";

}
