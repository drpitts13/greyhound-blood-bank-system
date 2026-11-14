using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Recipient traceback (end-user testing pass 67): units with no DIN recorded were not searched, yet the page said
/// "No other components share these DINs" and "No other recipients identified".
/// </summary>
public class LookbackTraceTests : BunitContext
{
    // TEST-BB-1078: units without a DIN are named, and the empty results say there was no DIN to search.
    [Fact]
    public void Trace_UnitsWithoutDin_AreNamed_NotReportedAsNoneFound()
    {
        var issued = new DateTime(2026, 11, 6, 13, 38, 0, DateTimeKind.Utc);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/lookback/recipient"] = new RecipientTraceReportDto(
                new RecipientTracePatientDto(12, "MRN0022", "Demo-Multi", "Riley", new DateOnly(1958, 11, 21), false),
                [
                    new RecipientTraceUnitDto(1, "W0001230009204", null, UnitStatus.Issued, 10, issued, "ICU 4", IssueType.Standard, IssueStatus.Issued, null, null, false),
                    new RecipientTraceUnitDto(2, "W0001230009203", " ", UnitStatus.Transfused, 9, issued, "ICU 4", IssueType.Standard, IssueStatus.Transfused, 5, TransfusionDisposition.Completed, false)
                ],
                [],
                [])
        });

        Services.GetRequiredService<NavigationManager>().NavigateTo("/lookback?mrn=MRN0022");
        var page = Render<Lookback>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-trace-without-din]")));

        Assert.Contains("2 units have no DIN recorded (W0001230009204, W0001230009203)", page.Find("[data-trace-without-din]").TextContent);
        Assert.Equal(2, page.FindAll("[data-trace-no-din]").Count);
        Assert.DoesNotContain("No other components share these DINs", page.Markup);
        Assert.DoesNotContain("No other recipients identified", page.Markup);
        Assert.Contains("No DIN to search.", page.Markup);
    }
}
