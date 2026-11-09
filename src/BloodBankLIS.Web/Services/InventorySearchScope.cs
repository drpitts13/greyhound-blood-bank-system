using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// Which units the Inventory search lists (Cycle 606). With no status chosen and no unit number typed it lists the
/// units still on a shelf: on hand, and expired units waiting to be discarded. Discarded, modified (the stock moved to
/// the new product), issued, transfused, shipped, and returned-to-supplier units are left out unless asked for. A typed
/// unit number or a chosen status finds any unit: before, a scanned issued unit was not found with the box unticked.
/// </summary>
public static class InventorySearchScope
{
    public static IReadOnlyList<UnitStatus>? Statuses(string? unitNumber, UnitStatus? status, bool includeNotInStock) =>
        includeNotInStock || status is not null || !string.IsNullOrWhiteSpace(unitNumber)
            ? null
            : InventoryUnitScopes.OnShelf;
}
