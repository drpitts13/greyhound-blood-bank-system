using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IInventoryRepository"/>.</summary>
public sealed class InventoryRepository : IInventoryRepository
{
    private static readonly UnitStatus[] ExpirableStatuses =
    {
        UnitStatus.Quarantine, UnitStatus.Available, UnitStatus.Allocated, UnitStatus.Returned,
        UnitStatus.Received, UnitStatus.Selected, UnitStatus.Assigned, UnitStatus.Crossmatched,
        UnitStatus.Expected, UnitStatus.ReturnPending, UnitStatus.Transferred, UnitStatus.CancelledAssignment
    };

    private readonly BloodBankDbContext _context;

    public InventoryRepository(BloodBankDbContext context) => _context = context;

    public Task<BloodUnit?> GetUnitAsync(long id, CancellationToken cancellationToken = default) =>
        _context.BloodUnits.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> UnitNumberExistsAsync(string unitNumber, CancellationToken cancellationToken = default) =>
        _context.BloodUnits.AnyAsync(u => u.UnitNumber == unitNumber, cancellationToken);

    public Task<bool> ComponentIdentityKeyExistsAsync(string componentIdentityKey, CancellationToken cancellationToken = default) =>
        _context.BloodUnits.AnyAsync(u => u.ComponentIdentityKey == componentIdentityKey, cancellationToken);

    public Task<BloodUnit?> GetByComponentIdentityAsync(string componentIdentity, CancellationToken cancellationToken = default) =>
        _context.BloodUnits.FirstOrDefaultAsync(
            u => u.ComponentIdentity == componentIdentity || u.UnitNumber == componentIdentity,
            cancellationToken);

    public async Task AddUnitAsync(BloodUnit unit, CancellationToken cancellationToken = default) =>
        await _context.BloodUnits.AddAsync(unit, cancellationToken);

    public async Task<IReadOnlyList<BloodUnit>> SearchAsync(InventorySearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = _context.BloodUnits.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.UnitNumber))
        {
            query = query.Where(u => u.UnitNumber == criteria.UnitNumber);
        }

        if (criteria.Status is not null)
        {
            query = query.Where(u => u.Status == criteria.Status);
        }

        if (criteria.Abo is not null)
        {
            query = query.Where(u => u.Abo == criteria.Abo);
        }

        if (criteria.RhD is not null)
        {
            query = query.Where(u => u.RhD == criteria.RhD);
        }

        if (criteria.ProductTypeId is not null)
        {
            query = query.Where(u => u.ProductTypeId == criteria.ProductTypeId);
        }

        if (criteria.LocationId is not null)
        {
            query = query.Where(u => u.CurrentLocationId == criteria.LocationId);
        }

        if (criteria.ExpiringBeforeUtc is not null)
        {
            query = query.Where(u => u.ExpiresUtc < criteria.ExpiringBeforeUtc);
        }

        return await query.OrderBy(u => u.ExpiresUtc).ToListAsync(cancellationToken);
    }

    public void AddStatusHistory(InventoryStatusHistory history) =>
        _context.InventoryStatusHistory.Add(history);

    public async Task<IReadOnlyList<InventoryStatusHistory>> GetHistoryAsync(long unitId, CancellationToken cancellationToken = default) =>
        await _context.InventoryStatusHistory
            .AsNoTracking()
            .Where(h => h.BloodProductId == unitId)
            .OrderBy(h => h.ChangedUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BloodUnit>> GetExpirableUnitsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default) =>
        await _context.BloodUnits
            .Where(u => u.ExpiresUtc <= asOfUtc && ExpirableStatuses.Contains(u.Status))
            .ToListAsync(cancellationToken);
}
