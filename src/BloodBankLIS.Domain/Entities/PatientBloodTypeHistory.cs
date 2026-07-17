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

    public BloodTypeSource Source { get; set; } = BloodTypeSource.TestResult;

    public long? SourceResultId { get; set; }

    public bool IsCurrent { get; set; }

    /// <summary>Required for manual edits (a dangerous action; see docs/safety-rules.md).</summary>
    public string? Reason { get; set; }

    public AboRh BloodType => new(Abo, RhD);
}
