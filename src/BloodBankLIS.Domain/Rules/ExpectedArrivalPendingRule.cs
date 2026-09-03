using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace expected inbound / packing-list follow-up: ASN units stay
/// on the worklist until arrival, cancel, or return to supplier. Overdue when
/// past <c>Inventory.ExpectedArrivalDueHours</c> (default 24).
/// </summary>
public static class ExpectedArrivalPendingRule
{
    public const string Code = "INV-EXPECT-OVERDUE";

    public static bool IsPending(UnitStatus status) => status == UnitStatus.Expected;

    public static RuleResult EvaluateOverdue(DateTime? dueUtc, DateTime now)
    {
        if (dueUtc is null || now <= dueUtc.Value)
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.Warning(Code, "Expected inbound unit has not arrived and is past the due time.");
    }
}
