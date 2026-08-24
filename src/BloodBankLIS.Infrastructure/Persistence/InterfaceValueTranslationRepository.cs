using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public sealed class InterfaceValueTranslationRepository : IInterfaceValueTranslationRepository
{
    private readonly BloodBankDbContext _context;
    private readonly EfRepository<InterfaceValueTranslation> _inner;

    public InterfaceValueTranslationRepository(BloodBankDbContext context)
    {
        _context = context;
        _inner = new EfRepository<InterfaceValueTranslation>(context);
    }

    public Task<InterfaceValueTranslation?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _inner.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<InterfaceValueTranslation>> ListAsync(CancellationToken cancellationToken = default) =>
        _inner.ListAsync(cancellationToken);

    public Task<IReadOnlyList<InterfaceValueTranslation>> ListAsync(
        System.Linq.Expressions.Expression<Func<InterfaceValueTranslation, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _inner.ListAsync(predicate, cancellationToken);

    public Task<InterfaceValueTranslation?> FirstOrDefaultAsync(
        System.Linq.Expressions.Expression<Func<InterfaceValueTranslation, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _inner.FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<bool> AnyAsync(
        System.Linq.Expressions.Expression<Func<InterfaceValueTranslation, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        _inner.AnyAsync(predicate, cancellationToken);

    public Task AddAsync(InterfaceValueTranslation entity, CancellationToken cancellationToken = default) =>
        _inner.AddAsync(entity, cancellationToken);

    public void Update(InterfaceValueTranslation entity) => _inner.Update(entity);

    public IQueryable<InterfaceValueTranslation> Query() => _inner.Query();

    public async Task ReplaceForDataItemAsync(
        string dataItemKey,
        IReadOnlyList<InterfaceValueTranslation> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataItemKey);
        ArgumentNullException.ThrowIfNull(rows);

        var existing = await _context.InterfaceValueTranslations
            .Where(m => m.DataItemKey == dataItemKey)
            .ToListAsync(cancellationToken);
        _context.InterfaceValueTranslations.RemoveRange(existing);

        foreach (var row in rows)
        {
            row.DataItemKey = dataItemKey;
            await _context.InterfaceValueTranslations.AddAsync(row, cancellationToken);
        }
    }
}
