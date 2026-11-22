using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Interfaces;

/// <summary>
/// Which outbound interface may carry a queued message (end-user testing pass 129: a message queued with no endpoint was
/// sent to whichever outbound endpoint was enabled, so with only a results feed to the EHR, billing charges would have
/// gone to it). An endpoint that lists its message types carries those; otherwise its kind decides: a DFT goes to
/// billing, an ORU to results, a BPS/BTS to the blood product interface. Any other message type has no fixed route.
/// </summary>
public static class OutboundMessageRoute
{
    /// <summary>"DFT" from "DFT" or "DFT^P03".</summary>
    public static string Type(string? messageType) =>
        (messageType ?? string.Empty).Split('^', 2)[0].Trim().ToUpperInvariant();

    public static InterfaceType? InterfaceFor(string? messageType) => Type(messageType) switch
    {
        "DFT" => InterfaceType.Billing,
        "ORU" => InterfaceType.Results,
        "BPS" or "BTS" => InterfaceType.Bpam,
        _ => null
    };

    /// <summary>Whether an endpoint (its kind and its configured message types, if any) may carry a message of that type.</summary>
    public static bool Carries(InterfaceType endpointType, string? endpointMessageTypes, string? messageType)
    {
        var listed = (endpointMessageTypes ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(Type)
            .Where(t => t.Length > 0)
            .ToList();
        if (listed.Count > 0)
        {
            return listed.Contains(Type(messageType));
        }

        return InterfaceFor(messageType) is not InterfaceType route || route == endpointType;
    }
}
