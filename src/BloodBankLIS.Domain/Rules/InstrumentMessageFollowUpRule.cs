using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Follow-up of a failed instrument message, mirroring the HL7 error queue: it stays open until a replay applies it or
/// someone resolves it with a reason. Only an open failed message may be replayed, so results that already filed are
/// never filed twice.
/// </summary>
public static class InstrumentMessageFollowUpRule
{
    public const string ReplayCode = "INST-REPLAY";
    public const string ResolveCode = "INST-RESOLVE";
    public const string ReleaseCode = "INST-HOLD-RELEASE";

    /// <summary>The hold code the instrument processor writes when an error or flag code held a result.</summary>
    public const string HeldMarker = "INST-HOLD";

    public const string ReplayResolutionNote = "Resolved by a successful replay of the stored message.";

    public static bool IsOpenFailure(InstrumentMessageStatus status, DateTime? resolvedUtc) =>
        status == InstrumentMessageStatus.Failed && resolvedUtc is null;

    public static RuleResult EvaluateReplay(InstrumentMessageStatus status, DateTime? resolvedUtc)
    {
        if (resolvedUtc is not null)
        {
            return RuleResult.HardStop(ReplayCode, "This message was already resolved, so it is not replayed.");
        }

        return status == InstrumentMessageStatus.Failed
            ? RuleResult.Pass(ReplayCode)
            : RuleResult.HardStop(ReplayCode,
                $"Only a failed message can be replayed. This one is {status}, so replaying it could file its results twice.");
    }

    /// <summary>An open failure whose result an instrument error or flag code held for review.</summary>
    public static bool IsHeld(InstrumentMessageStatus status, DateTime? resolvedUtc, string? errorDetail) =>
        IsOpenFailure(status, resolvedUtc)
        && errorDetail?.Contains(HeldMarker, StringComparison.Ordinal) == true;

    /// <summary>
    /// Releasing a held result posts it with the flag recorded, after someone has reviewed it. Only a held, open
    /// failure qualifies (other failures are fixed and replayed), and the reason is required.
    /// </summary>
    public static RuleResult EvaluateRelease(InstrumentMessageStatus status, DateTime? resolvedUtc, string? errorDetail, string? reason)
    {
        if (!IsOpenFailure(status, resolvedUtc))
        {
            return RuleResult.HardStop(ReleaseCode, resolvedUtc is not null
                ? "This message is already resolved, so nothing is held."
                : "Only a held result waiting in instrument failures can be released.");
        }

        if (!IsHeld(status, resolvedUtc, errorDetail))
        {
            return RuleResult.HardStop(ReleaseCode,
                "This message failed for another reason, not an instrument error code. Fix the cause and replay it, or resolve it.");
        }

        return string.IsNullOrWhiteSpace(reason)
            ? RuleResult.HardStop(ReleaseCode, "Say why the flagged result can be posted, for example that the reactions and the instrument's QC were reviewed.")
            : RuleResult.Pass(ReleaseCode);
    }

    public static string ReleaseResolutionNote(string reason) => $"Held result released and posted: {reason.Trim()}";

    public static RuleResult EvaluateResolve(InstrumentMessageStatus status, DateTime? resolvedUtc, string? reason)
    {
        if (!IsOpenFailure(status, resolvedUtc))
        {
            return RuleResult.HardStop(ResolveCode, resolvedUtc is not null
                ? "This message is already resolved."
                : "Only a failed message needs resolving.");
        }

        return string.IsNullOrWhiteSpace(reason)
            ? RuleResult.HardStop(ResolveCode, "Say how the failed message was handled, for example that the result was entered by hand.")
            : RuleResult.Pass(ResolveCode);
    }
}
