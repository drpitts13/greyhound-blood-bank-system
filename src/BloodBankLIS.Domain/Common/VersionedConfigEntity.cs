namespace BloodBankLIS.Domain.Common;

/// <summary>
/// Base type for clinically significant configuration that must be versioned, validated,
/// and audited rather than freely overwritten. The row itself is always the live version;
/// the change history (snapshots) lives in <c>ConfigurationChangeHistory</c>. Significant
/// edits bump <see cref="Version"/> and require a <see cref="ChangeReason"/>; activation is
/// gated by validation. Soft lifecycle only — config is deactivated/retired, never deleted.
/// </summary>
public abstract class VersionedConfigEntity : BaseEntity
{
    /// <summary>Monotonic version of this configuration record; incremented on significant edits.</summary>
    public int Version { get; set; } = 1;

    /// <summary>When this version becomes effective. Defaults to creation time on activation.</summary>
    public DateTime? EffectiveUtc { get; set; }

    /// <summary>When this record was retired/superseded; null while live.</summary>
    public DateTime? RetiredUtc { get; set; }

    /// <summary>True when the record is active and usable by clinical workflows.</summary>
    public bool IsActive { get; set; }

    /// <summary>True while the record is an unactivated draft.</summary>
    public bool IsDraft { get; set; } = true;

    /// <summary>Placeholder for an approval workflow (not enforced in the foundation phase).</summary>
    public bool IsPendingApproval { get; set; }

    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedUtc { get; set; }

    /// <summary>Reason recorded for the most recent significant change.</summary>
    public string? ChangeReason { get; set; }
}
