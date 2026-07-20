using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Computes the patient Products-tab status for an active allocation from
/// crossmatch-required flag, latest XM result, and compatibility findings.
/// </summary>
public static class ProductAllocationDisplayStatusRule
{
    public static ProductAllocationDisplayStatus Evaluate(
        bool requiresCrossmatch,
        CrossmatchResult? latestCrossmatchResult,
        bool hasCompatibilityException)
    {
        if (hasCompatibilityException
            || latestCrossmatchResult == CrossmatchResult.Incompatible)
        {
            return ProductAllocationDisplayStatus.Exception;
        }

        if (requiresCrossmatch)
        {
            return latestCrossmatchResult == CrossmatchResult.Compatible
                ? ProductAllocationDisplayStatus.ReadyForIssue
                : ProductAllocationDisplayStatus.Reserved;
        }

        return ProductAllocationDisplayStatus.ReadyForIssue;
    }
}
