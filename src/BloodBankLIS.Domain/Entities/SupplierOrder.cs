using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A blood order to a supplier, usually generated from the par shortfall. It is printed
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

    public ICollection<SupplierOrderLine> Lines { get; set; } = new List<SupplierOrderLine>();
}

public class SupplierOrderLine : BaseEntity
{
    public long SupplierOrderId { get; set; }

    public SupplierOrder? SupplierOrder { get; set; }

    public long ProductTypeId { get; set; }

    public AboGroup? Abo { get; set; }

    public int Quantity { get; set; }
}
