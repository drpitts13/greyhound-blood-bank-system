using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Ward receipt, Return, and Transfusion start from a scanned unit, but opening one left the cursor elsewhere, so the
/// first scan went nowhere (Cycle 622). Opened from the desk button, the Unit number field takes the focus; opened from a
/// row, the unit is already filled and the focus is left alone.
/// </summary>
public class ScanDeskFocusTests : BunitContext
{
    // The page's own reference to the desk's Unit number field (the rendered attribute is not kept on every render).
    private static string? InputId(IRenderedComponent<Issuing> page, string field) =>
        ((ElementReference)typeof(Issuing).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(page.Instance)!).Id;

    private int FocusesOn(IRenderedComponent<Issuing> page, string desk) => FocusesOnId(InputId(page, desk switch
    {
        "receipt" => "_wardUnitInput",
        "return" => "_returnUnitInput",
        _ => "_txUnitInput"
    }));

    private int FocusesOnId(string? id) =>
        JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)
            && i.Arguments.OfType<ElementReference>().Any(r => r.Id == id));

    // TEST-BB-744: each scan desk opened from its button puts the cursor in Unit number.
    [Theory]
    [InlineData("Ward receipt", "receipt")]
    [InlineData("Return", "return")]
    [InlineData("Transfusion", "transfuse")]
    public void DeskButton_FocusesUnitNumber(string button, string desk)
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.Trim() == button).Click();

        page.WaitForAssertion(() => Assert.Equal(1, FocusesOn(page, desk)));
    }

    // TEST-BB-745: Return opened from an Issued units row fills the unit and does not take the focus to Unit number.
    [Fact]
    public void ReturnFromARow_LeavesTheFocusAlone()
    {
        var issued = DateTime.UtcNow.AddHours(-2);
        var outstanding = new OutstandingIssueWorkItemDto(
            41, 5, "MRN0022", "Demo-Multi, Riley", "1980-01-01", 9, "W0001230009205", "O+", false, null,
            null, false, issued, null, "nurse1", null, "4W", IssueStatus.Issued, ProductName: "Red Blood Cells");
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/outstanding"] = new List<OutstandingIssueWorkItemDto> { outstanding }
        });

        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.StartsWith("Issued units")).Click();
        AngleSharp.Dom.IElement RowReturn() => page.FindAll("table.bb tbody tr")
            .First(r => r.TextContent.Contains("W0001230009205"))
            .QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Return");
        page.WaitForAssertion(() => Assert.NotNull(RowReturn()));
        RowReturn().Click();

        page.WaitForAssertion(() => Assert.Equal("W0001230009205", page.Find("[data-desk-unit='return']").GetAttribute("value")));
        Assert.Equal(0, FocusesOn(page, "return"));
    }

    // TEST-BB-746: after a return is recorded, Unit number is empty and takes the focus for the next unit in the cooler,
    // and Return unit stays disabled until another unit is chosen (it had kept the returned unit, ready to send again).
    [Fact]
    public void AfterAReturn_UnitNumberIsClearAndFocused()
    {
        var issued = DateTime.UtcNow.AddHours(-2);
        var outstanding = new OutstandingIssueWorkItemDto(
            41, 5, "MRN0022", "Demo-Multi, Riley", "1980-01-01", 9, "W0001230009205", "O+", false, null,
            null, false, issued, null, "nurse1", null, "4W", IssueStatus.Issued, ProductName: "Red Blood Cells");
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/outstanding"] = new List<OutstandingIssueWorkItemDto> { outstanding },
            ["/api/issues/41/return"] = new ReturnDto(7, 41, 9, DateTime.UtcNow, "tech", "Not needed", false)
        });

        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.StartsWith("Issued units")).Click();
        AngleSharp.Dom.IElement RowReturn() => page.FindAll("table.bb tbody tr")
            .First(r => r.TextContent.Contains("W0001230009205"))
            .QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Return");
        page.WaitForAssertion(() => Assert.NotNull(RowReturn()));
        RowReturn().Click();

        page.FindAll("select").First(s => s.QuerySelector($"option[value='{UnitStatus.Quarantine}']") is not null
            && s.Closest(".bb-card")!.QuerySelector("[data-desk-unit='return']") is not null).Change(UnitStatus.Quarantine.ToString());
        page.Find("[data-desk-unit='return']").Closest(".form-row")!.QuerySelectorAll("input")[1].Input("Not needed");
        Assert.False(page.Find("[data-return-submit]").HasAttribute("disabled"));
        var returnInputId = InputId(page, "_returnUnitInput");
        var before = FocusesOnId(returnInputId);
        page.Find("[data-return-submit]").Click();

        page.WaitForAssertion(() => Assert.Contains("Unit returned.", page.Markup));
        Assert.Equal("", page.Find("[data-desk-unit='return']").GetAttribute("value") ?? "");
        Assert.True(page.Find("[data-return-submit]").HasAttribute("disabled"));
        page.WaitForAssertion(() => Assert.True(FocusesOnId(returnInputId) > before));
    }

    // TEST-BB-747: after a ward receipt is recorded, Unit number is empty and takes the focus for the next unit in the
    // cooler, and Acknowledge receipt stays disabled until another unit is chosen.
    [Fact]
    public void AfterAWardReceipt_UnitNumberIsClearAndFocused()
    {
        var issued = DateTime.UtcNow.AddMinutes(-20);
        var inTransit = new InTransitWorkItemDto(
            41, 5, "MRN0022", "Demo-Multi, Riley", 9, "W0001230009205", "O+", false, null, null, false,
            issued, issued.AddMinutes(30), false, "Cooler 1", "4W", IssueStatus.Issued, ProductName: "Red Blood Cells");
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/in-transit"] = new List<InTransitWorkItemDto> { inTransit },
            ["/api/issues/41/ward-receipt"] = new IssueDto(41, 3, 9, 5, "nurse1", "4W", issued, "tech", null, IssueType.Standard, null,
                IssueStatus.Issued, false, WardReceivedUtc: DateTime.UtcNow, WardReceivedBy: "nurse2")
        });

        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.StartsWith("In transit")).Click();
        AngleSharp.Dom.IElement RowReceive() => page.FindAll("table.bb tbody tr")
            .First(r => r.TextContent.Contains("W0001230009205"))
            .QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Receive");
        page.WaitForAssertion(() => Assert.NotNull(RowReceive()));
        RowReceive().Click();

        Assert.Equal("W0001230009205", page.Find("[data-desk-unit='receipt']").GetAttribute("value"));
        var wardInputId = InputId(page, "_wardUnitInput");
        var before = FocusesOnId(wardInputId);
        page.Find("[data-ward-submit]").Click();

        page.WaitForAssertion(() => Assert.Contains("Ward receipt recorded.", page.Markup));
        Assert.Equal("", page.Find("[data-desk-unit='receipt']").GetAttribute("value") ?? "");
        Assert.True(page.Find("[data-ward-submit]").HasAttribute("disabled"));
        page.WaitForAssertion(() => Assert.True(FocusesOnId(wardInputId) > before));
    }

    // TEST-BB-748: the reaction answer binds (it could not, Cycle 624); after a transfusion is completed, the finished unit leaves the form (Unit number empty and focused,
    // Start and Document disabled); scanning the patient's next unit opens it with the bedside identifiers kept.
    [Fact]
    public void AfterCompletingATransfusion_TheNextScanKeepsTheBedside()
    {
        var issued = DateTime.UtcNow.AddHours(-3);
        OutstandingIssueWorkItemDto Unit(long issueId, string unit, DateTime? started) => new(
            issueId, 5, "MRN0022", "Demo-Multi, Riley", "1980-01-01", issueId + 100, unit, "O+", false, null,
            null, false, issued, issued.AddMinutes(20), "nurse1", null, "4W", IssueStatus.Issued, ProductName: "Red Blood Cells")
        { TransfusionStartedUtc = started };
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/outstanding"] = new List<OutstandingIssueWorkItemDto>
            {
                Unit(41, "W0001230009205", issued.AddMinutes(30)),
                Unit(42, "W0001230009206", null)
            },
            ["/api/issues/41/transfusion/complete"] = new TransfusionEventDto(
                7, 41, 141, 5, issued.AddMinutes(30), DateTime.UtcNow, 300m, "nurse1", false, TransfusionDisposition.Completed, "tech"),
            ["/api/issues/latest-for-unit/W0001230009206"] = new IssueDto(
                42, 3, 142, 5, "nurse1", "4W", issued, "tech", null, IssueType.Standard, null, IssueStatus.Issued, false)
        });

        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.StartsWith("Issued units")).Click();
        AngleSharp.Dom.IElement RowButton(string unit, string text) => page.FindAll("table.bb tbody tr")
            .First(r => r.TextContent.Contains(unit)).QuerySelectorAll("button").First(b => b.TextContent.Trim() == text);
        page.WaitForAssertion(() => Assert.NotNull(RowButton("W0001230009205", "Complete")));
        RowButton("W0001230009205", "Complete").Click();

        page.Find("input[placeholder='Scan wristband or type']").Input("MRN0022");
        var noReaction = page.Find("[data-tx-reaction]").QuerySelectorAll("option").First(o => o.TextContent.Trim() == "No reaction").GetAttribute("value")!;
        page.Find("[data-tx-reaction]").Change(noReaction);
        var reaction = typeof(Issuing).GetField("_txReaction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(page.Instance);
        // The answer must bind: the options had no usable values, so every finish was refused (Cycle 624).
        Assert.Equal(false, reaction);
        page.Find("[data-tx-disposition]").Change(TransfusionDisposition.Completed.ToString());
        var txInputId = InputId(page, "_txUnitInput");
        var before = FocusesOnId(txInputId);
        page.Find("[data-tx-complete]").Click();

        page.WaitForAssertion(() => Assert.Contains("Transfusion completed.", page.Markup));
        Assert.Equal("", page.Find("[data-desk-unit='transfuse']").GetAttribute("value") ?? "");
        Assert.True(page.Find("[data-tx-start]").HasAttribute("disabled"));
        Assert.True(page.Find("[data-tx-document]").HasAttribute("disabled"));
        page.WaitForAssertion(() => Assert.True(FocusesOnId(txInputId) > before));

        page.Find("[data-desk-unit='transfuse']").Change("W0001230009206");
        page.WaitForAssertion(() => Assert.Equal("W0001230009206", page.Find("[data-desk-unit='transfuse']").GetAttribute("value")));
        Assert.Equal("MRN0022", page.Find("input[placeholder='Scan wristband or type']").GetAttribute("value"));
        Assert.False(page.Find("[data-tx-start]").HasAttribute("disabled"));
    }

    // TEST-BB-753: the Transfusion desk warns when the patient at this bedside has a reaction under investigation, naming
    // it and asking for the reason and who authorized continuing (OCD-078, decided 2026-11-10); another patient's unit
    // shows neither. Start stays available: the server asks for the override if the fields are left empty.
    [Fact]
    public void TransfusionDesk_WarnsOfAnOpenReactionInvestigation()
    {
        var issued = DateTime.UtcNow.AddHours(-3);
        var reported = DateTime.UtcNow.AddMinutes(-15);
        OutstandingIssueWorkItemDto Unit(long issueId, long patientId, string unit, long? reaction) => new(
            issueId, patientId, $"MRN{patientId}", "Demo, Pat", "1980-01-01", issueId + 100, unit, "O+", false, null,
            null, false, issued, issued.AddMinutes(20), "nurse1", null, "4W", IssueStatus.Issued, ProductName: "Red Blood Cells")
        { OpenReactionInvestigationId = reaction, OpenReactionReportedUtc = reaction is null ? null : reported };
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/outstanding"] = new List<OutstandingIssueWorkItemDto>
            {
                Unit(42, 5, "W0001230009206", 3),
                Unit(43, 6, "W0001230009207", null)
            }
        });

        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.StartsWith("Issued units")).Click();
        AngleSharp.Dom.IElement RowButton(string unit, string text) => page.FindAll("table.bb tbody tr")
            .First(r => r.TextContent.Contains(unit)).QuerySelectorAll("button").First(b => b.TextContent.Trim() == text);
        page.WaitForAssertion(() => Assert.NotNull(RowButton("W0001230009206", "Start / document")));
        Assert.Single(page.FindAll("[data-issued-reaction-open]"));

        RowButton("W0001230009206", "Start / document").Click();
        var warning = string.Join(" ", page.Find("[data-tx-reaction-open]").TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("#3", warning);
        Assert.Contains("only if the physician has decided to continue", warning);
        Assert.NotNull(page.Find("[data-tx-reaction-reason]"));
        Assert.NotNull(page.Find("[data-tx-reaction-authorizer]"));
        Assert.False(page.Find("[data-tx-start]").HasAttribute("disabled"));

        page.FindAll("button").First(b => b.TextContent.StartsWith("Issued units")).Click();
        page.WaitForAssertion(() => Assert.NotNull(RowButton("W0001230009207", "Start / document")));
        RowButton("W0001230009207", "Start / document").Click();
        Assert.Empty(page.FindAll("[data-tx-reaction-open]"));
    }
}
