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

    /// <summary>
    /// For the transfusion form, before a unit is started (pass 236): when it was issued and, when received on the ward,
    /// when and by whom, so a unit held on the ward for days is seen before it is spiked. Null once started.
    /// </summary>
    public static string? BeforeStart(DateTime issuedUtc, DateTime? wardReceivedUtc, string? receivedBy, DateTime? startedUtc, DateTime nowUtc)
    {
        if (startedUtc is not null)
        {
            return null;
        }

        var issued = $"Issued {issuedUtc.ToLocalTime():yyyy-MM-dd HH:mm}, {Ago(issuedUtc, nowUtc)} ago";
        return wardReceivedUtc is DateTime received
            ? $"{issued}; received on the ward {received.ToLocalTime():yyyy-MM-dd HH:mm}{(string.IsNullOrWhiteSpace(receivedBy) ? "" : $" by {receivedBy}")}, {Ago(received, nowUtc)} ago. Check the unit and its time out of storage under your procedure before starting."
            : issued;
    }

    private static string Ago(DateTime from, DateTime nowUtc) => ElapsedText.FromMinutes((int)Math.Max(0, (nowUtc - from).TotalMinutes));
}
