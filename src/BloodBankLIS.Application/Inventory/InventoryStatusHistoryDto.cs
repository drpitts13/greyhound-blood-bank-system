using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Inventory;

public sealed record InventoryStatusHistoryDto(
    long Id,
    long BloodProductId,
    UnitStatus? FromStatus,
    UnitStatus ToStatus,
    long? FromLocationId,
    long? ToLocationId,
    string? Reason,
    string ChangedBy,
    DateTime ChangedUtc)
{
    public static InventoryStatusHistoryDto From(InventoryStatusHistory h) => new(
        h.Id, h.BloodProductId, h.FromStatus, h.ToStatus, h.FromLocationId, h.ToLocationId,
        h.Reason, h.ChangedBy, h.ChangedUtc);
}
