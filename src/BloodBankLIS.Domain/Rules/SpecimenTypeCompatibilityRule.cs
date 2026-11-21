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

        if (!AcceptedSpecimenTypes.Accepts(requiredSpecimenType, normalizedType))
        {
            results.Add(RuleResult.HardStop(
                "SPECIMEN.TYPE.REQUIRED",
                $"Test {normalizedTest} requires specimen type {AcceptedSpecimenTypes.Describe(requiredSpecimenType)} but specimen is '{normalizedType}'."));
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
