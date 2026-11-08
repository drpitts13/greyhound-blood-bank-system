namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The time an order is needed by (a surgery start, a scheduled transfusion): optional; not before the order was placed.
/// The worklists prepare by it and say when it has passed. It changes no priority or check.
/// </summary>
public static class OrderNeededByRule
{
    public const string Code = "ORD-NEEDED-BY";

    public static RuleResult Evaluate(DateTime orderedUtc, DateTime? neededByUtc) =>
        neededByUtc is DateTime needed && needed < orderedUtc.AddMinutes(-1)
            ? RuleResult.HardStop(Code, "The needed-by time is before the order was placed; check the date and time.")
            : RuleResult.Pass(Code);

    public static bool Passed(DateTime? neededByUtc, DateTime nowUtc) => neededByUtc is DateTime needed && needed <= nowUtc;
}
