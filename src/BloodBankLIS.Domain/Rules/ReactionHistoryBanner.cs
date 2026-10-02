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
}
