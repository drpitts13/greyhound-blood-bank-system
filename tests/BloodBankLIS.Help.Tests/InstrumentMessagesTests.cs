using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Instruments › Messages (Cycle 617): When showed the time of day only, so a failure from two days ago read as today's,
/// and nothing said how long an open failure had waited.
/// </summary>
public class InstrumentMessagesTests : BunitContext
{
    // TEST-BB-737: a message from an earlier day shows its date; an open failure says how long it has waited; a message
    // processed today shows the time alone and no wait.
    [Fact]
    public void Messages_ShowTheDate_AndHowLongAnOpenFailureHasWaited()
    {
        var old = DateTime.UtcNow.AddDays(-2).AddMinutes(-3);
        var today = DateTime.UtcNow;
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/instruments/messages"] = new List<InstrumentMessageSummaryDto>
            {
                new(21, 1, Hl7Direction.Inbound, InstrumentMessageKind.Result, InstrumentMessageStatus.Failed, old, old, "ACC21", null,
                    "Instrument code X has no translation to a test (INST-CODE)"),
                new(22, 1, Hl7Direction.Inbound, InstrumentMessageKind.Result, InstrumentMessageStatus.Processed, today, today, "ACC22", null, null)
            }
        });

        var page = Render<Instruments>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-inst-when='21']")));

        var failed = page.Find("[data-inst-when='21']").TextContent;
        Assert.Contains(old.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), failed);
        Assert.Contains("waiting 2 d", failed);

        var processed = page.Find("[data-inst-when='22']");
        Assert.Null(processed.QuerySelector("[data-inst-waiting]"));
        Assert.Equal(today.ToLocalTime().ToString("HH:mm:ss"), processed.TextContent.Trim());
    }
}
