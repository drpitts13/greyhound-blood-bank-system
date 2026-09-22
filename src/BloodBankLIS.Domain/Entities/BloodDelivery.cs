using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One supplier shipment. The system id is the delivery id. Received time is stamped
/// when the delivery is entered and does not change when units are added later.
/// </summary>
public class BloodDelivery : BaseEntity
{
    public long BloodSupplierId { get; set; }

    public BloodSupplier? BloodSupplier { get; set; }

    public string? PackingListNumber { get; set; }

    public DateTime OrderedUtc { get; set; }

    public DateTime ReceivedUtc { get; set; }

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Open;

    public DateTime? CompletedUtc { get; set; }

    public ICollection<BloodDeliveryLine> Lines { get; set; } = new List<BloodDeliveryLine>();
}
