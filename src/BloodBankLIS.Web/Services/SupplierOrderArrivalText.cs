namespace BloodBankLIS.Web.Services;

/// <summary>
/// The arrival line of a submitted supplier order (end-user testing pass 221: SO-DEMO-002 read "Promised by the supplier
/// for 2026-11-13 09:00: overdue, chase the supplier" four days later, without saying how late it was).
/// </summary>
public static class SupplierOrderArrivalText
{
    public static string Line(DateTime dueUtc, bool promised, bool overdue, DateTime nowUtc) =>
        $"{(promised ? "Promised by the supplier for" : "Arrival due")} {dueUtc.ToLocalTime():yyyy-MM-dd HH:mm}"
        + (overdue ? $": overdue {ElapsedText.FromMinutes((int)Math.Max(0, (nowUtc - dueUtc).TotalMinutes))}, chase the supplier" : string.Empty)
        + ".";

    /// <summary>The list badge (pass 275): "Arrival overdue 5 d" rather than "Arrival overdue".</summary>
    public static string Badge(DateTime? dueUtc, DateTime nowUtc) =>
        dueUtc is DateTime due && nowUtc > due
            ? $"Arrival overdue {ElapsedText.FromMinutes((int)(nowUtc - due).TotalMinutes)}"
            : "Arrival overdue";
}
