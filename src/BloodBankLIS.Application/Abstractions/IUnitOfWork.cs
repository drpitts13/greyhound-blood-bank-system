namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Commits all pending changes (entities and their audit events) in a single
/// transaction. A failed audit write rolls back the business change.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
