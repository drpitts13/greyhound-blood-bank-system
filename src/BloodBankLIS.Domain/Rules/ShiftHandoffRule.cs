namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Shift handoff requires a note, <c>audit.read</c>, and a different acknowledger.
/// Queue counts on the packet are informational and do not change clinical gates.
/// </summary>
public static class ShiftHandoffRule
{
    public const string PermissionCode = "SHIFT-HANDOFF-PERM";
    public const string NotesCode = "SHIFT-HANDOFF-NOTES";
    public const string SelfAckCode = "SHIFT-HANDOFF-SELF";
    public const string AlreadyAckCode = "SHIFT-HANDOFF-ACKED";

    public static RuleResult EvaluatePermission(bool hasAuditRead) =>
        hasAuditRead
            ? RuleResult.Pass(PermissionCode)
            : RuleResult.HardStop(
                PermissionCode,
                "Recording or acknowledging a shift handoff requires the audit.read permission.");

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
