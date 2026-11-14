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
        UnitStatus.Expected, UnitStatus.ReturnPending, UnitStatus.Transferred, UnitStatus.CancelledAssignment,
        UnitStatus.OnHold
    };

    private readonly BloodBankDbContext _context;

    public InventoryRepository(BloodBankDbContext context) => _context = context;

    public Task<BloodUnit?> GetUnitAsync(long id, CancellationToken cancellationToken = default) =>
        _context.BloodUnits.Include(u => u.ProductType).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> UnitNumberExistsAsync(string unitNumber, CancellationToken cancellationToken = default) =>
        _context.BloodUnits.AnyAsync(u => u.UnitNumber == unitNumber, cancellationToken);

    public Task<bool> ComponentIdentityKeyExistsAsync(string componentIdentityKey, CancellationToken cancellationToken = default) =>
        _context.BloodUnits.AnyAsync(u => u.ComponentIdentityKey == componentIdentityKey, cancellationToken);

    public async Task<IReadOnlyList<string>> ListUsedDivisionCodesAsync(
        string din,
        string productDescriptionCode,
        string collectionType,
        CancellationToken cancellationToken = default)
    {
        var prefix = productDescriptionCode + collectionType;
        return await _context.BloodUnits.AsNoTracking()
            .Where(u => u.Din == din
                && u.DivisionCode != null
                && u.DivisionCode != ""
                && (u.ProductDescriptionCode == productDescriptionCode && u.CollectionTypeCode == collectionType
                    || u.ProductCodeData != null && u.ProductCodeData.StartsWith(prefix)))
            .Select(u => u.DivisionCode!)
            .ToListAsync(cancellationToken);
    }

    public Task<BloodUnit?> GetByComponentIdentityAsync(string componentIdentity, CancellationToken cancellationToken = default) =>
        _context.BloodUnits.FirstOrDefaultAsync(
            u => u.ComponentIdentity == componentIdentity || u.UnitNumber == componentIdentity,
            cancellationToken);

    public async Task<IReadOnlyList<BloodUnit>> ListByUnitNumberOrDinAsync(string value, CancellationToken cancellationToken = default)
    {
        var upper = value.Trim().ToUpperInvariant();
        return await _context.BloodUnits
            .Where(u => u.UnitNumber.ToUpper() == upper || (u.Din != null && u.Din.ToUpper() == upper))
            .ToListAsync(cancellationToken);
    }

    public async Task AddUnitAsync(BloodUnit unit, CancellationToken cancellationToken = default) =>
        await _context.BloodUnits.AddAsync(unit, cancellationToken);

    public async Task<IReadOnlyList<BloodUnit>> SearchAsync(InventorySearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = _context.BloodUnits.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.UnitNumber))
        {
            var term = criteria.UnitNumber.Trim().ToLower();
            query = query.Where(u => u.UnitNumber.ToLower().Contains(term));
        }

        if (criteria.Status is not null || criteria.Statuses is { Count: > 0 })
        {
            var statuses = new List<UnitStatus>();
            if (criteria.Status is not null)
            {
                statuses.Add(criteria.Status.Value);
            }

            if (criteria.Statuses is { Count: > 0 })
            {
                statuses.AddRange(criteria.Statuses);
            }

            query = query.Where(u => statuses.Contains(u.Status));
        }
        else if (criteria.ExcludeIssuedAndTransfused)
        {
            query = query.Where(u =>
                u.Status != UnitStatus.Issued
                && u.Status != UnitStatus.Transfused
                && u.Status != UnitStatus.TransfusionStarted
                && u.Status != UnitStatus.TransfusionStopped);
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

    public Task<ProductType?> GetProductTypeAsync(long id, CancellationToken cancellationToken = default) =>
        _context.ProductTypes.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BloodUnit>> ListPendingRetypeAsync(CancellationToken cancellationToken = default) =>
        await _context.BloodUnits
            .AsNoTracking()
            .Include(u => u.ProductType)
            .Where(u => u.Status == UnitStatus.Received && u.ProductType!.RequiresRetype)
            .OrderBy(u => u.CreatedUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BloodUnit>> ListShippedOutAsync(DateTime? sinceUtc, CancellationToken cancellationToken = default) =>
        await _context.BloodUnits
            .AsNoTracking()
            .Where(u => u.Status == UnitStatus.ShippedOut && u.ShippedToFacility != null
                && (sinceUtc == null || (u.ShippedUtc != null && u.ShippedUtc >= sinceUtc)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<(BloodUnit Unit, InventoryStatusHistory Returned)>> ListReturnedToSupplierAsync(
        DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var units = await _context.BloodUnits
            .AsNoTracking()
            .Where(u => u.Status == UnitStatus.ReturnedToSupplier)
            .ToListAsync(cancellationToken);
        if (units.Count == 0)
        {
            return [];
        }

        var ids = units.Select(u => u.Id).ToList();
        var rows = await _context.InventoryStatusHistory
            .AsNoTracking()
            .Where(h => ids.Contains(h.BloodProductId) && h.ToStatus == UnitStatus.ReturnedToSupplier && h.ChangedUtc >= sinceUtc)
            .ToListAsync(cancellationToken);
        // The latest return of each unit: a unit is returned once, but a corrected status could repeat it.
        var latest = rows.GroupBy(h => h.BloodProductId).ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.ChangedUtc).ThenByDescending(h => h.Id).First());
        return units
            .Where(u => latest.ContainsKey(u.Id))
            .Select(u => (u, latest[u.Id]))
            .ToList();
    }
}
