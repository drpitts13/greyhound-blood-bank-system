using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Web.Components.Pages;
using BloodBankLIS.Web.Services;
using Bunit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>The Equipment page (Cycle 708).</summary>
public class EquipmentPageTests : BunitContext
{
    private static EquipmentCheckStatusDto Check(long id, string state, bool needsAction, string text, bool? lastPassed = null) =>
        new(id, "Daily check", 1, "Per SOP", true, state, new DateOnly(2026, 11, 11), needsAction, text,
            lastPassed is null ? null : new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc), lastPassed is null ? null : "tech", lastPassed,
            lastPassed == false ? "Read 33 °C" : null);

    private readonly ReportDownloadCache Cache = new(new MemoryCache(new MemoryCacheOptions()));

    private IRenderedComponent<EquipmentPage> Render(object? recordResponse = null)
    {
        var responses = new Dictionary<string, object>
        {
            ["/api/equipment"] = new List<EquipmentDto>
            {
                new(1, "CW-01", "Cell washer", "Cell washer", "Bench 1", null, true, null, null, null,
                    [Check(11, "DueToday", true, "Due today", lastPassed: true)]),
                new(2, "HB-01", "Heat block", "Heat block", null, null, true, new DateTime(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc), "Failed temperature check", "tech",
                    [Check(21, "Failed", true, "Failed: record a pass once it is fixed", lastPassed: false)])
            },
            ["/api/equipment/due"] = new List<EquipmentCheckDueDto>
            {
                new(1, "CW-01", "Cell washer", "Bench 1", 11, "Daily check", "DueToday", new DateOnly(2026, 11, 11), "Due today")
            }
        };
        if (recordResponse is not null)
        {
            responses["/api/equipment/checks/11/records"] = recordResponse;
        }

        responses["/api/equipment/2/records"] = new List<EquipmentCheckRecordDto>
        {
            new(31, 2, "HB-01", 21, "Temperature check", new DateTime(2026, 11, 11, 8, 0, 0, DateTimeKind.Utc), "tech", false, "=SUM(A1) read 33 °C"),
            new(30, 2, "HB-01", 20, "Retired weekly check", new DateTime(2026, 11, 4, 8, 0, 0, DateTimeKind.Utc), "amy", true, null)
        };
        CannedApi.Register(this, responses);
        Services.AddSingleton(Cache);
        var page = Render<EquipmentPage>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-due-check='11']")));
        return page;
    }

    // TEST-BB-973: the checks to do are listed (equipment out of service is not); Record opens the form with the SOP
    // instructions, Save needs a result, and saving reports it.
    [Fact]
    public void DueCheck_IsRecorded()
    {
        var page = Render(new EquipmentCheckRecordDto(5, 1, "CW-01", 11, "Daily check", DateTime.UtcNow, "tech", true, null));
        Assert.Single(page.FindAll("[data-due-check]"));

        page.Find("[data-due-check='11'] button").Click();
        Assert.Contains("Per SOP", page.Find("[data-record-form]").TextContent);
        Assert.True(page.Find("[data-record-save]").HasAttribute("disabled"));
        page.Find("[data-record-result]").Change("pass");
        page.Find("[data-record-save]").Click();

        page.WaitForAssertion(() => Assert.Contains("Check recorded as passed", page.Markup));
        Assert.Empty(page.FindAll("[data-record-form]"));
    }

    // TEST-BB-974: equipment out of service says since when, by whom and why; returning it asks why it can be used
    // again, and warns while a check's latest record failed.
    [Fact]
    public void OutOfService_ShowsWhy_AndWarnsOnReturn()
    {
        var page = Render();
        var heatBlock = page.Find("[data-equipment='HB-01']");
        Assert.NotNull(heatBlock.QuerySelector("[data-out-of-service]"));
        Assert.Contains("by tech: Failed temperature check", heatBlock.TextContent);

        page.Find("[data-equipment='HB-01'] [data-equipment-service]").Click();
        Assert.Contains("Why it can be used again", page.Find("[data-service-form]").TextContent);
        Assert.Contains("Record a passing check", page.Find("[data-service-failed]").TextContent);
        Assert.True(page.Find("[data-service-save]").HasAttribute("disabled"));
    }

    // TEST-BB-980: History shows every check performed on the equipment, newest first, including a check no longer
    // scheduled; the log downloads as CSV with UTC and local times, who, result and note (a note that looks like a
    // spreadsheet formula is guarded).
    [Fact]
    public void History_ShowsRecords_AndDownloadsTheLog()
    {
        var page = Render();
        page.Find("[data-equipment='HB-01'] [data-equipment-history]").Click();

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-record-row]").Count));
        var rows = page.FindAll("[data-record-row]");
        Assert.Contains("Temperature check", rows[0].TextContent);
        Assert.Contains("Failed", rows[0].TextContent);
        Assert.Contains("Retired weekly check", rows[1].TextContent);

        page.Find("[data-equipment-log]").Click();
        var token = JSInterop.VerifyInvoke("open").Arguments[0]!.ToString()!.Split('/').Last();
        var file = Cache.Take(token)!;
        Assert.Equal("equipment-log-HB-01.csv", file.FileName);
        var text = System.Text.Encoding.UTF8.GetString(file.Bytes);
        Assert.Contains("Equipment,Name,Check,Performed (UTC),Performed (local),By,Result,Reading or note", text);
        Assert.Contains("HB-01,Heat block,Temperature check,2026-11-11 08:00 UTC,", text);
        Assert.Contains("tech,Failed,", text);
        Assert.Contains(",'=SUM(A1) read 33 °C", text);

        page.Find("[data-equipment='HB-01'] [data-equipment-history]").Click();
        Assert.Empty(page.FindAll("[data-equipment-records]"));
    }

    // TEST-BB-982: Edit fills the form with the equipment's details (the code not editable) and saving needs a reason;
    // Change on a check fills its name and interval and also needs a reason.
    [Fact]
    public void Edit_IsPrefilled_AndNeedsAReason()
    {
        var page = Render();
        page.Find("[data-equipment='CW-01'] [data-equipment-edit]").Click();
        Assert.Equal("Bench 1", page.Find("[data-edit-location]").GetAttribute("value"));
        Assert.True(page.Find("[data-edit-save]").HasAttribute("disabled"));
        page.Find("[data-edit-reason]").Input("Moved");
        Assert.False(page.Find("[data-edit-save]").HasAttribute("disabled"));

        page.Find("[data-equipment='CW-01'] [data-check-edit]").Click();
        Assert.Equal("1", page.Find("[data-edit-check-interval]").GetAttribute("value"));
        Assert.True(page.Find("[data-edit-check-save]").HasAttribute("disabled"));
    }

    // TEST-BB-990: Stop on a check and Retire on equipment open a form saying what happens, whose button needs a reason;
    // retired equipment shows Retired and offers only Restore and History.
    [Fact]
    public void RetireAndStop_NeedAReason_AndRetiredOffersRestore()
    {
        var page = Render();
        page.Find("[data-equipment='CW-01'] [data-check-retire]").Click();
        Assert.Contains("Stop scheduling Daily check on CW-01", page.Find("[data-retire-form]").TextContent);
        Assert.True(page.Find("[data-retire-save]").HasAttribute("disabled"));
        page.Find("[data-retire-reason]").Input("Replaced");
        Assert.False(page.Find("[data-retire-save]").HasAttribute("disabled"));

        page.Find("[data-equipment='CW-01'] [data-equipment-retire]").Click();
        Assert.Contains("Retire CW-01 from the register", page.Find("[data-retire-form]").TextContent);
        Assert.True(page.Find("[data-retire-save]").HasAttribute("disabled"));
    }

    // TEST-BB-991: retired equipment shows Retired and offers only Restore and History (no edit, service, record or new check).
    [Fact]
    public void RetiredEquipment_OffersRestore()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/equipment"] = new List<EquipmentDto>
            {
                new(3, "OLD-01", "Old centrifuge", "Centrifuge", null, null, false, null, null, null, [Check(31, "NeverDone", true, "Never done")])
            },
            ["/api/equipment/due"] = new List<EquipmentCheckDueDto>()
        });
        Services.AddSingleton(Cache);
        var page = Render<EquipmentPage>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-equipment='OLD-01'] [data-equipment-retired]")));
        Assert.NotNull(page.Find("[data-equipment='OLD-01'] [data-equipment-restore]"));
        Assert.NotNull(page.Find("[data-equipment='OLD-01'] [data-equipment-history]"));
        Assert.Empty(page.FindAll("[data-equipment='OLD-01'] [data-equipment-edit]"));
        Assert.Empty(page.FindAll("[data-equipment='OLD-01'] [data-equipment-service]"));
        Assert.Empty(page.FindAll("[data-equipment='OLD-01'] [data-check-add]"));
    }

    // TEST-BB-993: Coming up lists the checks falling due in the window (30 days by default) with their due dates, and
    // says when nothing falls due.
    [Fact]
    public void ComingUp_SaysWhenNothingFallsDue()
    {
        var page = Render();
        Assert.Contains("Nothing else falls due within 30 days", page.Find("[data-equipment-upcoming]").TextContent);
    }

    [Fact]
    public void ComingUp_ListsChecksDueSoon()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/equipment"] = new List<EquipmentDto>(),
            ["/api/equipment/due"] = new List<EquipmentCheckDueDto>(),
            ["/api/equipment/upcoming"] = new List<EquipmentCheckDueDto>
            {
                new(4, "IRR-01", "Irradiator", "Room 3", 41, "Annual dose mapping", "Current", new DateOnly(2026, 12, 1), "Next due 2026-12-01")
            }
        });
        Services.AddSingleton(Cache);
        var page = Render<EquipmentPage>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-upcoming-check='41']")));
        Assert.Contains("2026-12-01", page.Find("[data-upcoming-check='41']").TextContent);
        Assert.Contains("Annual dose mapping", page.Find("[data-upcoming-check='41']").TextContent);
    }
}
