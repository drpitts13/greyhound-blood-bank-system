using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Persisted special transfusion requirement for a patient. Enforced by the issue
/// gate while clinically active (effective now, not end-dated); never an operator
/// checkbox (docs/erd.md §2, 21 CFR 606.151 / AABB special needs).
/// </summary>
public class SpecialTransfusionRequirement : BaseEntity
{
    public long PatientId { get; set; }

    public long? RequirementDefinitionId { get; set; }

    public SpecialTransfusionRequirementType RequirementType { get; set; }

    /// <summary>Antigen code when <see cref="RequirementType"/> is AntigenNegative (e.g. K, Fya).</summary>
    public string? AntigenCode { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime EffectiveUtc { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public string EnteredBy { get; set; } = "system";

    public string? DeactivationReason { get; set; }
}
