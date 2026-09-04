namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for inventory actions that make a unit issuable
/// (quarantine release, unused-directed conversion, operational-hold release,
/// locating a missing unit, inspecting a damaged unit)
/// or that retire a unit into a modified product, rewrite unit identity,
/// receive a unit, save unit blood attributes used at compatibility,
/// return a unit to the supplier, discard a unit, transfer a unit between
/// locations, or recall a unit.
/// Lookback DIN recall uses <c>lookback.manage</c> instead (OCD-014).
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
    public const string TransferCode = "INV-XFER-PERM";
    public const string RecallCode = "INV-RCL-PERM";
    public const string SaveAttributeCode = "INV-ATTR-PERM";
    public const string ReturnToSupplierCode = "INV-RTS-PERM";
    public const string LocateMissingCode = "INV-LOC-PERM";
    public const string InspectDamagedCode = "INV-INSP-PERM";

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

    public static RuleResult EvaluateTransfer(bool hasInventoryTransfer) =>
        hasInventoryTransfer
            ? RuleResult.Pass(TransferCode)
            : RuleResult.HardStop(
                TransferCode,
                "Transferring a unit requires the inventory.transfer permission.");

    public static RuleResult EvaluateRecall(bool hasInventoryRecall) =>
        hasInventoryRecall
            ? RuleResult.Pass(RecallCode)
            : RuleResult.HardStop(
                RecallCode,
                "Recalling a unit from inventory requires the inventory.recall permission.");

    public static RuleResult EvaluateSaveAttribute(bool hasInventoryReceive) =>
        hasInventoryReceive
            ? RuleResult.Pass(SaveAttributeCode)
            : RuleResult.HardStop(
                SaveAttributeCode,
                "Saving a unit blood attribute requires the inventory.receive permission.");

    public static RuleResult EvaluateReturnToSupplier(bool hasInventoryReceive) =>
        hasInventoryReceive
            ? RuleResult.Pass(ReturnToSupplierCode)
            : RuleResult.HardStop(
                ReturnToSupplierCode,
                "Returning a unit to the supplier requires the inventory.receive permission.");

    public static RuleResult EvaluateLocateMissing(bool hasInventoryRelease) =>
        hasInventoryRelease
            ? RuleResult.Pass(LocateMissingCode)
            : RuleResult.HardStop(
                LocateMissingCode,
                "Locating a missing unit requires the inventory.release permission.");

    public static RuleResult EvaluateInspectDamaged(bool hasInventoryRelease) =>
        hasInventoryRelease
            ? RuleResult.Pass(InspectDamagedCode)
            : RuleResult.HardStop(
                InspectDamagedCode,
                "Inspecting a damaged unit requires the inventory.release permission.");
}


