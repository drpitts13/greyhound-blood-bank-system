using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Documentation of a transfusion of an issued unit. A suspected reaction flags the
/// event for a reaction investigation (see docs/workflows.md section 9, added later).
/// </summary>
public class TransfusionEvent : BaseEntity
{
    public long IssueId { get; set; }

    public Issue? Issue { get; set; }

    public long BloodProductId { get; set; }

    public long PatientId { get; set; }

    public DateTime? StartUtc { get; set; }

    public DateTime? StopUtc { get; set; }

    public decimal? VolumeTransfused { get; set; }

    public string? Transfusionist { get; set; }

    /// <summary>Placeholder for structured vitals captured during transfusion.</summary>
    public string? VitalsJson { get; set; }

    public bool ReactionSuspected { get; set; }

    public TransfusionDisposition FinalDisposition { get; set; } = TransfusionDisposition.Completed;

    public string DocumentedBy { get; set; } = "system";
}
