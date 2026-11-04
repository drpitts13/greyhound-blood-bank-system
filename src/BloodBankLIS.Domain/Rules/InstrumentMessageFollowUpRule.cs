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
