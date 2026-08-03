using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// Auto-detects scanner-formatted ISBT vs human-readable entry.
/// UI may display the mode; do not rely solely on a user toggle.
/// </summary>
public static class IsbtInputTypeDetector
{
    public static IsbtInputMode Detect(string? value, ScannerInputSanitizer.Options? options = null)
    {
        var sanitized = ScannerInputSanitizer.Sanitize(value, options).Sanitized;
        return IsbtDataStructureRegistry.StartsWithSupportedIdentifier(sanitized)
            ? IsbtInputMode.ScannedIsbt
            : IsbtInputMode.HumanReadable;
    }
}
