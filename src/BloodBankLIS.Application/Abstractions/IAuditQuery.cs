using BloodBankLIS.Domain.Audit;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>Read-only access to append-only audit rows (no update or delete).</summary>
public interface IAuditQuery
{
    Task<IReadOnlyList<AuditEvent>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Rows for one record, oldest first (uses the EntityType/EntityId index).</summary>
    Task<IReadOnlyList<AuditEvent>> ListForEntityAsync(string entityType, long entityId, CancellationToken cancellationToken = default);
}
