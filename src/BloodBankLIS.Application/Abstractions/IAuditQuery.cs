using BloodBankLIS.Domain.Audit;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>Read-only access to append-only audit rows (no update or delete).</summary>
public interface IAuditQuery
{
    Task<IReadOnlyList<AuditEvent>> ListAsync(CancellationToken cancellationToken = default);
}
