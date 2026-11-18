using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class WardReceiptTimingTextTests
{
    // TEST-BB-1259: the ward receipt says when the unit was issued and, past its due time, how late it is (end-user
    // testing pass 235).
    [Fact]
    public void For_SaysWhenIssuedAndHowLate()
    {
        var issued = new DateTime(2026, 11, 6, 9, 38, 0, DateTimeKind.Utc);
        var due = issued.AddHours(1);
        var now = issued.AddDays(11).AddHours(15);
        string L(DateTime d) => d.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        Assert.Equal($"Issued {L(issued)}, 11 d 15 h ago; due on the ward by {L(due)}, now 11 d 14 h late. Check the unit and its time out of storage under your procedure before transfusion.",
            WardReceiptTimingText.For(issued, due, now));
        Assert.Equal($"Issued {L(issued)}, 20 min ago", WardReceiptTimingText.For(issued, due, issued.AddMinutes(20)));
        Assert.Null(WardReceiptTimingText.For(null, due, now));
    }
}
