using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One database backup attempt and its verification. The manifest (audit event count and
/// last audit hash inside the backup) is what a restore is reconciled against
/// (docs/DOWNTIME_PLAN.md section 4).
/// </summary>
public class DatabaseBackup : BaseEntity
{
    public DateTime StartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string Trigger { get; set; } = string.Empty;

    public string RequestedBy { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public long? SizeBytes { get; set; }

    public string? Sha256 { get; set; }

    public bool Succeeded { get; set; }

    public bool Verified { get; set; }

    public string? VerifyDetail { get; set; }

    public long? AuditEventCount { get; set; }

    public string? LastAuditHash { get; set; }

    public string? Error { get; set; }

    public DateTime? FileDeletedUtc { get; set; }
}
