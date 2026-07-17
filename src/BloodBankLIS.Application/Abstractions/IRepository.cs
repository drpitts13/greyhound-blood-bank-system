using System.Linq.Expressions;
using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Generic persistence abstraction for an auditable entity. Intentionally exposes
/// no hard-delete operation: clinical data is voided/superseded via status, never
/// physically deleted (see docs/erd.md conventions).
/// </summary>
public interface IRepository<TEntity> where TEntity : BaseEntity
{
    /// <summary>Tracked lookup by surrogate key (use when the entity will be mutated).</summary>
    Task<TEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>No-tracking filtered read.</summary>
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>Tracked first-or-default (use when the entity will be mutated).</summary>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    /// <summary>Composable query for searches/filters. Implemented over the data store.</summary>
    IQueryable<TEntity> Query();
}
