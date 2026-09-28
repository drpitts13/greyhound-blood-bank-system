using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Which inbound OBX-11 observation statuses may post a pending result.
/// Destructive or unknown statuses are rejected so the interface cannot silently
/// retract a released value. OCD-019.
/// A cancelled or discontinued order is also rejected so the result is not
/// filed where the bench worklist no longer shows it.
/// </summary>
public static class InterfaceResultAcceptanceRule
{
    public const string Code = "RES-IFACE-OBX-STATUS";
    public const string ClosedOrderCode = "RES-IFACE-ORDER-CLOSED";

    public static RuleResult Evaluate(string? obxStatus)
    {
        var code = (obxStatus ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0 || code is "F" or "C" or "P" or "R")
        {
            return RuleResult.Pass(Code);
        }

        return RuleResult.HardStop(
            Code,
            $"OBX observation status '{obxStatus}' is not accepted for interface result posting.");
    }

    public static RuleResult EvaluateOrder(OrderStatus status)
    {
        if (status is OrderStatus.Cancelled or OrderStatus.Discontinued)
        {
            var label = status == OrderStatus.Cancelled ? "cancelled" : "discontinued";
            return RuleResult.HardStop(
                ClosedOrderCode,
                $"The order is {label}; an interface result cannot be posted.");
        }

        return RuleResult.Pass(ClosedOrderCode);
    }
}
