using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// A specificity typed into the interpretation was listed as free text that "will not drive antigen-negative", even
/// when the server would match it to the catalog and post it as such. The page now matches typed text as the server
/// does, so the list shows what will post (Cycle 643).
/// </summary>
public class AntibodyIdTypedSpecificityTests : BunitContext
{
    private IRenderedComponent<AntibodyIdentification> Open()
    {
        var cell = new AntibodyIdCellDto(1, "1", PanelCellRole.Panel, 1, false, [], [new AntibodyIdReactionDto("AHG", ReactionGrade.TwoPlus)]);
        var workup = new AntibodyIdWorkupDetailDto(1, 1, 5, null, AntibodyWorkupStatus.InProgress, AntibodyIdDatResult.NotPerformed,
            null, null, null, null, null, null, null, null, false, null, null, null, [], [cell], [], ["IS", "37C", "AHG"], true);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/antibody-id/1"] = workup,
            ["/api/reference/blood-attributes"] = new List<BloodAttributeListItemDto>
            {
                new(11, "K", "Kell", "anti-K", true, 1),
                new(12, "E", "E antigen", "anti-E", true, 2)
            }
        });
        var page = Render<AntibodyIdentification>(p => p.Add(c => c.PatientId, 1L).Add(c => c.WorkupId, 1L));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("#abid-interpret")));
        return page;
    }

    private static void TypeAndAdd(IRenderedComponent<AntibodyIdentification> page, string text)
    {
        page.Find("#abid-interpret input[placeholder='anti-Vel']").Change(text);
        page.FindAll("#abid-interpret button").First(b => b.TextContent.Trim() == "Add specificity").Click();
    }

    private static string Listed(IRenderedComponent<AntibodyIdentification> page) =>
        string.Join(" | ", page.FindAll("#abid-interpret li").Select(li => li.TextContent.Replace("Remove", "").Trim()));

    // TEST-BB-785: "anti-k" typed is listed as the catalog's anti-K, marked catalog, so it will drive antigen-negative
    // selection as the server will post it.
    [Fact]
    public void TypedCatalogName_IsListedAsTheCatalogAntibody()
    {
        var page = Open();
        TypeAndAdd(page, "anti-k");
        var listed = Listed(page);
        Assert.StartsWith("anti-K — Identified (catalog)", listed);
    }

    // TEST-BB-786: text the catalog does not hold, or text naming two antibodies, stays as typed and says it is not in
    // the catalog and will not drive antigen-negative selection.
    [Fact]
    public void UnmatchedOrAmbiguousText_StaysAsTyped()
    {
        var page = Open();
        TypeAndAdd(page, "anti-Vel");
        TypeAndAdd(page, "anti-K anti-E");
        var items = page.FindAll("#abid-interpret li").Select(li => li.TextContent).ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains("anti-Vel", items[0]);
        Assert.Contains("anti-K anti-E", items[1]);
        Assert.All(items, i => Assert.Contains("not in the catalog; will not drive antigen-negative selection", i));
    }
}
