namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Keeping the reagent register (Cycle 713): a reagent code is used once (ignoring case), a rename or retirement has a
/// reason, and a reagent is not retired while it has lots received or open, since those are on the shelf or in use.
/// </summary>
public static class ReagentRegisterRule
{
    public const string Code = "REAGENT-REGISTER";

    public static RuleResult EvaluateCreate(string? code, string? name, bool codeInUse) =>
        string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)
            ? RuleResult.HardStop(Code, "Reagent code and name are required.")
            : codeInUse
                ? RuleResult.HardStop(Code, $"Reagent {code.Trim()} is already in the register (a retired one can be restored).")
                : RuleResult.Pass(Code);

    public static RuleResult EvaluateRename(string? name, string? reason) =>
        string.IsNullOrWhiteSpace(name)
            ? RuleResult.HardStop(Code, "Enter the reagent's name.")
            : string.IsNullOrWhiteSpace(reason)
                ? RuleResult.HardStop(Code, "Say why the name changes.")
                : RuleResult.Pass(Code);

    /// <param name="lotsOnHand">Lots of the reagent still received or open.</param>
    public static RuleResult EvaluateRetire(string? reason, int lotsOnHand) =>
        lotsOnHand > 0
            ? RuleResult.HardStop(Code,
                $"{lotsOnHand} lot{(lotsOnHand == 1 ? " is" : "s are")} still received or open. Close {(lotsOnHand == 1 ? "it" : "them")} first, so no lot of a retired reagent is left in use.")
            : string.IsNullOrWhiteSpace(reason)
                ? RuleResult.HardStop(Code, "Say why the reagent is retired (for example, discontinued by the manufacturer, replaced by another).")
                : RuleResult.Pass(Code);
}
