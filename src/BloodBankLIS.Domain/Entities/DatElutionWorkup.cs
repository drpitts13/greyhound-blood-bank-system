using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// First-class patient DAT and elution study. Records post-transfusion or
/// investigation DAT/elution outside a reaction packet. Structured eluate findings
/// (<see cref="DatElutionEluateFinding"/>) post to antibody history only when a second
/// person verifies the completed workup, and only alloantibodies post (OCD-042).
/// </summary>
public class DatElutionWorkup : BaseEntity
{
    public long PatientId { get; set; }

    public long? SpecimenId { get; set; }

    public DatElutionWorkupStatus Status { get; set; } = DatElutionWorkupStatus.Open;

    public DatWorkupResult DatResult { get; set; } = DatWorkupResult.NotRecorded;

    public string? DatMethod { get; set; }

    /// <summary>Reactions with each antiglobulin reagent; NotTested when not done (end-user testing pass 9).</summary>
    public ReactionGrade PolyspecificGrade { get; set; } = ReactionGrade.NotTested;

    public ReactionGrade IgGGrade { get; set; } = ReactionGrade.NotTested;

    public ReactionGrade C3Grade { get; set; } = ReactionGrade.NotTested;

    /// <summary>The negative control (saline or the manufacturer's control reagent), when tested.</summary>
    public ReactionGrade ControlGrade { get; set; } = ReactionGrade.NotTested;

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

    /// <summary>Second person who verified the completed workup; verification posts eluate alloantibodies.</summary>
    public string? VerifiedBy { get; set; }

    public DateTime? VerifiedUtc { get; set; }
}
