using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Records append-only configuration change snapshots (before/after JSON + version) for
/// versioned admin configuration. Staged on the same unit of work as the change, so it
/// commits or rolls back atomically. Complements <see cref="IAuditWriter"/>, which records
/// the global action-log event.
/// </summary>
public interface IConfigurationHistoryWriter
{
    void Capture(
        string entityType,
        long? entityId,
        int version,
        ConfigChangeAction action,
        object? oldValue = null,
        object? newValue = null,
        string? reason = null,
        long? signatureId = null);
}
