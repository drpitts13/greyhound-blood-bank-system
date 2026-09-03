using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace autologous and directed units are reserved to one recipient.
/// Autologous units cannot be issued to anyone else; directed units are similarly
/// locked in this slice (allogeneic conversion is a later supervisor path).
/// </summary>
public static class AutologousDirectedRule
{
    public const string ReceiveCode = "INV-AUTO-DIR";
    public const string IssueCode = "ISS-AUTO-DIR";

    public static bool RequiresRecipient(DonationRestriction restriction) =>
        restriction is DonationRestriction.Autologous or DonationRestriction.Directed;

    public static RuleResult EvaluateReceive(DonationRestriction restriction, long? reservedPatientId)
    {
        if (!RequiresRecipient(restriction))
        {
            return RuleResult.Pass(ReceiveCode);
        }

        if (reservedPatientId is null or <= 0)
        {
            return RuleResult.HardStop(
                ReceiveCode,
                $"Designate the intended recipient before receiving this {restriction.ToString().ToLowerInvariant()} unit.");
        }

        return RuleResult.Pass(ReceiveCode);
    }

    public static RuleResult EvaluateIssue(DonationRestriction restriction, long? reservedPatientId, long issuePatientId)
    {
        if (!RequiresRecipient(restriction))
        {
            return RuleResult.Pass(IssueCode);
        }

        if (reservedPatientId is null or <= 0)
        {
            return RuleResult.HardStop(
                IssueCode,
                $"This {restriction.ToString().ToLowerInvariant()} unit has no intended recipient on record.");
        }

        if (reservedPatientId.Value != issuePatientId)
        {
            return RuleResult.HardStop(
                IssueCode,
                $"This {restriction.ToString().ToLowerInvariant()} unit is reserved for another patient.");
        }

        return RuleResult.Pass(IssueCode);
    }
}
