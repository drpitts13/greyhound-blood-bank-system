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

    // End-user testing pass 53: listener starts filled the session log; they are hidden unless asked for, a failed
    // start is kept, and "Warnings and errors only" leaves the problems.
    [Fact]
    public void SessionLog_HidesListenerStarts_KeepsProblems()
    {
        var now = DateTime.UtcNow;
        var logs = new List<InstrumentLogDto>();
        for (var i = 0; i < 60; i++)
        {
            logs.Add(new(100 + i, 1, now.AddMinutes(-i), "Info", "Bind", "Listening on port 2576."));
        }

        logs.Add(new(1, 1, now.AddHours(-5), "Info", "Accept", "Connection from 127.0.0.1:62963."));
        logs.Add(new(2, 1, now.AddHours(-5), "Info", "Receive", "Received 420 characters."));
        logs.Add(new(3, 1, now.AddHours(-6), "Error", "Bind", "Could not listen on port 2576: address in use"));
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/instruments/logs"] = logs });

        var page = Render<Instruments>();
        page.WaitForAssertion(() => Assert.Contains("Received 420 characters.", page.Markup));
        Assert.DoesNotContain("Listening on port 2576.", page.Markup);
        Assert.Contains("Could not listen on port 2576", page.Markup);
        Assert.Contains("Show listener starts (60)", page.Markup);

        page.FindAll("[data-instrument-log-filters] input[type=checkbox]")[0].Change(true);
        Assert.DoesNotContain("Received 420 characters.", page.Markup);
        Assert.Contains("Could not listen on port 2576", page.Markup);

        page.FindAll("[data-instrument-log-filters] input[type=checkbox]")[0].Change(false);
        page.FindAll("[data-instrument-log-filters] input[type=checkbox]")[1].Change(true);
        Assert.Contains("Listening on port 2576.", page.Markup);
        Assert.Contains("Showing 50 of 63", page.Find("[data-instrument-log-count]").TextContent);
    }
}
