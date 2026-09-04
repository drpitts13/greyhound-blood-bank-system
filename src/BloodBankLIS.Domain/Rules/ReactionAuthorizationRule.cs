namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gate for updating a transfusion-reaction investigation.
/// Opening from a suspected transfusion remains an automatic issue-path write.
/// </summary>
public static class ReactionAuthorizationRule
{
    public const string InvestigateCode = "RXN-PERM";

    public static RuleResult EvaluateInvestigate(bool hasReactionInvestigate) =>
        hasReactionInvestigate
            ? RuleResult.Pass(InvestigateCode)
            : RuleResult.HardStop(
                InvestigateCode,
                "Updating a reaction investigation requires the reaction.investigate permission.");
}
