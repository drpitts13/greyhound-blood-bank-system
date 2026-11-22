namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Whether a product modification needs a typed reason (end-user testing loop 2 tick 54). A reason is required unless
/// the facility turns <see cref="FacilityPolicyKeys.RequireModificationReason"/> off; the record then says none was entered.
/// </summary>
public static class ModificationReasonRule
{
    public const string NotEnteredText = "No reason entered (not required by facility policy).";

    /// <summary>The reason to record, or null when one is required and none was given.</summary>
    public static string? Resolve(string? reason, bool required) =>
        !string.IsNullOrWhiteSpace(reason) ? reason : required ? null : NotEnteredText;
}
