using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Audit;

/// <summary>
/// An append-only audit record. Written in the same transaction as the change it
/// describes; the system exposes no update or delete path for audit (see
/// docs/architecture.md 4.1). Does not derive from BaseEntity because it has no
/// modifiable/concurrency metadata of its own.
/// </summary>
public class AuditEvent
{
    public long Id { get; set; }

    public AuditEventType EventType { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public long? EntityId { get; set; }

    /// <summary>Actor (username) responsible for the action.</summary>
    public string UserName { get; set; } = "system";

    public string? Workstation { get; set; }

    public DateTime OccurredUtc { get; set; }

    public string? OldValueJson { get; set; }

    public string? NewValueJson { get; set; }

    public string? Reason { get; set; }

    /// <summary>Reference to an electronic signature, when the action required one.</summary>
    public long? SignatureId { get; set; }

    /// <summary>Hosting environment name (Development/Production/...) when recorded; null on legacy/auto events.</summary>
    public string? Environment { get; set; }

    /// <summary>True when the action was performed while dev-mode (no-login) was active.</summary>
    public bool IsDevMode { get; set; }
}
