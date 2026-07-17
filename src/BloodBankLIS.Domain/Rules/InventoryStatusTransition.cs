using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Guards blood-unit status transitions. Any transition not explicitly allowed
/// is a HardStop (see docs/safety-rules.md section 4). Pure and deterministic.
/// </summary>
public static class InventoryStatusTransition
{
    public const string IllegalTransitionCode = "INV-ILLEGAL-TRANSITION";

    private static readonly IReadOnlyDictionary<UnitStatus, UnitStatus[]> Allowed =
        new Dictionary<UnitStatus, UnitStatus[]>
        {
            [UnitStatus.Quarantine] = new[] { UnitStatus.Available, UnitStatus.Discarded, UnitStatus.Expired },
            [UnitStatus.Available] = new[] { UnitStatus.Allocated, UnitStatus.Quarantine, UnitStatus.Discarded, UnitStatus.Expired },
            [UnitStatus.Allocated] = new[] { UnitStatus.Issued, UnitStatus.Available, UnitStatus.Discarded, UnitStatus.Expired },
            [UnitStatus.Issued] = new[] { UnitStatus.Transfused, UnitStatus.Returned },
            [UnitStatus.Returned] = new[] { UnitStatus.Available, UnitStatus.Quarantine, UnitStatus.Discarded, UnitStatus.Expired },
            [UnitStatus.Transfused] = Array.Empty<UnitStatus>(),
            [UnitStatus.Discarded] = Array.Empty<UnitStatus>(),
            [UnitStatus.Expired] = new[] { UnitStatus.Discarded }
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
