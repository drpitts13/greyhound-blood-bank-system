using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class ReservationEndedTextTests
{
    // TEST-BB-1256: a reserved unit past its reservation end says so on the chart, with how long ago (end-user testing
    // pass 232); a running, issued, released, or open-ended reservation says nothing.
    [Fact]
    public void For_SaysWhenAReservationHasEnded()
    {
        var ends = new DateTime(2026, 11, 7, 9, 50, 0, DateTimeKind.Utc);
        var now = ends.AddDays(10).AddHours(17);
        Assert.Equal($"Ended {ends.ToLocalTime():yyyy-MM-dd HH:mm}, 10 d 17 h ago. The unit is still held for this patient: issue it when ready, or release it.",
            ReservationEndedText.For(AllocationStatus.Reserved, ends, null, now));
        Assert.Null(ReservationEndedText.For(AllocationStatus.Reserved, now.AddHours(1), null, now));
        Assert.Null(ReservationEndedText.For(AllocationStatus.Reserved, ends, 5, now));
        Assert.Null(ReservationEndedText.For(AllocationStatus.Released, ends, null, now));
        Assert.Null(ReservationEndedText.For(AllocationStatus.Reserved, null, null, now));
        // TEST-BB-1294 (end-user testing pass 268): with its crossmatch expired too, the note does not say to issue it.
        Assert.EndsWith("still held for this patient: repeat the crossmatch on a current specimen before issuing it, or release it.",
            ReservationEndedText.For(AllocationStatus.Reserved, ends, null, now, crossmatchExpired: true));
    }
}
