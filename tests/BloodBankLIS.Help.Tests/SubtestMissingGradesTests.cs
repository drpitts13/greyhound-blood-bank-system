using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages.Admin;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>Graded subtests lacking a standard grade say so (end-user testing loop 2 tick 13).</summary>
public class SubtestMissingGradesTests : BunitContext
{
    private static SubtestDefinitionDto Subtest(long id, string code, SubtestResultType type, params string[] codes) => new(
        id, code, code, type, codes.Select(c => new SubtestChoiceDto(c, c, ReactionPolarity.Neutral)).ToList(), 1, true, false, null, null, null);

    // TEST-BB-1395: a graded subtest saved without mf lists it as missing; a complete one, and a pick list, list nothing.
    // Opening the old one offers Add missing standard grades, which keeps its choices and adds mf.
    [Fact]
    public void List_NamesMissingStandardGrades_AndAddKeepsExistingChoices()
    {
        var old = Subtest(1, "IS", SubtestResultType.GradedReaction, "0", "1+", "2+", "3+", "4+", "H", "w+", "+/-", "NT", "CUSTOM");
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/subtests"] = new List<SubtestDefinitionDto>
            {
                old,
                Subtest(2, "AHG", SubtestResultType.GradedReaction, "0", "1+", "2+", "3+", "4+", "H", "w+", "+/-", "mf", "NT"),
                Subtest(3, "PICK", SubtestResultType.PickList, "Yes", "No")
            },
            ["/api/admin/subtests/1"] = old
        });
        var page = Render<AdminSubtests>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-subtest-missing-grades='IS']")));
        Assert.Equal("Missing standard: mf", page.Find("[data-subtest-missing-grades='IS']").GetAttribute("title"));

        // TEST-BB-1425 (loop 2 tick 58): one summary above the list, with an Add to all action, instead of the note on every row.
        Assert.Equal(
            "1 graded subtest (IS) lacks the standard grade mf, marked * in Choices. Add them here or with Add missing standard grades on each subtest.",
            page.Find("[data-subtests-missing-summary] div").TextContent.Trim());
        Assert.NotNull(page.Find("[data-subtests-add-missing-all]"));
        Assert.Empty(page.FindAll("[data-subtest-missing-grades='AHG']"));
        Assert.Empty(page.FindAll("[data-subtest-missing-grades='PICK']"));

        page.FindAll("button").First(b => b.TextContent.Trim() == "Edit").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-subtest-add-missing]")));
        page.Find("[data-subtest-add-missing]").Click();
        var values = page.FindAll("input").Select(i => i.GetAttribute("value")).ToList();
        Assert.Contains("CUSTOM", values);
        Assert.Contains("mf", values);
        Assert.Empty(page.FindAll("[data-subtest-add-missing]"));
    }
}
