using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Application.Services;

/// <summary>
/// Reusable CRUD orchestration for an auditable entity. Audit metadata and
/// Create/Update audit events are produced automatically by the persistence
/// layer's SaveChanges pipeline, so no clinical create/update can bypass audit.
/// Deliberately offers no delete operation.
/// </summary>
public class EntityCrudService<TEntity> where TEntity : BaseEntity
{
    private readonly IRepository<TEntity> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public EntityCrudService(IRepository<TEntity> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public Task<TEntity?> GetAsync(long id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        _repository.ListAsync(cancellationToken);

    public async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _repository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _repository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
