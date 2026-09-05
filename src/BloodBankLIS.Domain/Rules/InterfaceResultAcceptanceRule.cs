namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Which inbound OBX-11 observation statuses may post a pending result.
/// Destructive or unknown statuses are rejected so the interface cannot silently
/// retract a released value. OCD-019.
/// </summary>
public static class InterfaceResultAcceptanceRule
{
    public const string Code = "RES-IFACE-OBX-STATUS";

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
}
