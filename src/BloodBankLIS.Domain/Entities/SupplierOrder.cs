using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A blood order to a supplier, usually generated from the minimum stock shortfall. It is printed
/// or exported and sent by the facility's usual route; there is no electronic interface
/// to the supplier. A received delivery is linked back to the order.
/// </summary>
public class SupplierOrder : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;

    public long BloodSupplierId { get; set; }

    public BloodSupplier? BloodSupplier { get; set; }

    public SupplierOrderStatus Status { get; set; } = SupplierOrderStatus.Draft;

    public string? Notes { get; set; }

    public string? SubmittedBy { get; set; }

    public DateTime? SubmittedUtc { get; set; }

    public long? SubmitSignatureId { get; set; }

    public long? BloodDeliveryId { get; set; }

    public DateTime? ReceivedUtc { get; set; }

    public string? CancelledBy { get; set; }

    public string? CancelReason { get; set; }

    /// <summary>
    /// The last follow-up with the supplier on a submitted order (who was spoken to, what was said), and the arrival they
    /// promised; the order is overdue against the promised time when there is one (end-user testing pass 45).
    /// </summary>
    public DateTime? PromisedArrivalUtc { get; set; }

    public string? FollowUpNote { get; set; }

    public string? FollowUpBy { get; set; }

    public DateTime? FollowUpUtc { get; set; }

    public ICollection<SupplierOrderLine> Lines { get; set; } = new List<SupplierOrderLine>();
}

public class SupplierOrderLine : BaseEntity
{
    public long SupplierOrderId { get; set; }

    public SupplierOrder? SupplierOrder { get; set; }

    public long ProductTypeId { get; set; }

    public AboGroup? Abo { get; set; }

    /// <summary>The Rh type wanted; null for either.</summary>
    public RhType? RhD { get; set; }

    /// <summary>What the units must be, as the supplier is to read it: irradiated, CMV-negative, K-negative...</summary>
    public string? Requirements { get; set; }

    /// <summary>The patient the units are ordered for, when the order is patient-specific.</summary>
    public long? PatientId { get; set; }

    public int Quantity { get; set; }
}
