using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Inventory;

/// <summary>
/// Filter criteria for inventory search. All filters are optional and combined with
/// AND. Null filters are ignored.
/// </summary>
public sealed record InventorySearchCriteria(
    string? UnitNumber = null,
    UnitStatus? Status = null,
    AboGroup? Abo = null,
    RhType? RhD = null,
    long? ProductTypeId = null,
    long? LocationId = null,
    DateTime? ExpiringBeforeUtc = null);
