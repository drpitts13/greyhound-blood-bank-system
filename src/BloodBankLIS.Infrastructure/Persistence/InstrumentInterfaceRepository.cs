using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public sealed class InstrumentInterfaceRepository : IInstrumentInterfaceRepository
{
    private readonly BloodBankDbContext _context;
    private readonly EfRepository<InstrumentInterface> _inner;

    public InstrumentInterfaceRepository(BloodBankDbContext context)
    {
        _context = context;
        _inner = new EfRepository<InstrumentInterface>(context);
    }

    public Task<InstrumentInterface?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _inner.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<InstrumentInterface>> ListAsync(CancellationToken cancellationToken = default) =>
        _inner.ListAsync(cancellationToken);

    public Task<IReadOnlyList<InstrumentInterface>> ListAsync(
        System.Linq.Expressions.Expression<Func<InstrumentInterface, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _inner.ListAsync(predicate, cancellationToken);

    public Task<InstrumentInterface?> FirstOrDefaultAsync(
        System.Linq.Expressions.Expression<Func<InstrumentInterface, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _inner.FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<bool> AnyAsync(
        System.Linq.Expressions.Expression<Func<InstrumentInterface, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _inner.AnyAsync(predicate, cancellationToken);

    public Task AddAsync(InstrumentInterface entity, CancellationToken cancellationToken = default) =>
        _inner.AddAsync(entity, cancellationToken);

    public void Update(InstrumentInterface entity) => _inner.Update(entity);

    public IQueryable<InstrumentInterface> Query() => _inner.Query();

    public async Task<IReadOnlyList<InstrumentInterface>> ListWithChildrenAsync(CancellationToken cancellationToken = default) =>
        await QueryWithChildren().ToListAsync(cancellationToken);

    public Task<InstrumentInterface?> GetWithChildrenAsync(long id, CancellationToken cancellationToken = default) =>
        QueryWithChildren().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task ReplaceChildrenAsync(
        InstrumentInterface entity,
        InstrumentConnection connection,
        IReadOnlyList<InstrumentFieldMapping> fieldMappings,
        IReadOnlyList<InstrumentTestMapping> testMappings,
        IReadOnlyList<InstrumentInterfaceRule> rules,
        CancellationToken cancellationToken = default)
    {
        var existingConnection = await _context.InstrumentConnections
            .Where(c => c.InterfaceId == entity.Id)
            .ToListAsync(cancellationToken);
        var existingFields = await _context.InstrumentFieldMappings
            .Where(m => m.InterfaceId == entity.Id)
            .ToListAsync(cancellationToken);
        var existingTests = await _context.InstrumentTestMappings
            .Where(m => m.InterfaceId == entity.Id)
            .ToListAsync(cancellationToken);
        var existingRules = await _context.InstrumentInterfaceRules
            .Where(m => m.InterfaceId == entity.Id)
            .ToListAsync(cancellationToken);

        _context.InstrumentFieldMappings.RemoveRange(existingFields);
        _context.InstrumentTestMappings.RemoveRange(existingTests);
        _context.InstrumentInterfaceRules.RemoveRange(existingRules);

        var existing = existingConnection.FirstOrDefault();
        if (existing is not null)
        {
            existing.IpAddress = connection.IpAddress;
            existing.Port = connection.Port;
        }
        else
        {
            connection.InterfaceId = entity.Id;
            connection.Interface = null;
            await _context.InstrumentConnections.AddAsync(connection, cancellationToken);
        }

        foreach (var row in fieldMappings)
        {
            row.InterfaceId = entity.Id;
            row.Interface = null;
            await _context.InstrumentFieldMappings.AddAsync(row, cancellationToken);
        }

        foreach (var row in testMappings)
        {
            row.InterfaceId = entity.Id;
            row.Interface = null;
            await _context.InstrumentTestMappings.AddAsync(row, cancellationToken);
        }

        foreach (var row in rules)
        {
            row.InterfaceId = entity.Id;
            row.Interface = null;
            await _context.InstrumentInterfaceRules.AddAsync(row, cancellationToken);
        }
    }

    private IQueryable<InstrumentInterface> QueryWithChildren() =>
        _context.InstrumentInterfaces
            .Include(i => i.Connection)
            .Include(i => i.FieldMappings)
            .Include(i => i.TestMappings)
            .Include(i => i.Rules);
}
