using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Persisted ASTM message with parse, mapping, and rule traces.</summary>
public class InstrumentMessageLog : BaseEntity
{
    public long InterfaceId { get; set; }

    public Hl7Direction Direction { get; set; } = Hl7Direction.Inbound;

    public InstrumentMessageKind MessageKind { get; set; } = InstrumentMessageKind.Unknown;

    public string RawMessage { get; set; } = string.Empty;

    public string? ParsedJson { get; set; }

    public string? MappedJson { get; set; }

    public string? RuleTraceJson { get; set; }

    public InstrumentMessageStatus Status { get; set; } = InstrumentMessageStatus.Received;

    public DateTime ReceivedUtc { get; set; }

    public DateTime? ProcessedUtc { get; set; }

    public string? ErrorDetail { get; set; }

    public string? SpecimenId { get; set; }

    public string? PatientId { get; set; }
}
