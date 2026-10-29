using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Specimen barcode relabel is a controlled identity change. Accession number stays
/// immutable. Rejected, cancelled, and expired tubes cannot be relabeled.
/// </summary>
public static class SpecimenRelabelRule
{
    public const string StatusCode = "SPEC-RELABEL-STATUS";
    public const string BarcodeCode = "SPEC-RELABEL-BARCODE";
    public const string ReasonCode = "SPEC-RELABEL-REASON";
    public const string UnchangedCode = "SPEC-RELABEL-SAME";

    public static RuleResult EvaluateStatus(SpecimenStatus status) =>
        status is SpecimenStatus.Collected or SpecimenStatus.Received or SpecimenStatus.Accepted
            ? RuleResult.Pass(StatusCode)
            : RuleResult.HardStop(
                StatusCode,
                $"A specimen with status {status} cannot be relabeled.");

    public static RuleResult EvaluateBarcode(string? barcode) =>
        string.IsNullOrWhiteSpace(barcode)
            ? RuleResult.HardStop(BarcodeCode, "A barcode is required to relabel a specimen.")
            : RuleResult.Pass(BarcodeCode);

    public static RuleResult EvaluateReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? RuleResult.HardStop(ReasonCode, "A reason is required to relabel a specimen.")
            : RuleResult.Pass(ReasonCode);

    public static RuleResult EvaluateChange(string? currentBarcode, string nextBarcode)
    {
        var current = string.IsNullOrWhiteSpace(currentBarcode) ? null : currentBarcode.Trim();
        var next = nextBarcode.Trim();
        return string.Equals(current, next, StringComparison.Ordinal)
            ? RuleResult.HardStop(UnchangedCode, "The new barcode must differ from the current barcode.")
            : RuleResult.Pass(UnchangedCode);
    }
}
