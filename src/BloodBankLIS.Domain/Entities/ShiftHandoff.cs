using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// End-of-shift packet. Captures the queue counts already used on the dashboard
/// plus a required note. Acknowledgment is a separate user action and is audited.
/// Counts do not change issue, result, or investigation gates.
/// </summary>
public class ShiftHandoff : BaseEntity
{
    public DateTime RecordedUtc { get; set; }

    public string FromUser { get; set; } = string.Empty;

    public string? ToUser { get; set; }

    public string Notes { get; set; } = string.Empty;

    public int RetrospectiveDue { get; set; }

    public int RetrospectiveOverdue { get; set; }

    public int OutstandingIssued { get; set; }

    public int InTransit { get; set; }

    public int InTransitOverdue { get; set; }

    public int PendingTests { get; set; }

    public int PendingSpecimens { get; set; }

    public int OpenReactions { get; set; }

    public int FatalityFollowUpDue { get; set; }

    public string? AcknowledgedBy { get; set; }

    public DateTime? AcknowledgedUtc { get; set; }
}
