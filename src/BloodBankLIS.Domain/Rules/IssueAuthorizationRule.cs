using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for issue.create, emergency/MTP issue, warning overrides,
/// issue return, ward receipt, and transfusion documentation.
/// Distinct from <see cref="IssueGate"/> clinical checks.
/// </summary>
public static class IssueAuthorizationRule
{
    public const string CreateCode = "ISS-CREATE-PERM";
    public const string EmergencyCode = "ISS-EMERG-PERM";
    public const string OverrideCode = "ISS-OVR-PERM";
    public const string ReturnCode = "ISS-RET-PERM";
    public const string DocumentTransfusionCode = "TXN-DOC-PERM";
    public const string WardReceiptCode = "TXN-WARD-PERM";
    public const string InterfaceDocumentCode = "TXN-IFACE-PERM";

    public static RuleResult EvaluateCreate(bool hasIssueCreate) =>
        hasIssueCreate
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Issuing a unit requires the issue.create permission.");

    public static RuleResult EvaluateEmergency(IssueType issueType, bool hasEmergencyReleasePermission)
    {
        if (issueType is not (IssueType.EmergencyRelease or IssueType.MassiveTransfusion))
        {
            return RuleResult.Pass(EmergencyCode);
        }

        return hasEmergencyReleasePermission
            ? RuleResult.Pass(EmergencyCode)
            : RuleResult.HardStop(
                EmergencyCode,
                "Emergency or massive-transfusion issue requires the issue.emergency-release permission.");
    }

    public static RuleResult EvaluateOverride(bool requiresOverride, IssueType issueType, bool hasOverridePermission)
    {
        if (!requiresOverride)
        {
            return RuleResult.Pass(OverrideCode);
        }

        if (issueType is IssueType.EmergencyRelease or IssueType.MassiveTransfusion)
        {
            return RuleResult.Pass(OverrideCode);
        }

        return hasOverridePermission
            ? RuleResult.Pass(OverrideCode)
            : RuleResult.HardStop(
                OverrideCode,
                "Overriding an issue warning requires the issue.override permission.");
    }

    public static RuleResult EvaluateReturn(bool hasIssueReturn) =>
        hasIssueReturn
            ? RuleResult.Pass(ReturnCode)
            : RuleResult.HardStop(
                ReturnCode,
                "Returning an issued unit to inventory requires the issue.return permission.");

    public static RuleResult EvaluateDocumentTransfusion(bool hasTransfusionDocument) =>
        hasTransfusionDocument
            ? RuleResult.Pass(DocumentTransfusionCode)
            : RuleResult.HardStop(
                DocumentTransfusionCode,
                "Documenting a transfusion requires the transfusion.document permission.");

    public static RuleResult EvaluateWardReceipt(bool hasTransfusionDocument) =>
        hasTransfusionDocument
            ? RuleResult.Pass(WardReceiptCode)
            : RuleResult.HardStop(
                WardReceiptCode,
                "Recording ward receipt of an issued unit requires the transfusion.document permission.");

    public static RuleResult EvaluateInterfaceDocument(bool hasTransfusionDocument) =>
        hasTransfusionDocument
            ? RuleResult.Pass(InterfaceDocumentCode)
            : RuleResult.HardStop(
                InterfaceDocumentCode,
                "Documenting a transfusion from the interface service requires the transfusion.document permission.");
}
