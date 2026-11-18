using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// A reservation past its end time on the chart's Products tab (end-user testing pass 232: MRN0022's two units, whose
/// reservations ended on 11-07, showed only "Crossmatch expired" on the chart, while Ready to issue said "Reservation
/// ended"). Null while the reservation runs, once issued, or when it has no end time. Pass 268: when its crossmatch has
/// also expired, the note no longer says "issue it when ready" (the line below says to repeat the crossmatch first).
/// </summary>
public static class ReservationEndedText
{
    public static string? For(AllocationStatus status, DateTime? reservationEndsUtc, long? issueId, DateTime nowUtc, bool crossmatchExpired = false) =>
        status == AllocationStatus.Reserved && issueId is null && reservationEndsUtc is DateTime ends && ends <= nowUtc
            ? $"Ended {ends.ToLocalTime():yyyy-MM-dd HH:mm}, {ElapsedText.FromMinutes((int)(nowUtc - ends).TotalMinutes)} ago. The unit is still held for this patient: "
                + (crossmatchExpired
                    ? "repeat the crossmatch on a current specimen before issuing it, or release it."
                    : "issue it when ready, or release it.")
            : null;
}
