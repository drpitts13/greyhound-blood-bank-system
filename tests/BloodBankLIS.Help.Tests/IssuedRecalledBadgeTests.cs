using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>Issue &amp; Transfuse flags a unit recalled while it is out (Cycle 691).</summary>
public class IssuedRecalledBadgeTests : BunitContext
{
    // TEST-BB-923: the issued unit recalled while out is marked "Recalled: do not transfuse, return it" with the recall's
    // reason; the other issued unit is not.
    [Fact]
    public void IssuedUnits_FlagARecalledUnit()
    {
        var issued = DateTime.UtcNow.AddHours(-1);
        OutstandingIssueWorkItemDto Unit(long issueId, string unit, bool recalled) => new(
            issueId, 5, "MRN5", "Demo, Pat", "1980-01-01", issueId + 100, unit, "O+", false, null,
            null, false, issued, issued.AddMinutes(20), "nurse1", null, "4W", IssueStatus.Issued, ProductName: "Red Blood Cells")
        { UnitRecalled = recalled, RecallReason = recalled ? "Donor subsequently reactive" : null };
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/outstanding"] = new List<OutstandingIssueWorkItemDto> { Unit(42, "W0001230009206", true), Unit(43, "W0001230009207", false) }
        });

        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.StartsWith("Issued units")).Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-issued-recalled]")));

        var badge = Assert.Single(page.FindAll("[data-issued-recalled]"));
        Assert.Contains("Recalled: do not transfuse, return it", badge.TextContent);
        Assert.Equal("Donor subsequently reactive", badge.GetAttribute("title"));
        Assert.Contains("W0001230009206", page.FindAll("table.bb tbody tr").First(r => r.QuerySelector("[data-issued-recalled]") is not null).TextContent);
    }

    // TEST-BB-925 (Cycle 692): In transit marks a unit recalled on its way "Recalled: do not accept, return it".
    [Fact]
    public void InTransit_FlagsARecalledUnit()
    {
        var issued = DateTime.UtcNow.AddMinutes(-20);
        InTransitWorkItemDto Unit(long issueId, string unit, bool recalled) => new(
            issueId, 5, "MRN5", "Demo, Pat", issueId + 100, unit, "O+", false, null, null, false, issued, null, false, null, "4W", IssueStatus.Issued)
        { UnitRecalled = recalled, RecallReason = recalled ? "Donor subsequently reactive" : null };
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/in-transit"] = new List<InTransitWorkItemDto> { Unit(42, "W0001230009206", true), Unit(43, "W0001230009207", false) }
        });

        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.StartsWith("In transit")).Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-transit-recalled]")));
        var badge = Assert.Single(page.FindAll("[data-transit-recalled]"));
        Assert.Contains("Recalled: do not accept, return it", badge.TextContent);
        Assert.Equal("Donor subsequently reactive", badge.GetAttribute("title"));
    }

    // TEST-BB-1340 (end-user testing pass 313): Issued units shows a unit's cooler check while it is still in its cooler,
    // and says which unit the check found missing; a unit received on the ward shows neither.
    [Fact]
    public void IssuedUnits_ShowTheCoolerCheck_AndTheMissingUnit()
    {
        var issued = DateTime.UtcNow.AddHours(-3);
        OutstandingIssueWorkItemDto Unit(long issueId, string unit, DateTime? received) => new(
            issueId, 5, "MRN5", "Demo, Pat", "1980-01-01", issueId + 100, unit, "O+", false, null,
            null, false, issued, received, received is null ? null : "nurse1", "Cooler 1", "Trauma Bay 1", IssueStatus.Issued, ProductName: "Red Blood Cells");
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/issues/outstanding"] = new List<OutstandingIssueWorkItemDto>
            {
                Unit(41, "W0001230009101", null), Unit(42, "W0001230009102", null), Unit(43, "W0001230009103", issued.AddMinutes(10))
            },
            ["/api/issues/coolers"] = new List<OpenCoolerDto>
            {
                new("Cooler 1", 3, ["Demo, Pat"], ["Trauma Bay 1"], issued, issued.AddMinutes(5), false, LastCheckSummary: "3 of 4 packed, 1 missing")
                    { MissingUnits = ["W0001230009101"] }
            }
        });

        var page = Render<Issuing>();
        page.FindAll("button").First(b => b.TextContent.StartsWith("Issued units")).Click();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-cooler-missing]")));

        Assert.Equal(["W0001230009101"], page.FindAll("[data-cooler-missing]").Select(n => n.GetAttribute("data-cooler-missing")));
        Assert.Equal(2, page.FindAll("[data-cooler-check-badge]").Count);
    }
}
