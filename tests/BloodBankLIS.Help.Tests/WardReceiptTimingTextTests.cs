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
    // TEST-BB-1260: before a unit is started, the transfusion form says when it was issued and when it was received on the
    // ward, and how long ago (end-user testing pass 236); nothing once it has started.
    [Fact]
    public void BeforeStart_SaysWhenIssuedAndReceived()
    {
        var issued = new DateTime(2026, 11, 6, 8, 45, 0, DateTimeKind.Utc);
        var received = issued.AddMinutes(12);
        var now = issued.AddDays(11).AddHours(15);
        string L(DateTime d) => d.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        Assert.Equal($"Issued {L(issued)}, 11 d 15 h ago; received on the ward {L(received)} by RN Demo, 11 d 14 h ago. Check the unit and its time out of storage under your procedure before starting.",
            WardReceiptTimingText.BeforeStart(issued, received, "RN Demo", null, now));
        Assert.Equal($"Issued {L(issued)}, 11 d 15 h ago", WardReceiptTimingText.BeforeStart(issued, null, null, null, now));
        Assert.Null(WardReceiptTimingText.BeforeStart(issued, received, "RN Demo", received.AddMinutes(5), now));
    }
}
