using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>Product History marks a unit issued while the patient's reaction was under investigation (Cycle 634).</summary>
public class ProductHistoryReactionTests : BunitContext
{
    private static PatientProductHistoryRowDto Issued(long id, string unit, long? reaction) => new(
        id, "Issue", PatientProductHistoryEventType.Issued, DateTime.UtcNow.AddHours(-id), unit, "Red Blood Cells", "A+", "A+",
        "VIS-1", "ORD-1", "ACC-1", "Compatible", "tech", "3N", null, null, null, null, null, false, false, false,
        UnitId: id, IssueId: id, ReactionInvestigationOpenAtIssueId: reaction);

    // TEST-BB-769: the row of a unit issued while reaction #1 was under investigation carries the badge on the main row
    // (not only under "all columns"); a unit issued with none does not.
    [Fact]
    public void IssuedDuringAReaction_IsMarkedOnTheRow()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var tab = Render<PatientChartProductHistoryTab>(p => p
            .Add(c => c.PatientId, 6)
            .Add(c => c.Rows, new List<PatientProductHistoryRowDto> { Issued(1, "W000123RXN0001", 1), Issued(2, "W0001230000099", null) }));

        var badge = Assert.Single(tab.FindAll("[data-history-reaction-at-issue]"));
        Assert.Equal("Issued during reaction #1", badge.TextContent.Trim());
        Assert.Contains("W000123RXN0001", badge.Closest("tr")!.TextContent);
    }
}
