using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// After-the-fact review of an override (emergency release, warning override). A second person with
/// <c>override.approve</c> who neither performed nor authorized it records whether it was acceptable or needs
/// follow-up (a note is required then). Review is a record, not a gate: it never undoes the override. How quickly
/// overrides must be reviewed is facility policy and is not set here.
/// </summary>
public static class OverrideReviewRule
{
    public const string Code = "OVR-REVIEW";

    public static RuleResult Evaluate(
        bool alreadyReviewed,
        string reviewer,
        string performedBy,
        string authorizedBy,
        OverrideReviewOutcome outcome,
        string? note)
    {
        if (alreadyReviewed)
        {
            return RuleResult.HardStop(Code, "This override has already been reviewed.");
        }

        if (string.Equals(reviewer?.Trim(), performedBy?.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(reviewer?.Trim(), authorizedBy?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(Code, "Someone other than the person who performed or authorized the override must review it.");
        }

        if (outcome == OverrideReviewOutcome.FollowUpRequired && string.IsNullOrWhiteSpace(note))
        {
            return RuleResult.HardStop(Code, "Say what follow-up is needed.");
        }

        return outcome is OverrideReviewOutcome.Acceptable or OverrideReviewOutcome.FollowUpRequired
            ? RuleResult.Pass(Code)
            : RuleResult.HardStop(Code, "Choose Acceptable or Needs follow-up.");
    }
}
