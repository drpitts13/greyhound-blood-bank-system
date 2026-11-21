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

    /// <summary>
    /// OCD-074: set when an infant's specimen was made valid for the admission; the expiration the usual rules gave it,
    /// which discharge never goes below. Null for every other specimen.
    /// </summary>
    public DateTime? NeonatalStandardExpiresUtc { get; set; }

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

    /// <summary>Catalog code chosen at rejection (SpecimenRejectionReason), for reporting by cause.</summary>
    public string? RejectionReasonCode { get; set; }

    /// <summary>When the specimen was rejected. Null on specimens rejected before this was recorded.</summary>
    public DateTime? RejectedUtc { get; set; }

    /// <summary>User who rejected the specimen.</summary>
    public string? RejectedBy { get; set; }

    /// <summary>Free-text specimen comment. Coded comments from the Specimen catalog may be inserted.</summary>
    public string? Comment { get; set; }

    /// <summary>Where the tube is stored now (rack, shelf, position); earlier places are kept as SpecimenStorageEvent rows.</summary>
    public string? StorageLocation { get; set; }

    public DateTime? StoredUtc { get; set; }

    public string? StoredBy { get; set; }

    /// <summary>When the tube was thrown away (Store tubes, Discard); the place it was in is kept on its storage history.</summary>
    public DateTime? DiscardedUtc { get; set; }

    public string? DiscardedBy { get; set; }

    /// <summary>Why the tube was discarded before its retention date, or null when it was due.</summary>
    public string? DiscardReason { get; set; }
}
