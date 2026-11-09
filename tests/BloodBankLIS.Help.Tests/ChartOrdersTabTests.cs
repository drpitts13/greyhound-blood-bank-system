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
