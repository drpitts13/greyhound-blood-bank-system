using AngleSharp.Dom;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// The issue desk's Ready to issue board, rendered: the summary line counts what can be issued now; each row shows the
/// ward's request, when its order is needed (red once passed, amber with the minutes left inside the warning window,
/// grey otherwise), and a separate "Expires before needed-by" mark only on the row it applies to.
/// </summary>
public class ReadyToIssuePageTests : BunitContext
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static ReadyToIssueWorkItemDto Unit(long id, string unitNumber) => new(
        id, 1, "MRN0001", "Demo, Patricia", "1970-01-01", 100 + id, unitNumber, "O+", false, null,
        Now.AddDays(2), false, Now.AddHours(-2), ProductAllocationDisplayStatus.ReadyForIssue, CrossmatchResult.Compatible,
        ProductName: "Red Blood Cells", UnitExpiresUtc: Now.AddDays(20), UnitBloodType: "O+");

    private IRenderedComponent<Issuing> RenderReady(params ReadyToIssueWorkItemDto[] rows)
    {
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/issues/ready-to-issue"] = rows.ToList() });
        var page = Render<Issuing>();
        page.WaitForAssertion(() => Assert.Contains(rows[0].UnitNumber!, page.Markup));
        return page;
    }

    private static IElement RowFor(IRenderedComponent<Issuing> page, string unitNumber) =>
        page.FindAll("tr").First(r => r.QuerySelector("td") is not null && r.TextContent.Contains(unitNumber));

    // TEST-BB-661
    [Fact]
    public void ReadyRows_ShowRequestNeededByAndExpiry_OnTheRightRow()
    {
        var page = RenderReady(
            Unit(1, "W-RDY-A") with
            {
                IssueRequestedUtc = Now.AddMinutes(-10), IssueRequestedBy = "RN Ward", IssueRequestedTo = "OR 3",
                OrderNumber = "ORD-A", NeededByUtc = Now.AddMinutes(20), NeededBySoon = true, NeededInMinutes = 20
            },
            Unit(2, "W-RDY-B") with { OrderNumber = "ORD-B", NeededByUtc = Now.AddMinutes(-5), NeededByPassed = true },
            Unit(3, "W-RDY-C") with { OrderNumber = "ORD-C", NeededByUtc = Now.AddDays(3), ExpiresBeforeNeeded = true },
            Unit(4, "W-RDY-D") with { SpecimenBlocksIssue = true, SpecimenBlockReason = "The specimen has expired." });

        Assert.Contains("3 can be issued now. 1 need attention first and are listed after them.", page.Markup);

        var a = RowFor(page, "W-RDY-A");
        Assert.Contains("Requested → OR 3", a.QuerySelector("[data-ready-requested]")!.TextContent);
        var soon = a.QuerySelector("[data-ready-needed-by]")!;
        Assert.Contains("badge-amber", soon.ClassName);
        Assert.EndsWith("(in 20 min)", soon.TextContent.Trim());
        Assert.Null(a.QuerySelector("[data-ready-expires-before-needed]"));

        var passed = RowFor(page, "W-RDY-B").QuerySelector("[data-ready-needed-by]")!;
        Assert.Contains("badge-red", passed.ClassName);
        Assert.EndsWith("(passed)", passed.TextContent.Trim());

        var c = RowFor(page, "W-RDY-C");
        var later = c.QuerySelector("[data-ready-needed-by]")!;
        Assert.Contains("badge-gray", later.ClassName);
        Assert.DoesNotContain("(", later.TextContent);
        Assert.Equal("Expires before needed-by", c.QuerySelector("[data-ready-expires-before-needed]")!.TextContent.Trim());

        var d = RowFor(page, "W-RDY-D");
        Assert.Null(d.QuerySelector("[data-ready-needed-by]"));
        Assert.Null(d.QuerySelector("[data-ready-expires-before-needed]"));
    }
}
