using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One antibody-identification case. Assistance may be stored on child findings;
/// completion requires technologist interpretation and, by default, supervisor review.
/// </summary>
public class AntibodyIdentificationWorkup : BaseEntity
{
    public long PatientId { get; set; }

    public long? SpecimenId { get; set; }

    public long? SourceResultId { get; set; }

    public long PrimaryLotId { get; set; }

    public AntibodyWorkupStatus Status { get; set; } = AntibodyWorkupStatus.InProgress;

    public AntibodyIdDatResult DatResult { get; set; } = AntibodyIdDatResult.NotPerformed;

    public string? DatMethod { get; set; }

    public string? Comment { get; set; }

    public string? TechnologistInterpretation { get; set; }

    public string? TechnologistUser { get; set; }

    public DateTime? InterpretedUtc { get; set; }

    public string? SupervisorUser { get; set; }

    public DateTime? ReviewedUtc { get; set; }

    public string? SupervisorComment { get; set; }

    public bool SupervisorAccepted { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string? CompletedBy { get; set; }

    public string? VoidReason { get; set; }
}
