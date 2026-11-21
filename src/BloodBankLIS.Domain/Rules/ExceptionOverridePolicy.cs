using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Pure gate: whether a user's max security level may override a given exception definition.
/// </summary>
public static class ExceptionOverridePolicy
{
    /// <summary>
    /// The rule codes the software looks up here before it lets a stop be overridden. A definition for any other code
    /// is listed for reference only: marking it overridable does not create an override (end-user testing loop 2 tick 47).
    /// </summary>
    public static readonly IReadOnlyList<string> OverrideCodes =
    [
        BloodAttributeCompatibilityRule.AntigenNegCode,
        AntibodyHistoryCrossmatchRule.RuleCode,
        AboRhDeltaRule.DeltaCode,
        CrossmatchSpecimenRule.RetrospectiveCode,
        SpecimenExpirationOverrideRule.Code,
    ];

    /// <summary>Whether the software consults this definition before allowing an override.</summary>
    public static bool IsUsedForOverride(string? ruleCode) =>
        !string.IsNullOrWhiteSpace(ruleCode) && OverrideCodes.Contains(ruleCode.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool CanOverride(int userMaxSecurityLevel, ExceptionDefinition? definition)
    {
        if (definition is null || !definition.IsActive || !definition.IsOverridable)
        {
            return false;
        }

        return userMaxSecurityLevel >= definition.MinSecurityLevel;
    }

    public static RuleResult EvaluateAccess(int userMaxSecurityLevel, ExceptionDefinition? definition, string ruleCode)
    {
        if (definition is null || !definition.IsActive)
        {
            return RuleResult.HardStop(
                "EXC-DEF-MISSING",
                $"No active exception definition for '{ruleCode}'; override is not permitted.");
        }

        if (!definition.IsOverridable)
        {
            return RuleResult.HardStop(
                definition.RuleCode,
                $"Exception '{definition.RuleCode}' is not overridable.");
        }

        if (userMaxSecurityLevel < definition.MinSecurityLevel)
        {
            return RuleResult.HardStop(
                "EXC-SECURITY-LEVEL",
                $"Security level {userMaxSecurityLevel} is below the minimum {definition.MinSecurityLevel} required to override '{definition.RuleCode}'.");
        }

        return RuleResult.Pass("EXC-SECURITY-LEVEL");
    }
}
