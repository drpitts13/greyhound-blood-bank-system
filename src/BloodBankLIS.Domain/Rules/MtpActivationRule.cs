using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Massive transfusion protocol activation. A protocol is called by a named clinician for a reason, recorded once per
/// patient, and every massive-transfusion issue belongs to it. This is workflow structure, not a clinical rule: pack
/// contents and ratios stay facility policy. Each unit still passes the full issue gate.
/// </summary>
public static class MtpActivationRule
{
    public const string ActivateCode = "MTP-ACTIVATE";
    public const string AlreadyActiveCode = "MTP-ALREADY-ACTIVE";
    public const string CloseCode = "MTP-CLOSE";
    public const string NotActiveCode = "MTP-NOT-ACTIVE";

    public static RuleEvaluation EvaluateActivate(string? requestedBy, string? reason, bool alreadyActive, PatientStatus patientStatus)
    {
        var results = new List<RuleResult>
        {
            string.IsNullOrWhiteSpace(requestedBy)
                ? RuleResult.HardStop(ActivateCode, "Name the clinician who called the massive transfusion protocol.")
                : RuleResult.Pass(ActivateCode),
            string.IsNullOrWhiteSpace(reason)
                ? RuleResult.HardStop(ActivateCode, "Give the reason the massive transfusion protocol was called.")
                : RuleResult.Pass(ActivateCode),
            alreadyActive
                ? RuleResult.HardStop(AlreadyActiveCode, "A massive transfusion protocol is already active for this patient. Issue under it, or close it first.")
                : RuleResult.Pass(AlreadyActiveCode),
            PatientMergeRule.EvaluateClinicalUse(patientStatus)
        };
        return new RuleEvaluation(results);
    }

    public static RuleEvaluation EvaluateClose(string? reason, MtpActivationStatus status) =>
        new([
            string.IsNullOrWhiteSpace(reason)
                ? RuleResult.HardStop(CloseCode, "Give the reason the massive transfusion protocol ended.")
                : RuleResult.Pass(CloseCode),
            status == MtpActivationStatus.Active
                ? RuleResult.Pass(CloseCode)
                : RuleResult.HardStop(CloseCode, "This massive transfusion protocol is already closed.")
        ]);

    /// <summary>A massive-transfusion issue needs an active activation for the patient; other issue types do not.</summary>
    public static RuleResult EvaluateIssue(IssueType issueType, bool hasActiveActivation) =>
        issueType != IssueType.MassiveTransfusion || hasActiveActivation
            ? RuleResult.Pass(NotActiveCode)
            : RuleResult.HardStop(NotActiveCode,
                "No massive transfusion protocol is active for this patient. Activate it first (who called it and why), or use Emergency release for a single uncrossmatched unit.");
}
