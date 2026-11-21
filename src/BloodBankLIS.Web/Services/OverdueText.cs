namespace BloodBankLIS.Web.Services;

/// <summary>
/// "Overdue 11 d 9 h" for a unit not yet received on the ward (end-user testing pass 230: the In transit and Issued
/// lists said only "Overdue" for units issued eleven days earlier, the same as for one ten minutes late).
/// </summary>
public static class OverdueText
{
    public static string Badge(DateTime? dueUtc, DateTime nowUtc) =>
        dueUtc is DateTime due && nowUtc > due
            ? $"Overdue {ElapsedText.FromMinutes((int)(nowUtc - due).TotalMinutes)}"
            : "Overdue";
}
