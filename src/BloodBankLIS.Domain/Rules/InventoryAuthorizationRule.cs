namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for inventory actions that make a unit issuable
/// (quarantine release, unused-directed conversion, operational-hold release)
/// or that retire a unit into a modified product, rewrite unit identity,
/// receive a unit, or discard a unit.
/// </summary>
public static class InventoryAuthorizationRule
{
    public const string QuarantineReleaseCode = "INV-REL-PERM";
    public const string DirectedConversionCode = "INV-DIR-PERM";
    public const string HoldReleaseCode = "INV-HOLD-PERM";
    public const string ModifyCode = "INV-MOD-PERM";
    public const string CorrectIdentityCode = "INV-ID-PERM";
    public const string ReceiveCode = "INV-RCV-PERM";
    public const string DiscardCode = "INV-DISC-PERM";

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

    public static RuleResult EvaluateModify(bool hasInventoryModify) =>
        hasInventoryModify
            ? RuleResult.Pass(ModifyCode)
            : RuleResult.HardStop(
                ModifyCode,
                "Modifying a blood product requires the inventory.modify permission.");

    public static RuleResult EvaluateCorrectIdentity(bool hasInventoryCorrectIdentity) =>
        hasInventoryCorrectIdentity
            ? RuleResult.Pass(CorrectIdentityCode)
            : RuleResult.HardStop(
                CorrectIdentityCode,
                "Correcting unit identity requires the inventory.correct-identity permission.");

    public static RuleResult EvaluateReceive(bool hasInventoryReceive) =>
        hasInventoryReceive
            ? RuleResult.Pass(ReceiveCode)
            : RuleResult.HardStop(
                ReceiveCode,
                "Receiving a unit into inventory requires the inventory.receive permission.");

    public static RuleResult EvaluateDiscard(bool hasInventoryDiscard) =>
        hasInventoryDiscard
            ? RuleResult.Pass(DiscardCode)
            : RuleResult.HardStop(
                DiscardCode,
                "Discarding a unit requires the inventory.discard permission.");
}


