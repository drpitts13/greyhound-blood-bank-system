namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Validates whether a test may be performed on a specimen type.
/// </summary>
public static class SpecimenTypeCompatibilityRule
{
    public static RuleEvaluation Evaluate(
        string specimenTypeCode,
        string testCode,
        string? requiredSpecimenType,
        IReadOnlySet<string> excludedTestCodes)
    {
        var results = new List<RuleResult>();
        var normalizedType = specimenTypeCode.Trim();
        var normalizedTest = testCode.Trim();

        if (!string.IsNullOrWhiteSpace(requiredSpecimenType)
            && !string.Equals(requiredSpecimenType.Trim(), normalizedType, StringComparison.OrdinalIgnoreCase))
        {
            results.Add(RuleResult.HardStop(
                "SPECIMEN.TYPE.REQUIRED",
                $"Test {normalizedTest} requires specimen type '{requiredSpecimenType.Trim()}' but specimen is '{normalizedType}'."));
        }

        if (excludedTestCodes.Contains(normalizedTest))
        {
            results.Add(RuleResult.HardStop(
                "SPECIMEN.TYPE.EXCLUDED",
                $"Test {normalizedTest} cannot be performed on specimen type '{normalizedType}'."));
        }

        return new RuleEvaluation(results);
    }
}
