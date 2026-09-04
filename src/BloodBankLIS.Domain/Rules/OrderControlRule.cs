using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// HL7 ORC-1 order control (SoftBank / SafeTrace inbound ORM). New orders stay on the
/// create path; this rule applies CA/DC/HD/RL against an existing order.
/// </summary>
public static class OrderControlRule
{
    public const string Code = "ORM-ORC";
    public const string UnknownCode = "ORM-ORC-UNKNOWN";
    public const string MissingCode = "ORM-ORC-MISSING";
    public const string TerminalCode = "ORM-ORC-TERMINAL";
    public const string NotHeldCode = "ORM-ORC-NOTHELD";
    public const string IssueCode = "ISS-ORDER-STATUS";

    public static bool IsNewOrder(string? control)
    {
        var orc = Normalize(control);
        return orc is "" or "NW" or "SN" or "OK";
    }

    public static bool IsFulfillable(OrderStatus status) =>
        status is OrderStatus.New
            or OrderStatus.InProcess
            or OrderStatus.Collected
            or OrderStatus.Received
            or OrderStatus.PartiallyComplete;

    public static RuleResult EvaluateIssue(bool orderLinked, bool orderIsFulfillable)
    {
        if (!orderLinked || orderIsFulfillable)
        {
            return RuleResult.Pass(IssueCode);
        }

        return RuleResult.HardStop(
            IssueCode,
            "The linked order is cancelled, discontinued, on hold, or already complete and cannot be issued against.");
    }

    public static RuleResult Apply(Order order, string? control, string? reason)
    {
        ArgumentNullException.ThrowIfNull(order);
        var orc = Normalize(control);
        if (orc.Length == 0)
        {
            return RuleResult.HardStop(MissingCode, "Order control (ORC-1) is required.");
        }

        switch (orc)
        {
            case "CA":
                return Cancel(order, OrderStatus.Cancelled, reason, "Cancelled via HL7 ORM");
            case "DC":
                return Cancel(order, OrderStatus.Discontinued, reason, "Discontinued via HL7 ORM");
            case "HD":
                if (order.Status == OrderStatus.OnHold)
                {
                    return RuleResult.Pass(Code, "Order is already on hold.");
                }

                if (!IsFulfillable(order.Status))
                {
                    return RuleResult.HardStop(TerminalCode, "A closed order cannot be placed on hold.");
                }

                order.Status = OrderStatus.OnHold;
                return RuleResult.Pass(Code, "Order placed on hold.");
            case "RL":
                if (order.Status != OrderStatus.OnHold)
                {
                    return RuleResult.HardStop(NotHeldCode, "Release requires an order that is on hold.");
                }

                order.Status = OrderStatus.InProcess;
                return RuleResult.Pass(Code, "Order released from hold.");
            case "XO":
            case "SC":
            case "XX":
                return RuleResult.Pass(Code, $"Order control {orc} acknowledged.");
            default:
                return RuleResult.HardStop(UnknownCode, $"Unsupported order control '{orc}'.");
        }
    }

    private static RuleResult Cancel(Order order, OrderStatus target, string? reason, string defaultReason)
    {
        if (order.Status == OrderStatus.Completed)
        {
            return RuleResult.HardStop(TerminalCode, "A completed order cannot be cancelled or discontinued.");
        }

        if (order.Status == target)
        {
            return RuleResult.Pass(Code, $"Order already {target}.");
        }

        order.Status = target;
        order.CancellationReason ??= string.IsNullOrWhiteSpace(reason) ? defaultReason : reason.Trim();
        order.FulfillmentStatus = FulfillmentStatus.Cancelled;
        return RuleResult.Pass(Code, target == OrderStatus.Cancelled ? "Order cancelled." : "Order discontinued.");
    }

    private static string Normalize(string? control) =>
        string.IsNullOrWhiteSpace(control) ? string.Empty : control.Trim().ToUpperInvariant();
}
