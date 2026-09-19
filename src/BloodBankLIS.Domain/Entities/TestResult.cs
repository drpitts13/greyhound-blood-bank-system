using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A versioned test result for a specimen (and optional order). Corrections and
/// invalidations insert a new row and supersede the prior version.
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

    public TestResult? SupersededByResult { get; set; }

    public string? Value { get; set; }

    public string? Units { get; set; }

    public string? Interpretation { get; set; }

    public ResultStatus Status { get; set; } = ResultStatus.Entered;

    public ResultSource Source { get; set; } = ResultSource.Manual;

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
