using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Guards blood-unit status transitions. Any transition not explicitly allowed
/// is a HardStop (see docs/safety-rules.md). Pure and deterministic.
/// INSTITUTIONAL_POLICY_REVIEW: confirm facility-specific exception paths.
/// </summary>
public static class InventoryStatusTransition
{
    public const string IllegalTransitionCode = IsbtErrorCodes.InvalidStatusTransition;

    private static readonly IReadOnlyDictionary<UnitStatus, UnitStatus[]> Allowed =
        new Dictionary<UnitStatus, UnitStatus[]>
        {
            [UnitStatus.Expected] =
            [
                UnitStatus.Received, UnitStatus.Quarantine, UnitStatus.CancelledAssignment,
                UnitStatus.Missing, UnitStatus.Discarded
            ],
            [UnitStatus.Received] =
            [
                UnitStatus.Available, UnitStatus.Quarantine, UnitStatus.OnHold, UnitStatus.Discarded,
                UnitStatus.Expired, UnitStatus.Recalled, UnitStatus.Damaged, UnitStatus.Missing
            ],
            [UnitStatus.Quarantine] =
            [
                UnitStatus.Available, UnitStatus.Discarded, UnitStatus.Expired, UnitStatus.Recalled,
                UnitStatus.Damaged, UnitStatus.Missing
            ],
            [UnitStatus.OnHold] =
            [
                UnitStatus.Available, UnitStatus.Quarantine, UnitStatus.Discarded, UnitStatus.Expired,
                UnitStatus.Recalled, UnitStatus.Damaged, UnitStatus.Missing
            ],
            [UnitStatus.Available] =
            [
                UnitStatus.Selected, UnitStatus.Assigned, UnitStatus.Allocated, UnitStatus.Crossmatched,
                UnitStatus.Quarantine, UnitStatus.OnHold, UnitStatus.Discarded, UnitStatus.Expired,
                UnitStatus.Recalled, UnitStatus.Transferred, UnitStatus.Missing, UnitStatus.Damaged,
                UnitStatus.Modified
            ],
            [UnitStatus.Selected] =
            [
                UnitStatus.Assigned, UnitStatus.Crossmatched, UnitStatus.Allocated, UnitStatus.Available,
                UnitStatus.CancelledAssignment, UnitStatus.Quarantine, UnitStatus.OnHold,
                UnitStatus.Discarded, UnitStatus.Expired, UnitStatus.Missing, UnitStatus.Damaged
            ],
            [UnitStatus.Assigned] =
            [
                UnitStatus.Crossmatched, UnitStatus.Issued, UnitStatus.Available, UnitStatus.CancelledAssignment,
                UnitStatus.Quarantine, UnitStatus.OnHold, UnitStatus.Discarded, UnitStatus.Expired,
                UnitStatus.Allocated, UnitStatus.Missing, UnitStatus.Damaged
            ],
            // Legacy synonym for Assigned during transition period.
            [UnitStatus.Allocated] =
            [
                UnitStatus.Issued, UnitStatus.Available, UnitStatus.Assigned, UnitStatus.Crossmatched,
                UnitStatus.CancelledAssignment, UnitStatus.Discarded, UnitStatus.Expired,
                UnitStatus.Quarantine, UnitStatus.OnHold, UnitStatus.Missing, UnitStatus.Damaged
            ],
            [UnitStatus.Crossmatched] =
            [
                UnitStatus.Issued, UnitStatus.Available, UnitStatus.CancelledAssignment, UnitStatus.Assigned,
                UnitStatus.Discarded, UnitStatus.Expired, UnitStatus.Quarantine, UnitStatus.OnHold,
                UnitStatus.Missing, UnitStatus.Damaged
            ],
            [UnitStatus.Issued] =
            [
                UnitStatus.TransfusionStarted, UnitStatus.Transfused, UnitStatus.ReturnPending,
                UnitStatus.Returned, UnitStatus.Recalled, UnitStatus.Missing, UnitStatus.Damaged
            ],
            [UnitStatus.TransfusionStarted] =
            [
                UnitStatus.Transfused, UnitStatus.TransfusionStopped
            ],
            [UnitStatus.TransfusionStopped] =
            [
                UnitStatus.Discarded, UnitStatus.Quarantine, UnitStatus.Returned
            ],
            [UnitStatus.ReturnPending] =
            [
                UnitStatus.Returned, UnitStatus.Available, UnitStatus.Quarantine, UnitStatus.Discarded
            ],
            [UnitStatus.Returned] =
            [
                UnitStatus.Available, UnitStatus.Quarantine, UnitStatus.OnHold, UnitStatus.Discarded,
                UnitStatus.Expired, UnitStatus.Missing, UnitStatus.Damaged
            ],
            [UnitStatus.Transferred] =
            [
                UnitStatus.Available, UnitStatus.Quarantine, UnitStatus.OnHold, UnitStatus.Received,
                UnitStatus.Missing, UnitStatus.Damaged
            ],
            [UnitStatus.CancelledAssignment] =
            [
                UnitStatus.Available, UnitStatus.Quarantine, UnitStatus.OnHold, UnitStatus.Discarded,
                UnitStatus.Missing, UnitStatus.Damaged
            ],
            [UnitStatus.Recalled] =
            [
                UnitStatus.Quarantine, UnitStatus.Discarded, UnitStatus.Missing
            ],
            [UnitStatus.Missing] =
            [
                UnitStatus.Available, UnitStatus.Quarantine, UnitStatus.Discarded, UnitStatus.Damaged
            ],
            [UnitStatus.Damaged] =
            [
                UnitStatus.Discarded, UnitStatus.Quarantine
            ],
            [UnitStatus.Transfused] = Array.Empty<UnitStatus>(),
            [UnitStatus.Discarded] = Array.Empty<UnitStatus>(),
            [UnitStatus.Expired] = [UnitStatus.Discarded, UnitStatus.Quarantine],
            [UnitStatus.Modified] = Array.Empty<UnitStatus>()
        };

    public static bool IsAllowed(UnitStatus from, UnitStatus to) =>
        from == to || (Allowed.TryGetValue(from, out var targets) && targets.Contains(to));

    public static RuleResult Evaluate(UnitStatus from, UnitStatus to)
    {
        return IsAllowed(from, to)
            ? RuleResult.Pass(IllegalTransitionCode)
            : RuleResult.HardStop(IllegalTransitionCode, $"Transition {from} -> {to} is not permitted.");
    }
}
