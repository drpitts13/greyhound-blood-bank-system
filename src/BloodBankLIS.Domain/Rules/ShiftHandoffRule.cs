namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Shift handoff requires a note, <c>shift.handoff</c>, and a different acknowledger. A named recipient must be an
/// active user other than the sender. Queue counts on the packet are informational and do not change clinical gates.
/// Acknowledging accepts responsibility for open bench work, so a read-only permission is not enough.
/// </summary>
public static class ShiftHandoffRule
{
    public const string PermissionCode = "SHIFT-HANDOFF-PERM";
    public const string NotesCode = "SHIFT-HANDOFF-NOTES";
    public const string SelfAckCode = "SHIFT-HANDOFF-SELF";
    public const string AlreadyAckCode = "SHIFT-HANDOFF-ACKED";
    public const string RecipientCode = "SHIFT-HANDOFF-TO";

    public static RuleResult EvaluatePermission(bool hasShiftHandoff) =>
        hasShiftHandoff
            ? RuleResult.Pass(PermissionCode)
            : RuleResult.HardStop(
                PermissionCode,
                "Recording or acknowledging a shift handoff requires the shift.handoff permission.");

    /// <summary>A blank recipient means anyone on the next shift.</summary>
    public static RuleResult EvaluateRecipient(string? toUser, string fromUser, bool isActiveUser)
    {
        if (string.IsNullOrWhiteSpace(toUser))
        {
            return RuleResult.Pass(RecipientCode);
        }

        if (string.Equals(toUser.Trim(), fromUser?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(RecipientCode, "You cannot hand off to yourself. Choose the incoming technologist, or leave it blank.");
        }

        return isActiveUser
            ? RuleResult.Pass(RecipientCode)
            : RuleResult.HardStop(RecipientCode, $"{toUser.Trim()} is not an active user. Choose the incoming technologist from the list, or leave it blank.");
    }

    public static RuleResult EvaluateNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes)
            ? RuleResult.HardStop(NotesCode, "A shift handoff requires a note describing open work and risks.")
            : RuleResult.Pass(NotesCode);

    public static RuleResult EvaluateAcknowledge(string fromUser, string acknowledger, DateTime? acknowledgedUtc)
    {
        if (acknowledgedUtc is not null)
        {
            return RuleResult.HardStop(AlreadyAckCode, "This shift handoff was already acknowledged.");
        }

        if (string.IsNullOrWhiteSpace(acknowledger))
        {
            return RuleResult.HardStop(PermissionCode, "An acknowledger is required.");
        }

        if (string.Equals(fromUser?.Trim(), acknowledger.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(
                SelfAckCode,
                "The technologist who recorded the handoff cannot acknowledge it.");
        }

        return RuleResult.Pass(SelfAckCode);
    }
}
