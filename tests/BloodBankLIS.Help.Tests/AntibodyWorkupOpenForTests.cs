using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

public class AntibodyWorkupOpenForTests : BunitContext
{
    // TEST-BB-1242 (end-user testing pass 220): each open antibody workup says how long it has been open, as the other
    // worklists do, beside the date it was opened.
    [Fact]
    public void OpenWorkups_SayHowLongTheyHaveBeenOpen()
    {
        var now = DateTime.UtcNow;
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/antibody-id"] = new List<AntibodyIdWorkupListItemDto>
            {
                new(5, 4, 9, "ACC-1", 2, "GHP-1", "Teaching panel", AntibodyWorkupStatus.InProgress, now.AddDays(-7).AddHours(-1), "tech",
                    "MRN0004", "Kellerman, Diane")
            }
        });
        var page = Render<AntibodyIdWorklist>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-workup-open-for]")));

        Assert.Equal("open 7 d", page.Find("[data-workup-open-for]").TextContent.Trim());
    }
}
