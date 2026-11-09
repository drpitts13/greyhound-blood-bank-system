using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using BloodBankLIS.Web.Services;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Store tubes' Due for discard (Cycle 615): sorted by date alone, pulling the tubes meant walking between racks, back
/// and forth. The list is now grouped by place, the place holding the oldest tube first.
/// </summary>
public class TubePullListTests : BunitContext
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static TubeInPlaceDto Tube(string accession, string? place, int daysPast) => new(
        1, accession, "EDTA", SpecimenStatus.Received, 5, "Demo, Pat", "MRN5", Now.AddDays(-10), Now.AddDays(-9), "tech",
        place, Now.AddDays(-daysPast));

    // TEST-BB-732: tubes in one place stay together whatever their dates; the place holding the oldest tube comes first;
    // the same place typed two ways is one place under its most common spelling; a tube with no place is its own group.
    [Fact]
    public void Group_PutsEachPlaceTogether_OldestPlaceFirst()
    {
        var places = TubePullList.Group(
        [
            Tube("T1", "Rack 2", 3),
            Tube("T2", "Rack 1", 1),
            Tube("T3", "rack-2", 9),
            Tube("T4", "Rack 1", 5),
            Tube("T5", null, 2),
            Tube("T6", "Rack 2", 4)
        ]);

        Assert.Equal(["Rack 2", "Rack 1", "No place recorded"], places.Select(p => p.Name));
        Assert.Equal(["T3", "T6", "T1"], places[0].Tubes.Select(t => t.AccessionNumber));
        Assert.Equal(["T4", "T2"], places[1].Tubes.Select(t => t.AccessionNumber));
        Assert.Empty(TubePullList.Group([]));
    }

    // TEST-BB-733: rendered, each place heads its tubes with a count, and each tube says how long it is past its date.
    [Fact]
    public void StoreTubes_ShowsDueTubesByPlace()
    {
        var due = DateTime.UtcNow;
        TubeInPlaceDto Due(string accession, string place, int daysPast) => new(
            1, accession, "EDTA", SpecimenStatus.Received, 5, "Demo, Pat", "MRN5", due.AddDays(-10), due.AddDays(-9), "tech",
            place, due.AddDays(-daysPast).AddMinutes(-5));
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/specimens/due-for-discard"] = new List<TubeInPlaceDto> { Due("T1", "Rack 2", 3), Due("T2", "Rack 1", 1), Due("T3", "Rack 2", 9) }
        });

        var page = Render<StoreTubes>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-pull-place]").Count));

        var groups = page.FindAll("[data-pull-place]");
        Assert.Equal(["Rack 2", "Rack 1"], groups.Select(g => g.GetAttribute("data-pull-place")));
        Assert.Contains("2 tubes", groups[0].QuerySelector("tr.group-row")!.TextContent);
        Assert.Equal(["T3", "T1"], groups[0].QuerySelectorAll("[data-pull-tube]").Select(r => r.GetAttribute("data-pull-tube")));
        Assert.Contains("9 d past", groups[0].QuerySelector("[data-pull-tube='T3']")!.TextContent);
        Assert.Contains("1 tube", groups[1].QuerySelector("tr.group-row")!.TextContent);
    }
}
