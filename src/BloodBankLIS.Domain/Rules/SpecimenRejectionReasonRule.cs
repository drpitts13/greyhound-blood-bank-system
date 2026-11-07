using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A rejection needs an active catalog code so rejections can be reported by cause. The comment is optional unless
/// the code says it is required.
/// </summary>
public static class SpecimenRejectionReasonRule
{
    public const string RequiredCode = "SPEC-REJECT-REASON-REQUIRED";
    public const string UnknownCode = "SPEC-REJECT-REASON-UNKNOWN";
    public const string CommentRequiredCode = "SPEC-REJECT-COMMENT-REQUIRED";
    public const string PassCode = "SPEC-REJECT-REASON";

    public static RuleResult Evaluate(string? reasonCode, SpecimenRejectionReason? reason, string? comment)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            return RuleResult.HardStop(RequiredCode, "Choose a rejection reason.");
        }

        if (reason is null || !reason.IsActive)
        {
            return RuleResult.HardStop(UnknownCode, $"Rejection reason '{reasonCode.Trim()}' is not an active reason.");
        }

        if (reason.RequiresComment && string.IsNullOrWhiteSpace(comment))
        {
            return RuleResult.HardStop(CommentRequiredCode, $"Rejection reason {reason.Code} needs a comment.");
        }

        return RuleResult.Pass(PassCode);
    }

    /// <summary>Text stored on the specimen: the catalog text, then the comment when there is one.</summary>
    public static string StoredText(SpecimenRejectionReason reason, string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? reason.Text : $"{reason.Text}: {comment.Trim()}";
}
