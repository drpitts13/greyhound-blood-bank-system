using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Operational work item for an interface failure (table <c>InterfaceErrorQueue</c>).
/// Retryable application errors land here with backoff metadata; resolve/replay
/// actions are audited (see docs/hl7-design.md sections 4-5).
/// </summary>
public class InterfaceErrorQueueItem : BaseEntity
{
    public long Hl7MessageId { get; set; }

    public Hl7MessageLog? Hl7Message { get; set; }

    public string ErrorType { get; set; } = string.Empty;

    public string ErrorDetail { get; set; } = string.Empty;

    public DateTime? NextRetryUtc { get; set; }

    public int RetryCount { get; set; }

    public bool Resolved { get; set; }

    public string? ResolvedBy { get; set; }

    public DateTime? ResolvedUtc { get; set; }

    /// <summary>Why the row was closed: the reason given by hand, or the replay or resend that delivered the message.</summary>
    public string? ResolutionNote { get; set; }
}
