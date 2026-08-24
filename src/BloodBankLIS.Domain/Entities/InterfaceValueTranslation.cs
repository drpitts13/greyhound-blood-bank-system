using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One internal ↔ external coded-value pair for an interface data item.
/// Global (not per endpoint). Applied on outbound messages always, and on inbound
/// messages only when an enabled inbound interface is resolved.
/// </summary>
public class InterfaceValueTranslation : BaseEntity
{
    /// <summary>Catalog key, e.g. <c>Order.TestCode</c>.</summary>
    public string DataItemKey { get; set; } = string.Empty;

    /// <summary>LIS / internal code.</summary>
    public string InternalValue { get; set; } = string.Empty;

    /// <summary>HIS / interface code.</summary>
    public string ExternalValue { get; set; } = string.Empty;

    public InterfaceTranslationDirection Direction { get; set; } = InterfaceTranslationDirection.Both;
}
