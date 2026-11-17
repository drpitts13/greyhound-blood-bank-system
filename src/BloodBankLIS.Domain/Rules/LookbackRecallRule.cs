using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Cycle 687: which components of a recalled donation are left as they are, and why. A component whose status cannot
/// become Recalled (it is no longer here, or was turned into other units, or has expired) is not recalled; the rest of the
/// donation still is. Before, the first such component stopped the recall part way.
/// </summary>
public static class LookbackRecallRule
{
    /// <summary>Null when the component can be recalled (or is handled otherwise: transfused, shipped, already recalled, discarded).</summary>
    public static string? WhyNotRecalled(UnitStatus status) =>
        status is UnitStatus.Recalled or UnitStatus.Discarded
            or UnitStatus.Transfused or UnitStatus.TransfusionStarted or UnitStatus.TransfusionStopped or UnitStatus.ShippedOut
            || InventoryStatusTransition.IsAllowed(status, UnitStatus.Recalled)
            ? null
            : status switch
            {
                UnitStatus.ReturnedToSupplier => "already returned to the supplier; it is not here to recall",
                UnitStatus.Modified => "modified into other units; those are listed under this donation",
                UnitStatus.Expired => "expired; it cannot be recalled, so discard it",
                _ => $"is {status}; it cannot be recalled"
            };

    /// <summary>
    /// A component of the donation still on a shelf and not already held (quarantined, recalled or damaged): the DIN
    /// lookback names it, so a unit from an implicated donation is not missed while the recipients are being traced
    /// (end-user testing pass 216). Information only; whether to recall is the user's decision.
    /// </summary>
    public static bool StillInStock(UnitStatus status) =>
        StorageTemperatureRule.StoredStatuses.Contains(status)
        && status is not (UnitStatus.Quarantine or UnitStatus.Recalled or UnitStatus.Damaged);
}
