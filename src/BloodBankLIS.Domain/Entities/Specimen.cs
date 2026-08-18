using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A patient specimen. The accession number is a preserved source identifier and
/// is unique. Expiration is computed at accessioning per policy and is enforced on
/// the issue path (see docs/safety-rules.md).
/// </summary>
public class Specimen : BaseEntity
{
    public string AccessionNumber { get; set; } = string.Empty;

    public long PatientId { get; set; }

    public Patient? Patient { get; set; }

    public long? EncounterId { get; set; }

    public Encounter? Encounter { get; set; }

    public string SpecimenType { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public DateTime CollectedUtc { get; set; }

    public DateTime? ReceivedUtc { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public string? DrawLocation { get; set; }

    public string? Collector { get; set; }

    /// <summary>First independent identifier attested at collection (AABB 5.11).</summary>
    public IdentityTokenType? Identifier1Type { get; set; }

    public string? Identifier1Value { get; set; }

    /// <summary>Second independent identifier attested at collection.</summary>
    public IdentityTokenType? Identifier2Type { get; set; }

    public string? Identifier2Value { get; set; }

    public SpecimenStatus Status { get; set; } = SpecimenStatus.Collected;

    public string? RejectionReason { get; set; }
}
