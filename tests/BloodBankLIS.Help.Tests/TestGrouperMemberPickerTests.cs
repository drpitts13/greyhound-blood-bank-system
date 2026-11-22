using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages.Admin;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>The grouper member picker names each test and works by keyboard (end-user testing loop 2 tick 33).</summary>
public class TestGrouperMemberPickerTests : BunitContext
{
    // TEST-BB-1409: each candidate member is a button reading code and name, marked pressed once chosen.
    [Fact]
    public void MemberPicker_NamesTests_AndTogglesAsButtons()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/reference/test-definitions"] = new List<TestDefinitionListItemDto> { new("ABSC", "Antibody Screen", TestCategory.AntibodyScreen, Id: 1) }
        });
        var page = Render<AdminTestGroupers>();
        page.FindAll("button").First(b => b.TextContent.Trim() == "New grouper").Click();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-grouper-member='ABSC']")));
        var member = page.Find("[data-grouper-member='ABSC']");
        Assert.Equal("BUTTON", member.TagName);
        Assert.Contains("ABSC · Antibody Screen", member.TextContent);
        Assert.Equal("false", member.GetAttribute("aria-pressed"));
        member.Click();
        Assert.Equal("true", page.Find("[data-grouper-member='ABSC']").GetAttribute("aria-pressed"));
    }
}
