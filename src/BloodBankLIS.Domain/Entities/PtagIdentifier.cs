using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A code printed on one compatibility tag. Unconsumed until the matching scan completes an issue.
/// </summary>
public class PtagIdentifier : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public long BloodProductId { get; set; }

    public long PatientId { get; set; }

    public long? PrintJobId { get; set; }

    public long? IssueId { get; set; }

    public Issue? Issue { get; set; }

    public DateTime? ConsumedUtc { get; set; }
}
