using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A closed reaction investigation is signed. Its workup and conclusions are then locked: changing them, or setting
/// the case back to open, needs an explicit reopen with a reason, and the case is closed again with a new signature.
/// Fatality follow-up (CBER notification, written report) is reporting, not findings, and stays available.
/// </summary>
public static class ReactionInvestigationLockRule
{
    public const string LockedCode = "RXN-CLOSED";
    public const string ReopenCode = "RXN-REOPEN";

    public static RuleResult EvaluateUpdate(ReactionInvestigationStatus current, bool changesFindings, ReactionInvestigationStatus? requestedStatus)
    {
        if (current != ReactionInvestigationStatus.Closed)
        {
            return RuleResult.Pass(LockedCode);
        }

        var reopening = requestedStatus is ReactionInvestigationStatus s && s != ReactionInvestigationStatus.Closed;
        return changesFindings || reopening
            ? RuleResult.HardStop(LockedCode,
                "This investigation is closed and signed. Reopen it with a reason to change it; it is then closed again with a new signature.")
            : RuleResult.Pass(LockedCode);
    }

    public static RuleResult EvaluateReopen(ReactionInvestigationStatus current, string? reason)
    {
        if (current != ReactionInvestigationStatus.Closed)
        {
            return RuleResult.HardStop(ReopenCode, "Only a closed investigation can be reopened.");
        }

        return string.IsNullOrWhiteSpace(reason)
            ? RuleResult.HardStop(ReopenCode, "Give the reason for reopening this signed investigation.")
            : RuleResult.Pass(ReopenCode);
    }
}
