using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A versioned test result. Verified results are immutable: corrections create a
/// new version and mark the prior row superseded; originals are preserved
/// (see docs/safety-rules.md sections 6-7). Full Tests catalog arrives in a later phase.
/// </summary>
public class TestResult : BaseEntity
{
    public long SpecimenId { get; set; }

    public Specimen? Specimen { get; set; }

    public long PatientId { get; set; }

    public long? OrderId { get; set; }

    public string TestCode { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public long? SupersededByResultId { get; set; }

    /// <summary>The newer version that supersedes this result, when corrected.</summary>
    public TestResult? SupersededByResult { get; set; }

    public string? Value { get; set; }

    public string? Units { get; set; }

    public string? Interpretation { get; set; }

    public ResultStatus Status { get; set; } = ResultStatus.Pending;

    public ResultSource Source { get; set; } = ResultSource.Manual;

    /// <summary>Instrument identifier, HL7 control id, or calculation rule key.</summary>
    public string? SourceReference { get; set; }

    public string? EnteredBy { get; set; }

    public DateTime? EnteredUtc { get; set; }

    public string? VerifiedBy { get; set; }

    public DateTime? VerifiedUtc { get; set; }

    public string? CorrectionReason { get; set; }

    public string? InvalidatedBy { get; set; }

    public DateTime? InvalidatedUtc { get; set; }

    public string? InvalidationReason { get; set; }
}
