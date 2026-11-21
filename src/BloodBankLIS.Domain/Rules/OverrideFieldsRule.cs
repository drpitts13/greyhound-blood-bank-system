namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-041: an override needs a reason and the name of whoever authorized it. Nothing fills the authorizer for the
/// user; when an override was started (one field given) this says plainly which field is missing. It is a warning,
/// never a hard stop, so screens that show the override form for warnings keep showing it. With both fields blank
/// nothing is added: that is the normal first submit that reveals the override is needed.
/// </summary>
public static class OverrideFieldsRule
{
    public const string Code = "OVR-FIELDS";

    public static RuleResult? Missing(string? overrideReason, string? authorizedBy)
    {
        var reasonMissing = string.IsNullOrWhiteSpace(overrideReason);
        var authorizerMissing = string.IsNullOrWhiteSpace(authorizedBy);
        if (reasonMissing == authorizerMissing)
        {
            return null;
        }

        return RuleResult.Warning(Code, reasonMissing
            ? "To override, give the reason."
            : "To override, name who authorized it (the physician or supervisor, or yourself).");
    }

    public static RuleEvaluation WithMissing(RuleEvaluation evaluation, string? overrideReason, string? authorizedBy) =>
        Missing(overrideReason, authorizedBy) is { } missing
            ? new RuleEvaluation(evaluation.Results.Append(missing).ToList())
            : evaluation;
}
