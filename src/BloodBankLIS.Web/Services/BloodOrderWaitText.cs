using BloodBankLIS.Application.Compliance;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// When a blood order still to prepare was placed and how long it has waited (end-user testing pass 167: a Stat order
/// placed eight days earlier showed only "ordered 2026-11-08 07:27", so nothing on the row said it had been waiting).
/// </summary>
public static class BloodOrderWaitText
{
    public static string Text(DateTime orderedUtc, DateTime nowUtc) =>
        $"ordered {orderedUtc.ToLocalTime():yyyy-MM-dd HH:mm} · {Waiting(orderedUtc, nowUtc)}";

    /// <summary>
    /// "waiting 10 d" (end-user testing pass 168: the Test Worklist showed a Stat crossmatch ordered ten days earlier
    /// with only its order time while no turnaround target is set).
    /// </summary>
    public static string Waiting(DateTime orderedUtc, DateTime nowUtc) => $"waiting {AlertInboxView.OpenFor(orderedUtc, nowUtc)}";
}
