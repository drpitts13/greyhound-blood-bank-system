using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Inventory-specific persistence operations, including the append-only status
/// history. Unit reads here are tracked so status/location changes persist.
/// </summary>
public interface IInventoryRepository
{
    Task<BloodUnit?> GetUnitAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> UnitNumberExistsAsync(string unitNumber, CancellationToken cancellationToken = default);

    Task<bool> ComponentIdentityKeyExistsAsync(string componentIdentityKey, CancellationToken cancellationToken = default);

    Task<BloodUnit?> GetByComponentIdentityAsync(string componentIdentity, CancellationToken cancellationToken = default);

    Task AddUnitAsync(BloodUnit unit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BloodUnit>> SearchAsync(InventorySearchCriteria criteria, CancellationToken cancellationToken = default);

    void AddStatusHistory(InventoryStatusHistory history);

    Task<IReadOnlyList<InventoryStatusHistory>> GetHistoryAsync(long unitId, CancellationToken cancellationToken = default);

    /// <summary>Units that are at/past expiration and still in a non-terminal, expirable status.</summary>
    Task<IReadOnlyList<BloodUnit>> GetExpirableUnitsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);

    Task<ProductType?> GetProductTypeAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Units waiting for a product ABO/Rh retype (Received + product RequiresRetype).</summary>
    Task<IReadOnlyList<BloodUnit>> ListPendingRetypeAsync(CancellationToken cancellationToken = default);
}
