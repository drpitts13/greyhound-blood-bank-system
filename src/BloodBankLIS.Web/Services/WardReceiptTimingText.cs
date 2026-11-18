namespace BloodBankLIS.Web.Services;

/// <summary>
/// When the unit being received was issued, and how far past its due time (end-user testing pass 235: receiving
/// W0001230009204, issued eleven days earlier, the form listed who issued it but not when). Information only: whether the
/// unit is still fit to transfuse is the facility's procedure.
/// </summary>
public static class WardReceiptTimingText
{
    public static string? For(DateTime? issuedUtc, DateTime? dueUtc, DateTime nowUtc)
    {
        if (issuedUtc is not DateTime issued)
        {
            return null;
        }

        var text = $"Issued {issued.ToLocalTime():yyyy-MM-dd HH:mm}, {ElapsedText.FromMinutes((int)Math.Max(0, (nowUtc - issued).TotalMinutes))} ago";
        return dueUtc is DateTime due && nowUtc > due
            ? $"{text}; due on the ward by {due.ToLocalTime():yyyy-MM-dd HH:mm}, now {ElapsedText.FromMinutes((int)(nowUtc - due).TotalMinutes)} late. Check the unit and its time out of storage under your procedure before transfusion."
            : text;
    }
}
