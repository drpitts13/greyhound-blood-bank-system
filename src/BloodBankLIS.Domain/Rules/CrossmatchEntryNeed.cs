using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Whether a reserved unit still needs a manual serologic crossmatch entered.
/// Electronic crossmatch and products that do not require crossmatch are excluded.
/// </summary>
public static class CrossmatchEntryNeed
{
    public static bool ForAllocation(
        bool requiresCrossmatch,
        CrossmatchResult? result,
        string? testCode,
        AllocationStatus status,
        long? issueId)
    {
        if (!requiresCrossmatch || issueId is not null || status != AllocationStatus.Reserved)
        {
            return false;
        }

        if (result is CrossmatchResult.Compatible or CrossmatchResult.Incompatible)
        {
            return false;
        }

        return !IsElectronic(testCode);
    }

    public static bool IsElectronic(string? testCode) =>
        string.Equals(
            testCode?.Trim(),
            CrossmatchSettings.DefaultElectronicTestCode,
            StringComparison.OrdinalIgnoreCase);
}
