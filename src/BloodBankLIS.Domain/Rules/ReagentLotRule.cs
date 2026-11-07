using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The life of a reagent lot: a lot is not received already expired or under a number still in use for the same
/// reagent, not opened once expired or closed, and an expired lot takes no daily QC (it is closed and replaced).
/// </summary>
public static class ReagentLotRule
{
    public const string ReceiveCode = "REAGENT-LOT-RECEIVE";
    public const string OpenCode = "REAGENT-LOT-OPEN";
    public const string QcCode = "REAGENT-LOT-QC";

    /// <summary>Within this many days of expiry the lot is shown as expiring soon.</summary>
    public const int ExpiringSoonDays = 7;

    public static RuleResult EvaluateReceive(DateTime expiresUtc, DateTime nowUtc, bool numberInUse)
    {
        if (expiresUtc <= nowUtc)
        {
            return RuleResult.HardStop(ReceiveCode, $"This lot expired {expiresUtc:yyyy-MM-dd}; do not put it into use.");
        }

        return numberInUse
            ? RuleResult.HardStop(ReceiveCode, "This reagent already has an open or received lot with that number. Check the lot number on the vial.")
            : RuleResult.Pass(ReceiveCode);
    }

    public static RuleResult EvaluateOpen(ReagentLotState state, DateTime expiresUtc, DateTime nowUtc)
    {
        if (state == ReagentLotState.Closed)
        {
            return RuleResult.HardStop(OpenCode, "A closed lot cannot be opened.");
        }

        return expiresUtc <= nowUtc
            ? RuleResult.HardStop(OpenCode, $"This lot expired {expiresUtc:yyyy-MM-dd}; close it and open a new lot.")
            : RuleResult.Pass(OpenCode);
    }

    public static RuleResult EvaluateQc(ReagentLotState state, DateTime expiresUtc, DateTime nowUtc)
    {
        if (state == ReagentLotState.Closed)
        {
            return RuleResult.HardStop(QcCode, "Record QC on an open or received lot.");
        }

        return expiresUtc <= nowUtc
            ? RuleResult.HardStop(QcCode, $"This lot expired {expiresUtc:yyyy-MM-dd}; close it rather than testing it.")
            : RuleResult.Pass(QcCode);
    }

    /// <summary>"expired 2026-11-01", "expires today", "1 day left", "12 days left".</summary>
    public static string ExpiryText(DateTime expiresUtc, DateTime nowUtc)
    {
        if (expiresUtc <= nowUtc)
        {
            return $"expired {expiresUtc:yyyy-MM-dd}";
        }

        var days = (expiresUtc.Date - nowUtc.Date).Days;
        return days switch
        {
            0 => "expires today",
            1 => "1 day left",
            _ => $"{days} days left"
        };
    }

    public static bool ExpiringSoon(DateTime expiresUtc, DateTime nowUtc) =>
        expiresUtc > nowUtc && (expiresUtc.Date - nowUtc.Date).Days <= ExpiringSoonDays;
}
