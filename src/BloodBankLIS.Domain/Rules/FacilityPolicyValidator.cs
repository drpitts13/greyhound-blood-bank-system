using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Domain.Rules;

public static class FacilityPolicyValidator
{
    public const string ReasonCode = "POLICY.REASON.REQUIRED";
    public const string LegalHoldCode = "POLICY.LEGAL-HOLD";
    public const string UnknownKeyCode = "POLICY.KEY.UNKNOWN";
    public const string BooleanCode = "POLICY.VALUE.BOOLEAN";
    public const string IntegerCode = "POLICY.VALUE.INTEGER";
    public const string RangeCode = "POLICY.VALUE.RANGE";

    public static RuleEvaluation Validate(SystemSetting setting, string? newValue, string? changeReason, bool isLegalHold)
    {
        ArgumentNullException.ThrowIfNull(setting);
        var results = new List<RuleResult>();

        if (isLegalHold)
        {
            results.Add(RuleResult.HardStop(LegalHoldCode, "This policy is under legal hold and cannot be changed (21 CFR 11.10)."));
            return new RuleEvaluation(results);
        }

        if (string.IsNullOrWhiteSpace(changeReason) || changeReason.Trim().Length < 8)
        {
            results.Add(RuleResult.HardStop(ReasonCode, "A change reason of at least 8 characters is required for facility policy edits."));
        }

        var definition = FacilityPolicyCatalog.Find(setting.Key);
        if (definition is null)
        {
            results.Add(RuleResult.HardStop(UnknownKeyCode, $"'{setting.Key}' is not a recognized facility policy key."));
            return new RuleEvaluation(results);
        }

        var value = (newValue ?? string.Empty).Trim();
        switch (definition.Kind)
        {
            case FacilityPolicyValueKind.Boolean:
                if (!bool.TryParse(value, out _))
                {
                    results.Add(RuleResult.HardStop(BooleanCode, $"{definition.DisplayName} must be true or false."));
                }
                break;
            case FacilityPolicyValueKind.Integer:
                if (!int.TryParse(value, out var parsed))
                {
                    results.Add(RuleResult.HardStop(IntegerCode, $"{definition.DisplayName} must be a whole number."));
                    break;
                }

                if (definition.MinInclusive is { } min && parsed < min)
                {
                    results.Add(RuleResult.HardStop(RangeCode, $"{definition.DisplayName} cannot be less than {min} ({definition.Citation})."));
                }

                if (definition.MaxInclusive is { } max && parsed > max)
                {
                    results.Add(RuleResult.HardStop(RangeCode, $"{definition.DisplayName} cannot be greater than {max} ({definition.Citation})."));
                }

                break;
        }

        return new RuleEvaluation(results);
    }
}
