namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing name for a reflex rule in the catalog.
/// The code stays so staff can select the rule.
/// A missing or blank name stays the rule id and is not replaced by the code.
/// </summary>
public static class ReflexRuleLabel
{
    /// <summary>
    /// Name for a reflex rule. A blank name stays the rule id.
    /// The rule code and the trigger or reflex test codes are not substitutes.
    /// </summary>
    public static string Name(long ruleId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"Reflex {ruleId}" : name.Trim();
}
