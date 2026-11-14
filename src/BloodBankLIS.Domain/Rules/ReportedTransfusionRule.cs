namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Cycle 703 (OCD-083 option B): a unit the EHR reported transfused while the blood bank had it recalled, quarantined,
/// damaged or missing keeps that status. No inventory action may change it (a release, locate or inspection would put a
/// transfused unit back in stock); the transfusion is reviewed on the supervisor queue instead.
/// </summary>
public static class ReportedTransfusionRule
{
    public const string Code = "INV-REPORTED-TRANSFUSED";

    public static RuleResult Evaluate(DateTime? transfusionReportedUtc, string unitNumber) =>
        transfusionReportedUtc is null
            ? RuleResult.Pass(Code)
            : RuleResult.HardStop(Code,
                $"{unitNumber} was reported transfused by the EHR (OCD-083); its status is kept and it is reviewed on the supervisor queue, not released, located or discarded.");
}
