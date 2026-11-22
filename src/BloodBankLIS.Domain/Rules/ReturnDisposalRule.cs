using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A returned unit is never disposed of by the return itself. Disposal goes through the discard step,
/// which needs inventory.discard, a coded discard reason, and the facility's second verifier. A return
/// that asks for disposal, or whose seal or appearance failed, sends the unit to quarantine and says so.
/// </summary>
public static class ReturnDisposalRule
{
    public const string Code = "RET-DISPOSE-VIA-DISCARD";

    /// <summary>Where a return that cannot go back to stock leaves the unit.</summary>
    public static UnitQuarantineReason QuarantineReason(bool disposalRequested, bool inspectionFailed) =>
        disposalRequested && !inspectionFailed
            ? UnitQuarantineReason.ReturnedForDisposal
            : UnitQuarantineReason.FailedReturnInspection;

    public static RuleResult Notice(string unitNumber, bool disposalRequested, bool inspectionFailed) =>
        RuleResult.Warning(Code,
            (inspectionFailed
                ? $"Unit {unitNumber} failed return inspection and is in quarantine. "
                : $"Unit {unitNumber} is in quarantine. ")
            + (disposalRequested || inspectionFailed
                ? "To dispose of it, discard it on the unit page with a discard reason (and a second verifier if your facility requires one)."
                : string.Empty));
}
