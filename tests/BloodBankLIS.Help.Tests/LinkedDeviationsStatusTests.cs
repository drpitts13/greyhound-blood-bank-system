using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// End-user testing pass 273: a search of the pages for values printed as code names found the deviations listed on a
/// unit or patient ("UnderReview", "CorrectiveAction"), Reagent QC's result status, the chart's order category and source,
/// a supplier order's status and an instrument message's status printed as stored. They read in words.
/// </summary>
public class LinkedDeviationsStatusTests : BunitContext
{
    // TEST-BB-1299: a deviation linked to a record gives its status in words.
    [Fact]
    public void LinkedDeviation_StatusReadsInWords()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/deviations"] = new List<DeviationDto>
            {
                new(1, "Unit found in the wrong refrigerator", "d", DeviationSeverity.Major, DeviationStatus.UnderReview, "BloodUnit", 1, null, "tech2", new DateTime(2026, 11, 5, 8, 50, 0, DateTimeKind.Utc))
            }
        });

        var panel = Render<LinkedDeviations>(p => p.Add(x => x.ContextType, "BloodUnit").Add(x => x.ContextId, 1L));
        panel.WaitForAssertion(() => Assert.NotNull(panel.Find("[data-linked-deviation-status]")));

        Assert.StartsWith("Under review · reported", panel.Find("[data-linked-deviation-status]").TextContent.Trim());
    }
}
