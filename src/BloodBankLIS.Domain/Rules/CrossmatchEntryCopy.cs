using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Entry-form wording for a recorded crossmatch. Names method and
/// Compatible/Incompatible after save. This is UX for an already stored
/// serologic or electronic result, not a new regulatory claim.
/// </summary>
public static class CrossmatchEntryCopy
{
    public static string Recorded(CrossmatchMethod method, CrossmatchResult result) =>
        $"{method} crossmatch recorded as {result}.";

    public static string Recorded(CrossmatchMethod method, CrossmatchResult result, long id) =>
        $"{Recorded(method, result).TrimEnd('.')} (#{id}).";

    public static string WorklistSaved(ResultSource source, CrossmatchMethod method, CrossmatchResult result, bool verified) =>
        verified
            ? $"{ResultSourceEntryCopy.Saved(source)} {Recorded(method, result)} Verified."
            : $"{ResultSourceEntryCopy.Saved(source)} {Recorded(method, result)} Incomplete.";
}
