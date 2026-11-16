using BloodBankLIS.Application.Compliance;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// When a blood order still to prepare was placed and how long it has waited (end-user testing pass 167: a Stat order
/// placed eight days earlier showed only "ordered 2026-11-08 07:27", so nothing on the row said it had been waiting).
/// </summary>
public static class BloodOrderWaitText
{
    public static string Text(DateTime orderedUtc, DateTime nowUtc) =>
        $"ordered {orderedUtc.ToLocalTime():yyyy-MM-dd HH:mm} · waiting {AlertInboxView.OpenFor(orderedUtc, nowUtc)}";
}
