namespace BloodBankLIS.Domain.Rules;

/// <summary>Changing which Test Group a reaction workup specimen gets is an admin configuration edit.</summary>
public static class ReactionWorkupSettingsAuthorizationRule
{
    public const string UpdateCode = "RXNSET-UPD-PERM";

    public static RuleResult EvaluateUpdate(bool hasAdminConfigEdit) =>
        hasAdminConfigEdit
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating the reaction workup Test Groups requires the admin.config.edit permission.");
}
