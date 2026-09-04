namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for printing specimen, compatibility, and component labels
/// and for reprinting a stored print job.
/// </summary>
public static class PrintAuthorizationRule
{
    public const string LabelCode = "PRT-LABEL-PERM";
    public const string ReprintCode = "PRT-REPRINT-PERM";

    public static RuleResult EvaluateLabel(bool hasPrintLabel) =>
        hasPrintLabel
            ? RuleResult.Pass(LabelCode)
            : RuleResult.HardStop(
                LabelCode,
                "Printing a label requires the print.label permission.");

    public static RuleResult EvaluateReprint(bool hasPrintReprint) =>
        hasPrintReprint
            ? RuleResult.Pass(ReprintCode)
            : RuleResult.HardStop(
                ReprintCode,
                "Reprinting a label requires the print.reprint permission.");
}
