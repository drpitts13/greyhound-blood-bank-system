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
    }
}
