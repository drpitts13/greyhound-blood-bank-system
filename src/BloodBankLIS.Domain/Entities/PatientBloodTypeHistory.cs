using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Append-only ABO/Rh determination history. Corrections add a new row and flip
/// <see cref="IsCurrent"/>; prior rows are preserved (see docs/erd.md section 3).
/// The created metadata (CreatedUtc/CreatedBy) records when and by whom the value
/// was recorded.
/// </summary>
public class PatientBloodTypeHistory : BaseEntity
{
    public long PatientId { get; set; }

    public AboGroup Abo { get; set; } = AboGroup.Unknown;

    public RhType RhD { get; set; } = RhType.Unknown;

    /// <summary>A1/A2 subgroup when ABO is A or AB; otherwise NotApplicable.</summary>
    public AboSubgroup AboSubgroup { get; set; } = AboSubgroup.Unknown;

    public BloodTypeSource Source { get; set; } = BloodTypeSource.TestResult;

    public long? SourceResultId { get; set; }

    /// <summary>
    /// The specimen the source result was tested on. Two determinations from the same specimen are not two
    /// determinations: a mislabeled tube would confirm itself. Null for manual or historical types.
    /// </summary>
    public long? SpecimenId { get; set; }

    public bool IsCurrent { get; set; }

    /// <summary>Required for manual edits (a dangerous action; see docs/safety-rules.md).</summary>
    public string? Reason { get; set; }

    /// <summary>For a manual entry: whether it is documented from a previous blood bank system or an outside source (OCD-048).</summary>
    public ManualTypeDocumentation? ManualDocumentation { get; set; }

    /// <summary>The record or report a documented manual entry came from.</summary>
    public string? ManualDocumentationReference { get; set; }

    /// <summary>
    /// Set when the result this type came from was invalidated. Such a row is never current again and is not
    /// evidence for the two-determination rule or for a discrepancy.
    /// </summary>
    public DateTime? InvalidatedUtc { get; set; }

    public AboRh BloodType => new(Abo, RhD);
}
