using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>The open RhIG list shows what the facts suggest beside what was recorded (Cycle 681, OCD-069).</summary>
public class RhigQueueSuggestionTests : BunitContext
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static RhigQueueItemDto Item(long id, RhigDecision decision, int? vials, RhigDecision? suggested, int? calculated) => new(
        new RhigEvaluationDto(id, id, RhigContext.Postpartum, Now.AddHours(-id), RhType.Positive, FetalBleedScreen.Positive, 0.9m,
            decision, vials, null, "tech1", Now.AddHours(-id), null, null, [],
            SuggestedDecision: suggested, VialsCalculated: calculated),
        $"Mother {id}", $"MRN{id}", false);

    // TEST-BB-892: each row says the suggested decision and calculated vials; a recorded dose that differs is marked; a
    // row whose facts suggest nothing shows a dash; a candidate with no dose calculated says so.
    [Fact]
    public void Queue_ShowsTheSuggestion()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/rhig/open"] = new List<RhigQueueItemDto>
            {
                Item(1, RhigDecision.Candidate, 2, RhigDecision.Candidate, 3),
                Item(2, RhigDecision.Undetermined, null, null, null),
                Item(3, RhigDecision.Undetermined, null, RhigDecision.Candidate, null)
            }
        });
        var page = Render<RhigQueue>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-rhig-queue-suggested='1']")));

        var first = page.Find("[data-rhig-queue-suggested='1']");
        Assert.Contains("Candidate, 3 vials", first.TextContent);
        Assert.NotNull(first.QuerySelector("[data-rhig-queue-differs]"));
        Assert.Equal("—", page.Find("[data-rhig-queue-suggested='2']").TextContent.Trim());
        Assert.Contains("Candidate (no dose calculated)", page.Find("[data-rhig-queue-suggested='3']").TextContent);
        Assert.Null(page.Find("[data-rhig-queue-suggested='3']").QuerySelector("[data-rhig-queue-differs]"));
    }
}
