using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using BloodBankLIS.Web.Services;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// HL7 error queue (Cycle 616): rows gave no time, so a message stuck for a minute looked the same as one stuck for a
/// day. Each row now says how long its error has waited.
/// </summary>
public class Hl7ErrorQueueTests : BunitContext
{
    // TEST-BB-735: each unresolved error says how long it has waited; a row from before the time was carried says "—".
    [Fact]
    public void ErrorRows_SayHowLongTheyHaveWaited()
    {
        var now = DateTime.UtcNow;
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/hl7/errors"] = new List<Hl7ErrorVm>
            {
                new(11, 101, "Parse", "PID-3 missing", 0, null, false, "CTRL-11", "ADT", "A08",
                    Direction: Hl7Direction.Inbound, QueuedUtc: now.AddHours(-5).AddMinutes(-1)),
                new(12, 102, "Ack", "AE from receiver", 2, null, false, "CTRL-12", "ORU", "R01",
                    Direction: Hl7Direction.Outbound)
            }
        });

        var page = Render<Hl7>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-error-waiting='11']")));

        Assert.Equal("5 h", page.Find("[data-error-waiting='11']").TextContent.Trim());
        Assert.Equal("—", page.Find("[data-error-waiting='12']").TextContent.Trim());
    }
}
