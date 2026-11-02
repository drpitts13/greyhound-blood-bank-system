using BloodBankLIS.Domain.Isbt128;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Receiving by scanning the tube in hand: the scan must be the specimen's accession number or its label barcode,
/// so the tube received is the one on the worklist, not the row someone clicked.
/// </summary>
public static class SpecimenReceiptScanRule
{
    public const string Code = "SPEC-RECEIPT-SCAN";

    /// <summary>Scanner noise (control characters, prefixes) removed, trimmed, upper case. Empty when nothing usable.</summary>
    public static string Normalize(string? scan) =>
        ScannerInputSanitizer.Sanitize(scan).Sanitized.Trim().ToUpperInvariant();

    public static bool Matches(string normalizedScan, string accessionNumber, string? barcode) =>
        normalizedScan.Length > 0
        && (string.Equals(normalizedScan, accessionNumber.Trim(), StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(barcode) && string.Equals(normalizedScan, barcode.Trim(), StringComparison.OrdinalIgnoreCase)));

    public static RuleResult NotFound(string normalizedScan) =>
        RuleResult.HardStop(Code, normalizedScan.Length == 0
            ? "Scan the tube's accession or label barcode."
            : $"No specimen has accession or label {normalizedScan}. Check the tube and its label.");

    public static RuleResult Ambiguous(string normalizedScan) =>
        RuleResult.HardStop(Code, $"More than one specimen matches {normalizedScan}. Receive it from its row and check the label.");
}
