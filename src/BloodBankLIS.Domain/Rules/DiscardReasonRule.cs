using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A discard needs an active catalog code so wastage can be reported by reason.
/// The comment is optional unless the code says it is required.
/// </summary>
public static class DiscardReasonRule
{
    public const string RequiredCode = "DISCARD-REASON-REQUIRED";
    public const string UnknownCode = "DISCARD-REASON-UNKNOWN";
    public const string CommentRequiredCode = "DISCARD-COMMENT-REQUIRED";
    public const string PassCode = "DISCARD-REASON";

    public static RuleResult Evaluate(string? reasonCode, UnitDiscardReason? reason, string? comment)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            return RuleResult.HardStop(RequiredCode, "Choose a discard reason.");
        }

        if (reason is null || !reason.IsActive)
        {
            return RuleResult.HardStop(UnknownCode, $"Discard reason '{reasonCode.Trim()}' is not an active reason.");
        }

        if (reason.RequiresComment && string.IsNullOrWhiteSpace(comment))
        {
            return RuleResult.HardStop(CommentRequiredCode, $"Discard reason {reason.Code} needs a comment.");
        }

        return RuleResult.Pass(PassCode);
    }

    /// <summary>Text stored on the unit: the catalog text, then the comment when there is one.</summary>
    public static string StoredText(UnitDiscardReason reason, string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? reason.Text : $"{reason.Text}: {comment.Trim()}";
}
