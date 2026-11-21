using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.Domain.Tests;

public class OutboundMessageRouteTests
{
    // TEST-BB-1150: an endpoint that lists message types carries those; otherwise a charge goes only to billing, a
    // result only to results, a dispense only to the blood product interface (end-user testing pass 129).
    [Theory]
    [InlineData(InterfaceType.Billing, null, "DFT^P03", true)]
    [InlineData(InterfaceType.Results, null, "DFT", false)]
    [InlineData(InterfaceType.Results, null, "ORU^R01", true)]
    [InlineData(InterfaceType.Billing, null, "ORU", false)]
    [InlineData(InterfaceType.Bpam, "", "BPS^O29", true)]
    [InlineData(InterfaceType.Results, null, "BTS", false)]
    [InlineData(InterfaceType.Results, "DFT,ORU", "DFT^P03", true)]
    [InlineData(InterfaceType.Billing, "DFT", "ORU", false)]
    [InlineData(InterfaceType.Adt, null, "ZZZ", true)]
    public void Carries(InterfaceType endpointType, string? listed, string messageType, bool expected) =>
        Assert.Equal(expected, OutboundMessageRoute.Carries(endpointType, listed, messageType));
}
