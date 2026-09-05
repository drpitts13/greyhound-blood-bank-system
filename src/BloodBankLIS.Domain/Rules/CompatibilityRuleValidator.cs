using System.Text.Json;
using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Validates versioned compatibility tables (AABB 5.1.5 change control, 21 CFR 11.10).
/// </summary>
public static class CompatibilityRuleValidator
{
    public const string VersionRequiredCode = "COMPAT.VERSION.REQUIRED";
    public const string VersionDuplicateCode = "COMPAT.VERSION.DUPLICATE";
    public const string PolicyRequiredCode = "COMPAT.POLICY.REQUIRED";
    public const string EffectiveRequiredCode = "COMPAT.EFFECTIVE.REQUIRED";
    public const string NotesRequiredCode = "COMPAT.NOTES.REQUIRED";
    public const string SingleActiveCode = "COMPAT.VERSION.SINGLE-ACTIVE";
    public const string ReasonCode = "COMPAT.REASON.REQUIRED";
    public const string RuleCodeRequired = "COMPAT.RULE.CODE.REQUIRED";
    public const string RuleCodeDuplicate = "COMPAT.RULE.CODE.DUPLICATE";
    public const string SeverityCode = "COMPAT.RULE.SEVERITY";
    public const string DescriptionCode = "COMPAT.RULE.DESCRIPTION";
    public const string ExpressionCode = "COMPAT.RULE.EXPRESSION";
    public const string VersionMissingCode = "COMPAT.VERSION.MISSING";

    public static RuleEvaluation ValidateVersion(
        CompatibilityRuleVersion version,
        bool duplicateVersionName,
        string? changeReason,
        bool requireReason)
    {
        ArgumentNullException.ThrowIfNull(version);
        var results = new List<RuleResult>();

        if (string.IsNullOrWhiteSpace(version.Version))
        {
            results.Add(RuleResult.HardStop(VersionRequiredCode, "Version identifier is required."));
        }

        if (duplicateVersionName)
        {
            results.Add(RuleResult.HardStop(VersionDuplicateCode, $"Compatibility table version '{version.Version}' already exists."));
        }

        if (string.IsNullOrWhiteSpace(version.PolicyVersion))
        {
            results.Add(RuleResult.HardStop(PolicyRequiredCode, "Policy version is required."));
        }

        if (version.EffectiveDate == default)
        {
            results.Add(RuleResult.HardStop(EffectiveRequiredCode, "Effective date is required."));
        }

        if (string.IsNullOrWhiteSpace(version.Notes))
        {
            results.Add(RuleResult.HardStop(NotesRequiredCode, "Medical-director / institutional review notes are required."));
        }

        if (requireReason && (string.IsNullOrWhiteSpace(changeReason) || changeReason.Trim().Length < 8))
        {
            results.Add(RuleResult.HardStop(ReasonCode, "A change reason of at least 8 characters is required."));
        }

        return new RuleEvaluation(results);
    }

    public static RuleEvaluation ValidateRule(
        CompatibilityRule rule,
        bool versionExists,
        bool duplicateRuleCode)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var results = new List<RuleResult>();

        if (!versionExists)
        {
            results.Add(RuleResult.HardStop(VersionMissingCode, "Compatibility table version was not found."));
            return new RuleEvaluation(results);
        }

        if (string.IsNullOrWhiteSpace(rule.RuleCode))
        {
            results.Add(RuleResult.HardStop(RuleCodeRequired, "Rule code is required."));
        }

        if (duplicateRuleCode)
        {
            results.Add(RuleResult.HardStop(RuleCodeDuplicate, $"Rule code '{rule.RuleCode}' already exists on this version."));
        }

        if (!CompatibilityRuleCatalog.Severities.Contains(rule.Severity))
        {
            results.Add(RuleResult.HardStop(SeverityCode, "Severity must be HardStop, Warning, or Pass."));
        }

        if (string.IsNullOrWhiteSpace(rule.Description))
        {
            results.Add(RuleResult.HardStop(DescriptionCode, "Description is required."));
        }

        var json = string.IsNullOrWhiteSpace(rule.ExpressionJson) ? "{}" : rule.ExpressionJson.Trim();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                results.Add(RuleResult.HardStop(ExpressionCode, "Expression JSON must be an object."));
            }
        }
        catch (JsonException)
        {
            results.Add(RuleResult.HardStop(ExpressionCode, "Expression JSON is not valid JSON."));
        }

        return new RuleEvaluation(results);
    }

    public static string NormalizeSeverity(string? severity)
    {
        if (string.Equals(severity, "Warning", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        if (string.Equals(severity, "Pass", StringComparison.OrdinalIgnoreCase))
        {
            return "Pass";
        }

        return "HardStop";
    }
}
