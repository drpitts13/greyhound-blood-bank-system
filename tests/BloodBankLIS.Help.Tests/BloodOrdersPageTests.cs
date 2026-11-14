using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// Blood orders to prepare, rendered: each row's specimen cell says one thing about the specimen (usable until a time,
/// type and screen not verified, or none usable) and adds the needed-by warning beside it, never "Usable … None usable"
/// (the Cycle 573 defect, which data tests could not see). The next-step button carries the row's step and link.
/// </summary>
public class BloodOrdersPageTests : BunitContext
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private static BloodOrderToPrepareDto Row(string number, bool usable, bool typeAndScreenPending = false, bool expiresBeforeNeeded = false,
        string nextStep = "Reserve 1 unit", string href = "/patients/1?tab=products") => new(
        1, number, 1, "Demo, Patricia", "MRN0001", "O+", [], OrderPriority.Routine, false, Now.AddHours(-1), "1 × Red Blood Cells",
        1, 0, 0, 1, usable, usable ? Now.AddDays(1) : null, nextStep, href,
        UsableSpecimenId: usable ? 7 : null, TypeAndScreenPending: typeAndScreenPending,
        NeededByUtc: expiresBeforeNeeded ? Now.AddDays(2) : null, SpecimenExpiresBeforeNeeded: expiresBeforeNeeded);

    private IRenderedComponent<BloodOrders> RenderWith(params BloodOrderToPrepareDto[] rows)
    {
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/blood-orders/to-prepare"] = rows.ToList() });
        var page = Render<BloodOrders>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("table.bb tbody tr")));
        return page;
    }

    private static string SpecimenCell(IRenderedComponent<BloodOrders> page, string orderNumber) =>
        page.FindAll("table.bb tbody tr").Single(r => r.TextContent.Contains(orderNumber)).Children[5].TextContent;

    // TEST-BB-660
    [Fact]
    public void SpecimenCell_SaysOneThing_WithTheNeededByWarningBeside()
    {
        var page = RenderWith(
            Row("ORD-USABLE", usable: true),
            Row("ORD-NONE", usable: false, nextStep: "Collect a specimen", href: "/patients/1?tab=specimens&panel=specimen"),
            Row("ORD-PENDING", usable: true, typeAndScreenPending: true, nextStep: "Finish type and screen", href: "/patients/1?tab=tests"),
            Row("ORD-LATE-SPEC", usable: true, expiresBeforeNeeded: true, nextStep: "Collect a new specimen", href: "/patients/1?tab=specimens&panel=specimen"));

        var usable = SpecimenCell(page, "ORD-USABLE");
        Assert.Contains("Usable until", usable);
        Assert.DoesNotContain("None usable", usable);
        Assert.DoesNotContain("Expires before needed-by", usable);

        var none = SpecimenCell(page, "ORD-NONE");
        Assert.Contains("None usable", none);
        Assert.DoesNotContain("Usable until", none);

        var pending = SpecimenCell(page, "ORD-PENDING");
        Assert.Contains("Type and screen not verified", pending);
        Assert.DoesNotContain("None usable", pending);

        var lateSpecimen = SpecimenCell(page, "ORD-LATE-SPEC");
        Assert.Contains("Usable until", lateSpecimen);
        Assert.Contains("Expires before needed-by: draw a new specimen", lateSpecimen);
        Assert.DoesNotContain("None usable", lateSpecimen);

        var button = page.FindAll("table.bb tbody tr").Single(r => r.TextContent.Contains("ORD-LATE-SPEC")).QuerySelector("td.right a")!;
        Assert.Equal(("Collect a new specimen", "/patients/1?tab=specimens&panel=specimen"), (button.TextContent.Trim(), button.GetAttribute("href")));
    }

    // TEST-BB-708: an order that lost a reserved unit to expiry says so on its row, with the unit and when; others do not.
    [Fact]
    public void ExpiredReservation_IsNamedOnItsRow()
    {
        var page = RenderWith(
            Row("ORD-LOST", usable: true) with { ExpiredReservations = ["Unit W0001230000001 expired at 2026-11-08 10:00 UTC."] },
            Row("ORD-PLAIN", usable: true));

        var lost = page.FindAll("table.bb tbody tr").Single(r => r.TextContent.Contains("ORD-LOST"));
        var note = lost.QuerySelector("[data-expired-reservations]")!.TextContent;
        Assert.Contains("Reserved unit expired", note);
        Assert.Contains("Unit W0001230000001 expired at 2026-11-08 10:00 UTC.", note);
        Assert.Null(page.FindAll("table.bb tbody tr").Single(r => r.TextContent.Contains("ORD-PLAIN")).QuerySelector("[data-expired-reservations]"));
    }
}
