using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>The test line comment on the worklist (end-user testing pass 40).</summary>
public class TestLineCommentEditorTests : BunitContext
{
    private static TestWorkItemDto Line(string? comment) =>
        new(7, 1, 1, "Bench, Pat", "MRN1", "DAT", "DAT", "ORD1", OrderPriority.Stat, DateTime.UtcNow, "ACC1", 1,
            SpecimenStatus.Accepted, DateTime.UtcNow.AddDays(2), false, "O+", false, null, null, null, null, null,
            null, true, null, Comment: comment);

    // TEST-BB-1056: Save comment is offered only when the text differs from what is saved, so an empty, unchanged box
    // cannot be "saved" (end-user testing pass 39: it said "Comment saved.").
    [Fact]
    public void SaveComment_OnlyWhenTheTextChanged()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var empty = Render<TestLineCommentEditor>(p => p.Add(x => x.WorkItem, Line(null)));
        Assert.True(empty.Find("[data-save-comment]").HasAttribute("disabled"));

        var existing = Render<TestLineCommentEditor>(p => p.Add(x => x.WorkItem, Line("Hemolyzed, redraw requested")));
        Assert.True(existing.Find("[data-save-comment]").HasAttribute("disabled"));
    }
}
