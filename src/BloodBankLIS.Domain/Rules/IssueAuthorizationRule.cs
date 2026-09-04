using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for emergency/MTP issue and warning overrides.
/// Distinct from <see cref="IssueGate"/> clinical checks.
/// </summary>
public static class IssueAuthorizationRule
{
    public const string EmergencyCode = "ISS-EMERG-PERM";
    public const string OverrideCode = "ISS-OVR-PERM";

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
}
