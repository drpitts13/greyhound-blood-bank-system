using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A single test or product line on a patient order. Orders may contain multiple lines.
/// </summary>
public class OrderLine : BaseEntity
{
    public long OrderId { get; set; }

    public Order? Order { get; set; }

    public int LineNumber { get; set; }

    public OrderCategory LineCategory { get; set; } = OrderCategory.Test;

    public string LineName { get; set; } = string.Empty;

    public string? TestCode { get; set; }

    public OrderType OrderType { get; set; } = OrderType.Other;

    public long? ProductTypeId { get; set; }

    public ProductType? ProductType { get; set; }

    public FulfillmentStatus? FulfillmentStatus { get; set; }

    public ResultStatus? ResultStatus { get; set; }

    public bool IsActive { get; set; } = true;
}
