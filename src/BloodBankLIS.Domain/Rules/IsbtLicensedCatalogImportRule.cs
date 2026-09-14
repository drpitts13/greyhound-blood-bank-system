namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Licensed ISBT catalog import (OCD-004). The engine does not invent ICCBBA
/// product or ABO/RhD codes. The facility must supply tables from a license.
/// </summary>
public static class IsbtLicensedCatalogImportRule
{
    public const string PermissionCode = "ISBT-IMPORT-PERM";
    public const string LicenseCode = "ISBT-IMPORT-LICENSE";
    public const string VersionCode = "ISBT-IMPORT-VERSION";
    public const string EmptyCode = "ISBT-IMPORT-EMPTY";

    public static RuleResult EvaluatePermission(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(PermissionCode)
            : RuleResult.HardStop(PermissionCode, "Importing a licensed ISBT catalog requires admin.config.edit.");

    public static RuleResult Evaluate(
        bool licenseAcknowledged,
        string? standardVersion,
        int productRowCount,
        int aboRhdRowCount)
    {
        if (!licenseAcknowledged)
        {
            return RuleResult.HardStop(
                LicenseCode,
                "Import requires acknowledgment that the payload is from a current ICCBBA license. Codes are not invented by this software.");
        }

        if (string.IsNullOrWhiteSpace(standardVersion)
            || standardVersion.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
            || standardVersion.Contains("PENDING-ICCBBA", StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.HardStop(
                VersionCode,
                "StandardVersion must identify a licensed ICCBBA extract. Placeholder or pending-ICCBBA versions are not accepted.");
        }

        if (productRowCount <= 0 && aboRhdRowCount <= 0)
        {
            return RuleResult.HardStop(
                EmptyCode,
                "Import must include at least one product description code or ABO/RhD code supplied by the licensee.");
        }

        return RuleResult.Pass(LicenseCode);
    }
}
