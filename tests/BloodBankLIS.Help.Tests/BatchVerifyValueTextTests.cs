using BloodBankLIS.Application.Results;
using BloodBankLIS.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// End-user testing pass 257: after pass 256 found the reaction page showing a stored antibody screen, the batch verify
/// list on the Test Worklist and an equipment's Results list still showed a result's stored value, which for a screen or
/// an ABO/Rh panel is JSON. Both now read it as staff do.
/// </summary>
public class BatchVerifyValueTextTests : BunitContext
{
    // TEST-BB-1283: a batch verify candidate that is a stored screen panel reads as its readings.
    [Fact]
    public void BatchCandidate_ShowsTheScreenAsReadings()
    {
        const string stored = """{"version":2,"subtests":{"Cell1":{"IS":"0","AHG":"0"},"Cell2":{"IS":"0","AHG":"0"}}}""";
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/test-worklist/pending"] = new List<TestWorkItemDto>(),
            ["/api/results/batch-verify/candidates"] = new BatchVerifyCandidatesDto(true,
                [new BatchVerifyCandidateDto(7, 1, "Demo, Pat", "MRN5", "ABSC", stored, "tech2", "ACC-1")])
        });

        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo("/test-worklist?show=verify");
        var page = Render<TestWorklist>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-batch-verify]")));

        var text = page.Find("[data-batch-verify]").TextContent;
        Assert.Contains("Cell1 AHG:0", text);
        Assert.DoesNotContain("\"subtests\"", text);
    }
}
