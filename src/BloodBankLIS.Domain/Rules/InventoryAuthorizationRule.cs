namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gate for releasing a unit from quality quarantine to Available.
/// </summary>
public static class InventoryAuthorizationRule
{
    public const string QuarantineReleaseCode = "INV-REL-PERM";

    public static RuleResult EvaluateQuarantineRelease(bool hasInventoryRelease) =>
        hasInventoryRelease
            ? RuleResult.Pass(QuarantineReleaseCode)
            : RuleResult.HardStop(
                QuarantineReleaseCode,
                "Releasing a unit from quarantine requires the inventory.release permission.");
}
