namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for reserving a unit to a patient, recording a crossmatch,
/// and releasing a reservation back to Available.
/// </summary>
public static class CompatibilityAuthorizationRule
{
    public const string AllocateCode = "XM-ALLOC-PERM";
    public const string CrossmatchCode = "XM-PERM";
    public const string ReleaseCode = "XM-REL-PERM";

    public static RuleResult EvaluateAllocate(bool hasCompatibilityAllocate) =>
        hasCompatibilityAllocate
            ? RuleResult.Pass(AllocateCode)
            : RuleResult.HardStop(
                AllocateCode,
                "Allocating a unit requires the compatibility.allocate permission.");

    public static RuleResult EvaluateCrossmatch(bool hasCompatibilityCrossmatch) =>
        hasCompatibilityCrossmatch
            ? RuleResult.Pass(CrossmatchCode)
            : RuleResult.HardStop(
                CrossmatchCode,
                "Recording a crossmatch requires the compatibility.crossmatch permission.");

    public static RuleResult EvaluateRelease(bool hasCompatibilityAllocate) =>
        hasCompatibilityAllocate
            ? RuleResult.Pass(ReleaseCode)
            : RuleResult.HardStop(
                ReleaseCode,
                "Releasing an allocation requires the compatibility.allocate permission.");
}
