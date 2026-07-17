using System.Linq.Expressions;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IRepository{TEntity}"/>. No hard-delete
/// method is provided, by design.
/// </summary>
public sealed class EfRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    private readonly BloodBankDbContext _context;

    public EfRepository(BloodBankDbContext context) => _context = context;

    public Task<TEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _context.Set<TEntity>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.Set<TEntity>().AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        await _context.Set<TEntity>().AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        _context.Set<TEntity>().FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        _context.Set<TEntity>().AnyAsync(predicate, cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await _context.Set<TEntity>().AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => _context.Set<TEntity>().Update(entity);

    public IQueryable<TEntity> Query() => _context.Set<TEntity>().AsQueryable();
}
