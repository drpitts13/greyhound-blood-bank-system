using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Status sets used when looking up units for action pickers.
/// On-hand excludes terminal and not-yet-received statuses.
/// </summary>
public static class InventoryUnitScopes
{
    public static readonly IReadOnlyList<UnitStatus> Available = [UnitStatus.Available];

    public static readonly IReadOnlyList<UnitStatus> Issuable =
    [
        UnitStatus.Available,
        UnitStatus.Allocated,
        UnitStatus.Assigned,
        UnitStatus.Crossmatched,
        UnitStatus.Selected
    ];

    public static readonly IReadOnlyList<UnitStatus> OnHand =
    [
        UnitStatus.Quarantine,
        UnitStatus.Available,
        UnitStatus.Allocated,
        UnitStatus.Returned,
        UnitStatus.Received,
        UnitStatus.Selected,
        UnitStatus.Assigned,
        UnitStatus.Crossmatched,
        UnitStatus.ReturnPending,
        UnitStatus.CancelledAssignment,
        UnitStatus.OnHold
    ];

    /// <summary>
    /// Crossmatch entry includes issued and transfused units so retrospective
    /// emergency / MTP follow-up can name the unit already released.
    /// </summary>
    public static readonly IReadOnlyList<UnitStatus> Crossmatch =
    [
        ..OnHand,
        UnitStatus.Issued,
        UnitStatus.Transfused
    ];
}
