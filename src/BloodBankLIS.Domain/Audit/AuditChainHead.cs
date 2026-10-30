namespace BloodBankLIS.Domain.Audit;

/// <summary>
/// Single-row pointer to the newest <see cref="AuditEvent.RecordHash"/> in the
/// audit hash chain. Writers lock this row inside their save transaction, so
/// concurrent saves extend the chain one after another instead of forking it.
/// Does not derive from BaseEntity: it is never audited itself.
/// </summary>
public class AuditChainHead
{
    /// <summary>The only row's key.</summary>
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>
    /// RecordHash of the last chained AuditEvent, or <c>GENESIS</c> when no
    /// hashed row exists yet.
    /// </summary>
    public string TipHash { get; set; } = string.Empty;
}
