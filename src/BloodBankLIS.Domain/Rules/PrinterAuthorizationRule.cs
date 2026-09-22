namespace BloodBankLIS.Domain.Rules;

/// <summary>Privilege gates for PDF and Zebra printer catalog changes.</summary>
public static class PrinterAuthorizationRule
{
    public const string CreateCode = "PRINTER-CREATE-PERM";
    public const string UpdateCode = "PRINTER-UPD-PERM";
    public const string ActivateCode = "PRINTER-ACT-PERM";
    public const string DeactivateCode = "PRINTER-DEACT-PERM";

    public static RuleResult EvaluateCreate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(CreateCode, "Creating a printer requires the admin.config.edit permission.");

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(UpdateCode, "Updating a printer requires the admin.config.edit permission.");

    public static RuleResult EvaluateActivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(ActivateCode)
            : RuleResult.HardStop(ActivateCode, "Activating a printer requires the admin.config.activate permission.");

    public static RuleResult EvaluateDeactivate(bool hasAdminConfigActivate) =>
        hasAdminConfigActivate
            ? RuleResult.Pass(DeactivateCode)
            : RuleResult.HardStop(DeactivateCode, "Deactivating a printer requires the admin.config.activate permission.");
}
