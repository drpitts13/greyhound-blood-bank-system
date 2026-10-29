using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// First-class patient DAT and elution study. Records post-transfusion or
/// investigation DAT/elution outside a reaction packet. Does not identify
/// antibodies and does not post to antibody history.
/// </summary>
public class DatElutionWorkup : BaseEntity
{
    public long PatientId { get; set; }

    public long? SpecimenId { get; set; }

    public DatElutionWorkupStatus Status { get; set; } = DatElutionWorkupStatus.Open;

    public DatWorkupResult DatResult { get; set; } = DatWorkupResult.NotRecorded;

    public string? DatMethod { get; set; }

    /// <summary>Eluate findings or free-text result when elution was performed.</summary>
    public string? ElutionResult { get; set; }

    /// <summary>Required when DAT is positive and elution was not performed.</summary>
    public string? ElutionNotPerformedReason { get; set; }

    public string? Comment { get; set; }

    public string? CompletedBy { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string? VoidedBy { get; set; }

    public DateTime? VoidedUtc { get; set; }

    public string? VoidReason { get; set; }
}
