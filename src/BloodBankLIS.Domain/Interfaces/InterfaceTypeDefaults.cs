using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Interfaces;

/// <summary>Derived message types and typical direction for each interface type.</summary>
public static class InterfaceTypeDefaults
{
    public static string MessageTypes(InterfaceType type) => type switch
    {
        InterfaceType.Adt => "ADT",
        InterfaceType.Orders => "ORM,OML",
        InterfaceType.Results => "ORU",
        InterfaceType.Billing => "DFT",
        InterfaceType.Bpam => "RAS,BPS",
        _ => "ADT"
    };

    public static Hl7Direction Direction(InterfaceType type) => type switch
    {
        InterfaceType.Results or InterfaceType.Billing => Hl7Direction.Outbound,
        _ => Hl7Direction.Inbound
    };

    public static bool SupportsMessageType(InterfaceType type, string messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType))
        {
            return false;
        }

        return MessageTypes(type)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(messageType, StringComparer.OrdinalIgnoreCase);
    }
}
