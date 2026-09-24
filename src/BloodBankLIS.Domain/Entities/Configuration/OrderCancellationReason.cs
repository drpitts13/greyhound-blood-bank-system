using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Admin catalog of why an order may be cancelled. Chart cancellation stores the code
/// and copies <see cref="Text"/> onto the order.
/// </summary>
public class OrderCancellationReason : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
