using System.Linq.Expressions;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Application.Tests;

/// <summary>In-memory repository for testing application orchestration without a database.</summary>
public sealed class FakeRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    private readonly List<TEntity> _store = new();
    private long _nextId = 1;

    public IReadOnlyList<TEntity> Store => _store;

    public Task<TEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((IReadOnlyList<TEntity>)_store.ToList());

    public Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Task.FromResult((IReadOnlyList<TEntity>)_store.AsQueryable().Where(predicate).ToList());

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.AsQueryable().FirstOrDefault(predicate));

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.AsQueryable().Any(predicate));

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == 0)
        {
            entity.Id = _nextId++;
        }

        _store.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(TEntity entity)
    {
        // Already mutated in place for the in-memory store; nothing to persist here.
    }

    public IQueryable<TEntity> Query() => _store.AsQueryable();
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}
