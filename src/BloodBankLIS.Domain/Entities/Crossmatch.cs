using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A crossmatch (serologic or electronic) between a unit and a patient using a
/// specific specimen. A compatible, unexpired crossmatch is a precondition for
/// issuing crossmatch-required products (see docs/safety-rules.md section 1).
/// </summary>
public class Crossmatch : BaseEntity
{
    public long BloodProductId { get; set; }

    public BloodUnit? Unit { get; set; }

    public long PatientId { get; set; }

    public long SpecimenId { get; set; }

    public CrossmatchMethod Method { get; set; } = CrossmatchMethod.Serologic;

    public CrossmatchResult Result { get; set; } = CrossmatchResult.NotPerformed;

    public DateTime PerformedUtc { get; set; }

    public string PerformedBy { get; set; } = "system";

    /// <summary>When the crossmatch is no longer valid (typically tied to specimen expiry).</summary>
    public DateTime? ExpiresUtc { get; set; }

    public string? Comment { get; set; }

    public string? Phase { get; set; }

    public string? Interpretation { get; set; }

    public string? ObservedResultsJson { get; set; }

    public CrossmatchClinicalStatus ClinicalStatus { get; set; } = CrossmatchClinicalStatus.NotPerformed;

    public string? RulesVersion { get; set; }

    public string? PolicyVersion { get; set; }

    public long? EncounterId { get; set; }

    public long? OrderId { get; set; }
}
