using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>Read-only access to append-only audit rows (no update or delete).</summary>
public interface IAuditQuery
{
    Task<IReadOnlyList<AuditEvent>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Rows for one record, oldest first (uses the EntityType/EntityId index).</summary>
    Task<IReadOnlyList<AuditEvent>> ListForEntityAsync(string entityType, long entityId, CancellationToken cancellationToken = default);

    /// <summary>One user's latest rows of one kind for one record type, newest first (at most <paramref name="take"/>).</summary>
    Task<IReadOnlyList<AuditEvent>> ListLatestForUserAsync(
        string userName, AuditEventType eventType, string entityType, int take, CancellationToken cancellationToken = default);
}
