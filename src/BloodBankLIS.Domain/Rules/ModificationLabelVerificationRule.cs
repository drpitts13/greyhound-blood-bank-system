namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A modification result stays in quarantine until the unit number on the new
/// label matches the result. Other quarantine releases are not asked for that scan.
/// </summary>
public static class ModificationLabelVerificationRule
{
    public const string Code = "MOD-LABEL-SCAN";

    public static RuleResult Evaluate(bool isModificationResult, string storedUnitNumber, string? scannedUnitNumber)
    {
        if (!isModificationResult)
        {
            return RuleResult.Pass(Code);
        }

        var scanned = scannedUnitNumber?.Trim() ?? string.Empty;
        if (scanned.Length == 0)
        {
            return RuleResult.HardStop(
                Code,
                "Scan the unit number on the new label before releasing this modification result.");
        }

        if (!string.Equals(storedUnitNumber.Trim(), scanned, StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(
                Code,
                "The scanned unit number does not match this modification result.");
        }

        return RuleResult.Pass(Code);
    }
}
