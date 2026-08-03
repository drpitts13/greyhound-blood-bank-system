using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Preserves original scanned strings for audit. Raw barcode strings are never primary keys.
/// Stores original, sanitized, and normalized values separately.
/// </summary>
public class BloodComponentRawScan : BaseEntity
{
    public long BloodProductId { get; set; }

    public BloodUnit? Unit { get; set; }

    public IsbtDataStructureKind StructureKind { get; set; }

    public string OriginalValue { get; set; } = string.Empty;

    public string SanitizedValue { get; set; } = string.Empty;

    public string? NormalizedValue { get; set; }

    public ComponentEntrySource Source { get; set; }

    public string EnteredBy { get; set; } = "system";

    public DateTime EnteredAt { get; set; }
}
