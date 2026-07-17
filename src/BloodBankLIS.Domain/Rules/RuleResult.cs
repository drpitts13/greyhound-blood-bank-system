namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Severity of a single safety rule result. The engine never auto-downgrades a
/// HardStop (see docs/safety-rules.md).
/// </summary>
public enum RuleSeverity
{
    Pass = 0,
    Warning = 1,
    HardStop = 2
}

/// <summary>
/// Outcome of evaluating one safety rule. Carries a stable code so the UI and
/// audit can identify exactly which check fired.
/// </summary>
public sealed record RuleResult(string Code, RuleSeverity Severity, string Message)
{
    public static RuleResult Pass(string code, string message = "") =>
        new(code, RuleSeverity.Pass, message);

    public static RuleResult Warning(string code, string message) =>
        new(code, RuleSeverity.Warning, message);

    public static RuleResult HardStop(string code, string message) =>
        new(code, RuleSeverity.HardStop, message);
}
