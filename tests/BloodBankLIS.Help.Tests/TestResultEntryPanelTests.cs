using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Web.Components;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Result entry, rendered: every required reaction started at the first grade ("0"), so a form saved untouched recorded
/// negative reactions nobody read (Cycle 590). A required reading now starts blank and the save names what is missing.
/// </summary>
public class TestResultEntryPanelTests : BunitContext
{
    private static readonly IReadOnlyList<SubtestChoiceDto> Grades = SubtestChoiceDefinitions.DefaultGradedReaction()
        .Select(c => new SubtestChoiceDto(c.Code, c.Label, c.Polarity))
        .ToList();

    private static TestWorkItemDto WorkItem() => new(
        1, 1, 1, "Demo, Newborn", "MRN0002", "ABORH", "ABO/Rh type", "ORD-1", OrderPriority.Routine, DateTime.UtcNow,
        "ACC-1", 1, SpecimenStatus.Accepted, DateTime.UtcNow.AddDays(2), false, null, false, null,
        null, null, null, null, null, true, null);

    private IRenderedComponent<TestResultEntryPanel> RenderAboRh()
    {
        var definition = new TestDefinitionForEntryDto(
            "ABORH", "ABO/Rh type", ResultValueType.AboRh, null,
            [
                new ResolvedPanelSubtestDto("ANTI-A", "Anti-A", SubtestResultType.GradedReaction, Grades, true, 1),
                new ResolvedPanelSubtestDto("ANTI-B", "Anti-B", SubtestResultType.GradedReaction, Grades, true, 2),
                new ResolvedPanelSubtestDto("ANTI-D", "Anti-D", SubtestResultType.GradedReaction, Grades, true, 3),
                new ResolvedPanelSubtestDto("CTL", "Control", SubtestResultType.GradedReaction, Grades, false, 4)
            ],
            [], [], null, false);
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/reference/test-definitions/ABORH"] = definition });
        var panel = Render<TestResultEntryPanel>(p => p.Add(x => x.WorkItem, WorkItem()));
        panel.WaitForAssertion(() => Assert.Contains("Anti-D", panel.Markup));
        return panel;
    }

    // TEST-BB-684: required reactions start blank with a "—" choice; an optional one starts Not tested.
    [Fact]
    public void RequiredReactions_StartBlank()
    {
        var panel = RenderAboRh();
        var selects = panel.FindAll("tbody select");

        Assert.Equal(4, selects.Count);
        foreach (var required in selects.Take(3))
        {
            Assert.Equal(string.Empty, required.GetAttribute("value"));
            Assert.Equal("—", required.QuerySelector("option")!.TextContent);
        }

        Assert.Equal("NT", selects[3].GetAttribute("value"));
    }

    // TEST-BB-685: saving with readings missing is refused, naming them; nothing is sent.
    [Fact]
    public void Save_WithReadingsMissing_IsRefusedNamingThem()
    {
        var panel = RenderAboRh();
        panel.FindAll("tbody select")[0].Change("4+");

        panel.FindAll("button").First(b => b.TextContent.Trim() == "Save result").Click();
        panel.FindAll("button").First(b => b.TextContent.Trim() == "Save incomplete").Click();

        panel.WaitForAssertion(() => Assert.Contains("Enter a reading for Anti-B, Anti-D before saving.", panel.Markup));
    }
}
