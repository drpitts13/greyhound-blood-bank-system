using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>The Equipment page (Cycle 708).</summary>
public class EquipmentPageTests : BunitContext
{
    private static EquipmentCheckStatusDto Check(long id, string state, bool needsAction, string text, bool? lastPassed = null) =>
        new(id, "Daily check", 1, "Per SOP", true, state, new DateOnly(2026, 11, 11), needsAction, text,
            lastPassed is null ? null : new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc), lastPassed is null ? null : "tech", lastPassed,
            lastPassed == false ? "Read 33 °C" : null);

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

        CannedApi.Register(this, responses);
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
}
