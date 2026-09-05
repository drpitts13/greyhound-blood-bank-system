namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gate for facility-policy mutations that change electronic XM,
/// second ABO, uncrossmatched O, specimen windows, and other facility switches.
/// </summary>
public static class FacilityPolicyAuthorizationRule
{
    public const string UpdateCode = "FACPOL-UPD-PERM";

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a facility policy requires the admin.config.edit permission.");
}
