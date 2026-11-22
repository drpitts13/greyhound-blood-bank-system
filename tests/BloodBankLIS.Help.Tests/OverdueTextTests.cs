using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class OverdueTextTests
{
    // TEST-BB-1254: an issued unit not yet received says how long past its due time it is (end-user testing pass 230).
    [Fact]
    public void Badge_SaysHowLongOverdue()
    {
        var due = new DateTime(2026, 11, 6, 10, 38, 0, DateTimeKind.Utc);
        Assert.Equal("Overdue 11 d 9 h", OverdueText.Badge(due, due.AddDays(11).AddHours(9)));
        Assert.Equal("Overdue 25 min", OverdueText.Badge(due, due.AddMinutes(25)));
        Assert.Equal("Overdue", OverdueText.Badge(null, due));
    }
}
