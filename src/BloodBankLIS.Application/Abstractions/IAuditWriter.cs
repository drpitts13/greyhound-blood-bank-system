using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Records explicit audit events for clinical actions (issue, verify, return,
/// discard, override, reprint, ...). The event is staged on the same unit of work
/// as the change it describes, so it commits or rolls back atomically with it.
///
/// Automatic Create/Update auditing of entities is handled by the persistence
/// layer's SaveChanges pipeline; this interface is for named domain actions.
/// </summary>
public interface IAuditWriter
{
    void Record(
        AuditEventType eventType,
        string entityType,
        long? entityId,
        object? oldValue = null,
        object? newValue = null,
        string? reason = null,
        long? signatureId = null);
}
