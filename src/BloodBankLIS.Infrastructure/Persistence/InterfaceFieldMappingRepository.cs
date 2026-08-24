using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public sealed class InterfaceFieldMappingRepository : IInterfaceFieldMappingRepository
{
    private readonly BloodBankDbContext _context;
    private readonly EfRepository<InterfaceFieldMapping> _inner;

    public InterfaceFieldMappingRepository(BloodBankDbContext context)
    {
        _context = context;
        _inner = new EfRepository<InterfaceFieldMapping>(context);
    }

    public Task<InterfaceFieldMapping?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _inner.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<InterfaceFieldMapping>> ListAsync(CancellationToken cancellationToken = default) =>
        _inner.ListAsync(cancellationToken);

    public Task<IReadOnlyList<InterfaceFieldMapping>> ListAsync(
        System.Linq.Expressions.Expression<Func<InterfaceFieldMapping, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _inner.ListAsync(predicate, cancellationToken);

    public Task<InterfaceFieldMapping?> FirstOrDefaultAsync(
        System.Linq.Expressions.Expression<Func<InterfaceFieldMapping, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _inner.FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<bool> AnyAsync(
        System.Linq.Expressions.Expression<Func<InterfaceFieldMapping, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _inner.AnyAsync(predicate, cancellationToken);

    public Task AddAsync(InterfaceFieldMapping entity, CancellationToken cancellationToken = default) =>
        _inner.AddAsync(entity, cancellationToken);

    public void Update(InterfaceFieldMapping entity) => _inner.Update(entity);

    public IQueryable<InterfaceFieldMapping> Query() => _inner.Query();

    public async Task ReplaceForEndpointAsync(
        InterfaceEndpoint endpoint,
        IReadOnlyList<InterfaceFieldMapping> mappings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(mappings);

        var existing = await _context.InterfaceFieldMappings
            .Where(m => m.EndpointId == endpoint.Id)
            .ToListAsync(cancellationToken);
        _context.InterfaceFieldMappings.RemoveRange(existing);

        foreach (var row in mappings)
        {
            row.EndpointId = endpoint.Id;
            row.Endpoint = endpoint;
            await _context.InterfaceFieldMappings.AddAsync(row, cancellationToken);
        }
    }
}
