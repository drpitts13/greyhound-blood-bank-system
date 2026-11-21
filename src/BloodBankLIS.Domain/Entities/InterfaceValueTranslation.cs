using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One internal ↔ external coded-value pair for an interface data item. A row with no
/// <see cref="EndpointId"/> applies to every interface; a row for one interface overrides
/// the global row for the same value there. Applied on outbound messages always, and on
/// inbound messages only when an enabled inbound interface is resolved.
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

    /// <summary>The interface this row is limited to; null applies to all interfaces.</summary>
    public long? EndpointId { get; set; }
}
