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

    public static RuleResult EvaluateAccess(
        bool hasImmunoOverride,
        bool hasResultVerify,
        bool hasInventoryRelease) =>
        hasImmunoOverride || hasResultVerify || hasInventoryRelease
            ? RuleResult.Pass(PermissionCode)
            : RuleResult.HardStop(
                PermissionCode,
                "The supervisor queue requires immuno.override, result.verify, or inventory.release.");

    public static bool IncludeAntibodyReview(bool hasImmunoOverride) => hasImmunoOverride;

    public static bool IncludeResultVerify(bool hasResultVerify) => hasResultVerify;

    public static bool IncludeQuarantineRelease(bool hasInventoryRelease) => hasInventoryRelease;

    public static int KindSortOrder(string kind) => kind switch
    {
        KindAntibodyReview => 0,
        KindResultVerify => 1,
        KindQuarantineRelease => 2,
        _ => 99
    };
}
