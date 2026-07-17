namespace BloodBankLIS.Domain.Common;

/// <summary>
/// Base type for persisted entities. Carries audit metadata and an optimistic
/// concurrency token. Clinical entities are never hard-deleted; status/void
/// columns and history tables preserve prior state (see docs/erd.md).
/// </summary>
public abstract class BaseEntity
{
    public long Id { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>User identifier (username) that created the row.</summary>
    public string CreatedBy { get; set; } = "system";

    public DateTime? ModifiedUtc { get; set; }

    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Mapped to SQL Server <c>rowversion</c>;
    /// on non-SQL-Server providers it is treated as a plain column.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
