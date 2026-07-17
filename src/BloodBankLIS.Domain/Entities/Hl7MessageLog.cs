using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Persisted record of every HL7 message in or out (table <c>HL7Messages</c>). The
/// raw text is preserved verbatim alongside parsed JSON, status, ack code, and error
/// detail, supporting the error-queue and replay workflows (docs/hl7-design.md 5).
/// </summary>
public class Hl7MessageLog : BaseEntity
{
    public long? EndpointId { get; set; }

    public Hl7Direction Direction { get; set; } = Hl7Direction.Inbound;

    public string MessageType { get; set; } = string.Empty;

    public string? TriggerEvent { get; set; }

    public string MessageControlId { get; set; } = string.Empty;

    public string RawMessage { get; set; } = string.Empty;

    public string? ParsedJson { get; set; }

    public Hl7MessageStatus Status { get; set; } = Hl7MessageStatus.Received;

    public DateTime ReceivedUtc { get; set; }

    public DateTime? ProcessedUtc { get; set; }

    /// <summary>HL7 acknowledgement code: AA (accept), AE (app error), AR (reject).</summary>
    public string? AckCode { get; set; }

    public string? ErrorDetail { get; set; }

    public int RetryCount { get; set; }
}
