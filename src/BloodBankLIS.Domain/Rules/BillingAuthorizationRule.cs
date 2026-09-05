namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for charge review, cancel, and export after capture.
/// Capture stays ungated so clinical paths can still write pending events.
/// </summary>
public static class BillingAuthorizationRule
{
    public const string ReviewCode = "BILL-REV-PERM";
    public const string CancelCode = "BILL-CXL-PERM";
    public const string ExportCode = "BILL-EXP-PERM";

    public static RuleResult EvaluateReview(bool hasBillingReview) =>
        hasBillingReview
            ? RuleResult.Pass(ReviewCode)
            : RuleResult.HardStop(
                ReviewCode,
                "Reviewing a charge requires the billing.review permission.");

    public static RuleResult EvaluateCancel(bool hasBillingCancel) =>
        hasBillingCancel
            ? RuleResult.Pass(CancelCode)
            : RuleResult.HardStop(
                CancelCode,
                "Cancelling a charge requires the billing.cancel permission.");

    public static RuleResult EvaluateExport(bool hasBillingExport) =>
        hasBillingExport
            ? RuleResult.Pass(ExportCode)
            : RuleResult.HardStop(
                ExportCode,
                "Exporting a charge requires the billing.export permission.");
}
