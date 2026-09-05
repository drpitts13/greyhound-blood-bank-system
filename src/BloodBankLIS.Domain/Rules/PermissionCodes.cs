namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The canonical catalog of use-case permission codes. Authorization evaluates these
/// codes (roles aggregate them); the UI and API never compare role strings directly
/// (see docs/architecture.md 4.2). Codes are stable identifiers, like rule codes.
/// </summary>
public static class PermissionCodes
{
    public const string PatientWrite = "patient.write";

    /// <summary>
    /// Combine a duplicate patient into the surviving record. Distinct from
    /// <see cref="PatientWrite"/> so demographic edits do not imply merge.
    /// </summary>
    public const string PatientMerge = "patient.merge";

    public const string SpecimenAccession = "specimen.accession";
    public const string SpecimenReject = "specimen.reject";
    public const string SpecimenEdit = "specimen.edit";

    public const string ResultEnter = "result.enter";
    public const string ResultVerify = "result.verify";
    public const string ResultCorrect = "result.correct";
    public const string ResultInvalidate = "result.invalidate";

    public const string ImmunoRecord = "immuno.record";
    public const string ImmunoOverride = "immuno.override";

    public const string InventoryReceive = "inventory.receive";
    public const string InventoryTransfer = "inventory.transfer";
    public const string InventoryRelease = "inventory.release";
    public const string InventoryDiscard = "inventory.discard";
    public const string InventoryRecall = "inventory.recall";
    public const string InventoryCorrectIdentity = "inventory.correct-identity";
    public const string InventoryModify = "inventory.modify";

    public const string CompatibilityCrossmatch = "compatibility.crossmatch";
    public const string CompatibilityAllocate = "compatibility.allocate";

    public const string IssueCreate = "issue.create";
    public const string IssueOverride = "issue.override";
    public const string IssueReturn = "issue.return";
    public const string IssueEmergencyRelease = "issue.emergency-release";
    public const string TransfusionDocument = "transfusion.document";
    public const string TransfusionStart = "transfusion.start";
    public const string TransfusionComplete = "transfusion.complete";
    public const string OverrideApprove = "override.approve";

    public const string PrintLabel = "print.label";
    public const string PrintReprint = "print.reprint";

    public const string BillingReview = "billing.review";
    public const string BillingCancel = "billing.cancel";
    public const string BillingExport = "billing.export";

    public const string Hl7Manage = "hl7.manage";

    public const string AuditRead = "audit.read";
    public const string LookbackManage = "lookback.manage";
    public const string ReactionInvestigate = "reaction.investigate";
    public const string DeviationManage = "deviation.manage";

    // --- Administration / configuration ---

    /// <summary>View any admin configuration area (read-only).</summary>
    public const string AdminConfigView = "admin.config.view";

    /// <summary>Create/edit/clone/save-draft configuration records.</summary>
    public const string AdminConfigEdit = "admin.config.edit";

    /// <summary>Activate/deactivate configuration records (validation-gated).</summary>
    public const string AdminConfigActivate = "admin.config.activate";

    public const string AdminTestsManage = "admin.tests.manage";
    public const string AdminProductsManage = "admin.products.manage";
    public const string AdminModificationRulesManage = "admin.modification-rules.manage";
    public const string AdminHl7Manage = "admin.hl7.manage";
    public const string AdminUsersManage = "admin.users.manage";
    public const string AdminRolesManage = "admin.roles.manage";

    /// <summary>Review configuration change history / version comparisons.</summary>
    public const string AdminAuditReview = "admin.audit.review";

    /// <summary>Every defined permission code, used for seeding the permission table.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        PatientWrite, PatientMerge,
        SpecimenAccession, SpecimenReject, SpecimenEdit,
        ResultEnter, ResultVerify, ResultCorrect, ResultInvalidate,
        ImmunoRecord, ImmunoOverride,
        InventoryReceive, InventoryTransfer, InventoryRelease, InventoryDiscard,
        InventoryRecall, InventoryCorrectIdentity, InventoryModify,
        CompatibilityCrossmatch, CompatibilityAllocate,
        IssueCreate, IssueOverride, IssueReturn, IssueEmergencyRelease,
        TransfusionDocument, TransfusionStart, TransfusionComplete, OverrideApprove,
        PrintLabel, PrintReprint,
        BillingReview, BillingCancel, BillingExport,
        Hl7Manage,
        AuditRead, LookbackManage, ReactionInvestigate, DeviationManage,
        AdminConfigView, AdminConfigEdit, AdminConfigActivate,
        AdminTestsManage, AdminProductsManage, AdminModificationRulesManage, AdminHl7Manage,
        AdminUsersManage, AdminRolesManage, AdminAuditReview
    };

    /// <summary>The administration permission set, granted to admin-capable roles.</summary>
    public static readonly IReadOnlyList<string> AdminAll = new[]
    {
        AdminConfigView, AdminConfigEdit, AdminConfigActivate,
        AdminTestsManage, AdminProductsManage, AdminModificationRulesManage, AdminHl7Manage,
        AdminUsersManage, AdminRolesManage, AdminAuditReview
    };
}
