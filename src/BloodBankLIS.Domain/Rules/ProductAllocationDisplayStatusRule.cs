using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Computes the patient Products-tab status for an active allocation from
/// crossmatch-required flag, latest XM result, and compatibility findings.
/// A compatible crossmatch that is no longer in date does not make the unit ready,
/// because issue refuses it (<see cref="CrossmatchValidityRule"/>).
/// </summary>
public static class ProductAllocationDisplayStatusRule
{
    public static ProductAllocationDisplayStatus Evaluate(
        bool requiresCrossmatch,
        CrossmatchResult? latestCrossmatchResult,
        bool hasCompatibilityException,
        bool hasCurrentCompatibleCrossmatch = true)
    {
        if (hasCompatibilityException
            || latestCrossmatchResult == CrossmatchResult.Incompatible)
        {
            return ProductAllocationDisplayStatus.Exception;
        }

        if (requiresCrossmatch)
        {
            if (latestCrossmatchResult != CrossmatchResult.Compatible)
            {
                return ProductAllocationDisplayStatus.Reserved;
            }

            return hasCurrentCompatibleCrossmatch
                ? ProductAllocationDisplayStatus.ReadyForIssue
                : ProductAllocationDisplayStatus.CrossmatchExpired;
        }

        return ProductAllocationDisplayStatus.ReadyForIssue;
    }
}
