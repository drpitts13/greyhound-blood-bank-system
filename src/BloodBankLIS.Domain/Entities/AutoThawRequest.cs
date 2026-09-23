using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Offer to thaw an allocated unit in place. Pending when the user declined at allocation;
/// completed when expiration was rewritten and thaw billing was captured.
/// </summary>
public class AutoThawRequest : BaseEntity
{
    public long AllocationId { get; set; }

    public Allocation? Allocation { get; set; }

    public long BloodUnitId { get; set; }

    public BloodUnit? BloodUnit { get; set; }

    public AutoThawRequestStatus Status { get; set; } = AutoThawRequestStatus.Pending;

    public DateTime? ThawUtc { get; set; }

    public DateTime? PreviousExpiresUtc { get; set; }

    public DateTime? NewExpiresUtc { get; set; }

    public long? OrderActionId { get; set; }

    public OrderAction? OrderAction { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string? CompletedBy { get; set; }
}
