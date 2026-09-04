namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for inventory actions that make a unit issuable
/// (quarantine release, unused-directed conversion, operational-hold release).
/// </summary>
public static class InventoryAuthorizationRule
{
    public const string QuarantineReleaseCode = "INV-REL-PERM";
    public const string DirectedConversionCode = "INV-DIR-PERM";
    public const string HoldReleaseCode = "INV-HOLD-PERM";

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

    public static RuleResult EvaluateHoldRelease(bool hasInventoryRelease) =>
        hasInventoryRelease
            ? RuleResult.Pass(HoldReleaseCode)
            : RuleResult.HardStop(
                HoldReleaseCode,
                "Releasing a unit from operational hold requires the inventory.release permission.");
}


