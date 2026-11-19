using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class SupplierOrderArrivalTextTests
{
    // TEST-BB-1243: a submitted order's arrival line says how late an overdue arrival is (end-user
    // testing pass 221); one not yet due reads as before.
    [Fact]
    public void Line_SaysHowLateAnOverdueArrivalIs()
    {
        var due = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
        var local = due.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        Assert.Equal($"Promised by the supplier for {local}: overdue 4 d 8 h, chase the supplier.",
            SupplierOrderArrivalText.Line(due, promised: true, overdue: true, due.AddDays(4).AddHours(8)));
        Assert.Equal($"Arrival due {local}.", SupplierOrderArrivalText.Line(due, promised: false, overdue: false, due.AddHours(-2)));
        // TEST-BB-1301 (end-user testing pass 275): the list badge says how long the arrival is overdue.
        Assert.Equal("Arrival overdue 5 d 10 h", SupplierOrderArrivalText.Badge(due, due.AddDays(5).AddHours(10)));
        Assert.Equal("Arrival overdue", SupplierOrderArrivalText.Badge(null, due));
    }
}
