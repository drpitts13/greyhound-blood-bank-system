namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Supervisor queue access is permission-scoped. Items come from existing
/// clinical queues; listing them does not change issue, result, or review gates.
/// </summary>
public static class SupervisorQueueRule
{
    public const string PermissionCode = "SUP-QUEUE-PERM";

    public const string KindAntibodyReview = "AntibodyIdReview";
    public const string KindResultVerify = "ResultPendingVerification";
    public const string KindQuarantineRelease = "QuarantineRelease";
    public const string KindOverrideReview = "OverrideReview";
    public const string KindDiscrepancy = "InventoryDiscrepancy";
    /// <summary>
    /// A unit on operational hold (end-user testing pass 137: a unit held nine days for a packing slip was on no queue or
    /// home card, so a forgotten hold kept the unit out of stock with nobody reminded). Waits on inventory.release.
    /// </summary>
    public const string KindOperationalHold = "OperationalHold";
    /// <summary>An RhIG evaluation filled in and waiting for a verifier to complete it (Cycle 537).</summary>
    public const string KindRhigComplete = "RhigComplete";
    /// <summary>OCD-083: a transfusion the EHR reported for a unit recalled, quarantined, damaged or missing (Cycle 700).</summary>
    public const string KindTransfusionReported = "TransfusionReported";

    /// <summary>A reported transfusion waits on whoever releases stock or reviews overrides.</summary>
    public static bool IncludeTransfusionReported(bool hasInventoryRelease, bool hasOverrideApprove) => hasInventoryRelease || hasOverrideApprove;

    public static RuleResult EvaluateAccess(
        bool hasImmunoOverride,
        bool hasResultVerify,
        bool hasInventoryRelease,
        bool hasOverrideApprove = false) =>
        hasImmunoOverride || hasResultVerify || hasInventoryRelease || hasOverrideApprove
            ? RuleResult.Pass(PermissionCode)
            : RuleResult.HardStop(
                PermissionCode,
                "The supervisor queue requires immuno.override, result.verify, inventory.release, or override.approve.");

    public static bool IncludeOverrideReview(bool hasOverrideApprove) => hasOverrideApprove;

    public static bool IncludeAntibodyReview(bool hasImmunoOverride) => hasImmunoOverride;

    public static bool IncludeResultVerify(bool hasResultVerify) => hasResultVerify;

    public static bool IncludeQuarantineRelease(bool hasInventoryRelease) => hasInventoryRelease;

    /// <summary>Missing and damaged units wait on whoever can locate, inspect, or discard them (inventory.release).</summary>
    public static bool IncludeDiscrepancy(bool hasInventoryRelease) => hasInventoryRelease;

    /// <summary>Units on operational hold wait on whoever can release them (inventory.release).</summary>
    public static bool IncludeOperationalHold(bool hasInventoryRelease) => hasInventoryRelease;

    public static readonly IReadOnlyList<string> Kinds =
        [KindAntibodyReview, KindResultVerify, KindQuarantineRelease, KindDiscrepancy, KindOperationalHold, KindTransfusionReported, KindOverrideReview];

    /// <summary>The queue kind a link names (<c>?kind=</c>), matched without regard to case; null when it names none.</summary>
    public static string? KindFromQuery(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Kinds.FirstOrDefault(k => string.Equals(k, value.Trim(), StringComparison.OrdinalIgnoreCase));

    public static int KindSortOrder(string kind) => kind switch
    {
        KindAntibodyReview => 0,
        KindResultVerify => 1,
        KindQuarantineRelease => 2,
        KindDiscrepancy => 3,
        KindOperationalHold => 4,
        KindTransfusionReported => 5,
        KindOverrideReview => 6,
        _ => 99
    };
}
