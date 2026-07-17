using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Audit;

/// <summary>
/// Append-only versioned snapshot of a configuration change. Complements
/// <see cref="AuditEvent"/> (the global action log) by storing the before/after JSON of a
/// config record at a specific version, so the admin UI can show history and compare
/// versions. Written in the same transaction as the change it describes. There is no
/// update or delete path (see docs/architecture.md 4.1).
/// </summary>
public class ConfigurationChangeHistory
{
    public long Id { get; set; }

    /// <summary>CLR type name of the configuration entity (e.g. "TestDefinition").</summary>
    public string EntityType { get; set; } = string.Empty;

    public long? EntityId { get; set; }

    /// <summary>Version of the config record this snapshot represents.</summary>
    public int Version { get; set; }

    public ConfigChangeAction Action { get; set; }

    public string? OldValueJson { get; set; }

    public string? NewValueJson { get; set; }

    public string? ChangeReason { get; set; }

    public string ChangedBy { get; set; } = "system";

    public string? Workstation { get; set; }

    public DateTime ChangedUtc { get; set; }

    /// <summary>Hosting environment name when the change was made (Development/Production/...).</summary>
    public string? Environment { get; set; }

    /// <summary>True when the change was made while dev-mode (no-login) was active.</summary>
    public bool IsDevMode { get; set; }

    /// <summary>Optional electronic-signature reference for signed changes.</summary>
    public long? SignatureId { get; set; }
}
