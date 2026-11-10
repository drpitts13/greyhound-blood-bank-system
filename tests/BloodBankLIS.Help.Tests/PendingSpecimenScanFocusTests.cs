using BloodBankLIS.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Pending specimens works by scanning, but opening it or changing Receive/Accept left the cursor elsewhere, so the next
/// tube was scanned into the page or a radio button (Cycle 637).
/// </summary>
public class PendingSpecimenScanFocusTests : BunitContext
{
    private string? ScanInputId(IRenderedComponent<TestWorklist> page) =>
        ((ElementReference)typeof(TestWorklist).GetField("_specimenScanInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(page.Instance)!).Id;

    private int Focuses(string? id) => JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)
        && i.Arguments.OfType<ElementReference>().Any(r => r.Id == id));

    // TEST-BB-773: opening Pending specimens puts the cursor in the scan field, and so does changing to Accept.
    [Fact]
    public void ScanField_TakesTheFocus_OnOpenAndModeChange()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var page = Render<TestWorklist>();
        page.FindAll("button").First(b => b.TextContent.Trim() == "Specimens").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-specimen-scan]")));
        var id = ScanInputId(page);
        page.WaitForAssertion(() => Assert.Equal(1, Focuses(id)));

        page.Find("[data-scan-mode='accept']").Change(true);
        page.WaitForAssertion(() => Assert.Equal(2, Focuses(id)));
        Assert.Contains("Scan tube to accept", page.Markup);
    }

    // TEST-BB-775: rejecting a pending tube offers the next step, Collect a new specimen, opening the patient's accession
    // form; before the reject is confirmed nothing is offered.
    [Fact]
    public void RejectingATube_OffersANewSpecimen()
    {
        var tube = new BloodBankLIS.Application.Specimens.PendingSpecimenWorkItemDto(
            18, 12, "Demo-Multi, Riley", "MRN0022", "ACC-DEMO-RCV1", "EDTA", DateTime.UtcNow.AddMinutes(-25), null,
            DateTime.UtcNow.AddDays(3), BloodBankLIS.Domain.Enums.SpecimenStatus.Collected, false);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/specimens/pending"] = new List<BloodBankLIS.Application.Specimens.PendingSpecimenWorkItemDto> { tube },
            ["/api/reference/specimen-rejection-reasons"] = new List<BloodBankLIS.Application.Admin.SpecimenRejectionReasonRefDto>
            {
                new("HEMOLYZED", "Hemolyzed", false, 1)
            },
            ["/api/specimens/18/reject"] = new { Id = 18L, AccessionNumber = "ACC-DEMO-RCV1", PatientId = 12L }
        });

        var page = Render<TestWorklist>();
        page.FindAll("button").First(b => b.TextContent.Trim() == "Specimens").Click();
        AngleSharp.Dom.IElement Row() => page.FindAll("table.bb tbody tr").First(r => r.TextContent.Contains("ACC-DEMO-RCV1"));
        page.WaitForAssertion(() => Assert.NotNull(Row()));
        Row().QuerySelectorAll("button").First(b => b.TextContent.Trim() == "Reject").Click();
        Assert.Empty(page.FindAll("[data-rejected-next]"));

        page.WaitForAssertion(() => Assert.NotNull(page.Find("select[aria-label='Rejection reason'] option[value='HEMOLYZED']")));
        page.Find("select[aria-label='Rejection reason']").Change("HEMOLYZED");
        page.FindAll("button").First(b => b.TextContent.Trim() == "Confirm reject").Click();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-rejected-next]")));
        var next = page.Find("[data-rejected-next]");
        Assert.Contains("ACC-DEMO-RCV1", next.TextContent);
        Assert.Contains("needs a new specimen", next.TextContent);
        Assert.Equal("/patients/12?tab=specimens&panel=specimen", next.QuerySelector("a")!.GetAttribute("href"));
    }
}
