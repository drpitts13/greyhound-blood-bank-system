namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Chooses whether a product order should receive a serologic XM/CXM line
/// after the current antibody screen, and which configured test to use.
/// </summary>
public static class CrossmatchAttachmentRule
{
    public const string SkipWhenEligibleCode = "XM-ATTACH-EXM";

    public static string? ChooseSerologicTestCode(
        bool electronicXmEligible,
        bool requiresComplexCrossmatch,
        string negativeHistoryTestCode,
        string positiveHistoryTestCode)
    {
        if (electronicXmEligible)
        {
            return null;
        }

        return requiresComplexCrossmatch
            ? positiveHistoryTestCode
            : negativeHistoryTestCode;
    }
}
