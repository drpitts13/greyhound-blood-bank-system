namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for inventory actions that make a unit issuable
/// (quarantine release, unused-directed conversion).
/// </summary>
public static class InventoryAuthorizationRule
{
    public const string QuarantineReleaseCode = "INV-REL-PERM";
    public const string DirectedConversionCode = "INV-DIR-PERM";

    public static RuleResult EvaluateQuarantineRelease(bool hasInventoryRelease) =>
        hasInventoryRelease
            ? RuleResult.Pass(QuarantineReleaseCode)
            : RuleResult.HardStop(
                QuarantineReleaseCode,
                "Releasing a unit from quarantine requires the inventory.release permission.");

    public static RuleResult EvaluateDirectedConversion(bool hasInventoryRelease) =>
        hasInventoryRelease
            ? RuleResult.Pass(DirectedConversionCode)
            : RuleResult.HardStop(
                DirectedConversionCode,
                "Converting a directed unit to allogeneic inventory requires the inventory.release permission.");
}

