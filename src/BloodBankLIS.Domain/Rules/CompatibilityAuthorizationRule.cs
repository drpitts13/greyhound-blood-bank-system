namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for reserving a unit to a patient and recording a crossmatch.
/// </summary>
public static class CompatibilityAuthorizationRule
{
    public const string AllocateCode = "XM-ALLOC-PERM";
    public const string CrossmatchCode = "XM-PERM";

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
}
