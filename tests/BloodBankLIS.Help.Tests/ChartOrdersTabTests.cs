using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Shared;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// The chart's Orders tab, rendered: an order's needed-by time reads as on the worklists, red with "(passed)" once it
/// has gone, amber with the minutes left inside the facility's warning window, and plain otherwise.
/// </summary>
public class ChartOrdersTabTests : BunitContext
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static PatientOrderDto Order(long id, string number, DateTime? neededBy, bool passed = false, bool soon = false, int? inMinutes = null) => new(
        id, 1, 1, "VIS-1", number, OrderCategory.Product, "Red cells", OrderPriority.Routine, OrderStatus.New, 1, "OR", null, "Dr. Demo",
        null, null, null, FulfillmentStatus.Ordered, OrderSource.Manual, Now.AddHours(-1), null, [],
        NeededByUtc: neededBy, NeededByPassed: passed, NeededBySoon: soon, NeededInMinutes: inMinutes);

    private IRenderedComponent<PatientChartOrdersTab> LinkPanel(params BloodBankLIS.Application.Specimens.SpecimenDto[] all) =>
        LinkPanel(false, all);

    private IRenderedComponent<PatientChartOrdersTab> LinkPanel(bool product, params BloodBankLIS.Application.Specimens.SpecimenDto[] all)
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var patient = new PatientDto(12, "MRN0022", "Demo-Multi", "Riley", null, new DateOnly(1970, 1, 1), Sex.Female, PatientStatus.Active, Now, "test");
        var order = new PatientOrderDto(7, 12, 1, "VIS-1", "ORD-TS", OrderCategory.Test, "Type and screen", OrderPriority.Routine, OrderStatus.New,
            1, "OR", null, "Dr. Demo", null, null, null, FulfillmentStatus.Ordered, OrderSource.Manual, Now.AddHours(-1), null,
            [product
                ? new OrderLineDto(1, 1, OrderCategory.Product, "Red Blood Cells", null, OrderType.Other, 3, FulfillmentStatus.Ordered, null)
                : new OrderLineDto(1, 1, OrderCategory.Test, "ABO/Rh", "ABORH", OrderType.AboRh, null, null, null)]);
        return Render<PatientChartOrdersTab>(p => p
            .Add(c => c.Patient, patient)
            .Add(c => c.Orders, new List<PatientOrderDto> { order })
            .Add(c => c.LinkingOrderId, 7L)
            .Add(c => c.AllSpecimens, all));
    }

    private static BloodBankLIS.Application.Specimens.SpecimenDto Tube(string accession, SpecimenStatus status, DateTime? expires, DateTime? discarded = null) =>
        new(1, accession, 12, "EDTA", null, null, null, null, Now.AddMinutes(-30), null, expires, status, null, DiscardedUtc: discarded);

    // TEST-BB-806: with nothing to link, the Link specimen panel names a tube already drawn (or received) for the patient
    // and links to Pending specimens instead of only saying to accession one; with no such tube, or only an expired or
    // discarded one, it says to accession a specimen as before.
    [Fact]
    public void LinkPanel_NamesTheTubeOnItsWay()
    {
        var panel = LinkPanel(Tube("ACC-DEMO-RCV1", SpecimenStatus.Collected, Now.AddDays(3)));
        var note = panel.Find("[data-link-tube-awaiting]");
        Assert.Contains("Tube ACC-DEMO-RCV1 was drawn", note.TextContent);
        Assert.Contains("Then link it here.", note.TextContent);
        Assert.Equal("/test-worklist?tab=specimens", note.QuerySelector("a")!.GetAttribute("href"));
        Assert.DoesNotContain("Accession a specimen first", panel.Markup);
    }

    // A product order also names the tube, and says its type and screen must be completed before it can be linked.
    [Fact]
    public void LinkPanel_ProductOrder_NamesTheTubeAndTheTypeAndScreen()
    {
        var panel = LinkPanel(true, Tube("ACC-DEMO-RCV1", SpecimenStatus.Collected, Now.AddDays(3)));
        var note = panel.Find("[data-link-tube-awaiting]");
        Assert.Contains("Tube ACC-DEMO-RCV1 was drawn", note.TextContent);
        Assert.Contains("Then complete its type and screen and link it here.", note.TextContent);
        Assert.Contains("No active specimens with a completed type and screen are available.", panel.Markup);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void LinkPanel_WithoutATubeOnItsWay_SaysToAccession(bool expired, bool discarded)
    {
        var tubes = expired || discarded
            ? new[] { Tube("ACC-OLD", SpecimenStatus.Collected, expired ? Now.AddMinutes(-1) : Now.AddDays(3), discarded ? Now.AddMinutes(-5) : null) }
            : [];
        var panel = LinkPanel(tubes);
        Assert.Empty(panel.FindAll("[data-link-tube-awaiting]"));
        Assert.Contains("Accession a specimen first", panel.Markup);
    }

    // TEST-BB-666
    [Fact]
    public void NeededBy_ReadsAsOnTheWorklists()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        var patient = new PatientDto(1, "MRN0001", "Demo", "Patricia", null, new DateOnly(1970, 1, 1), Sex.Female, PatientStatus.Active, Now, "test");
        var tab = Render<PatientChartOrdersTab>(p => p
            .Add(c => c.Patient, patient)
            .Add(c => c.Orders, new List<PatientOrderDto>
            {
                Order(1, "ORD-SOON", Now.AddMinutes(25), soon: true, inMinutes: 25),
                Order(2, "ORD-PASSED", Now.AddMinutes(-10), passed: true),
                Order(3, "ORD-LATER", Now.AddDays(2)),
                Order(4, "ORD-NONE", null)
            }));

        string NeededBy(string number, out string cls)
        {
            var record = tab.FindAll("tbody.record").Single(r => r.TextContent.Contains(number));
            var span = record.QuerySelector("[data-order-needed-by-shown]");
            cls = span?.ClassName ?? string.Empty;
            return span?.TextContent.Trim() ?? string.Empty;
        }

        Assert.EndsWith("(in 25 min)", NeededBy("ORD-SOON", out var soonClass));
        Assert.Contains("badge-amber", soonClass);
        Assert.EndsWith("(passed)", NeededBy("ORD-PASSED", out var passedClass));
        Assert.Contains("badge-red", passedClass);
        var later = NeededBy("ORD-LATER", out var laterClass);
        Assert.StartsWith("needed by", later);
        Assert.DoesNotContain("(", later);
        Assert.Equal("sub", laterClass);
        Assert.Equal(string.Empty, NeededBy("ORD-NONE", out _));
    }
}
