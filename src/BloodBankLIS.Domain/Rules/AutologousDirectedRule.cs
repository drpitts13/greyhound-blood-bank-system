using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// SoftBank/SafeTrace autologous and directed units are reserved to one recipient.
/// Autologous units cannot be issued to anyone else. Directed units may be
/// supervisor-converted to allogeneic inventory (<see cref="ConvertCode"/>).
/// </summary>
public static class AutologousDirectedRule
{
    public const string ReceiveCode = "INV-AUTO-DIR";
    public const string IssueCode = "ISS-AUTO-DIR";
    public const string ConvertCode = "INV-DIR-ALLO";

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

    public static bool IsConvertibleStatus(UnitStatus status) => status is
        UnitStatus.Expected or UnitStatus.Received or UnitStatus.Quarantine
        or UnitStatus.Available or UnitStatus.OnHold;

    /// <summary>
    /// AABB/SoftBank: unused directed units may enter volunteer inventory with
    /// supervisor dual control. Autologous units cannot; reserved/issued units
    /// must be released first.
    /// </summary>
    public static RuleResult EvaluateConvert(DonationRestriction restriction, UnitStatus status)
    {
        if (restriction == DonationRestriction.Autologous)
        {
            return RuleResult.HardStop(
                ConvertCode,
                "Autologous units cannot be released to allogeneic inventory.");
        }

        if (restriction != DonationRestriction.Directed)
        {
            return RuleResult.HardStop(
                ConvertCode,
                "Only a directed unit can be converted to allogeneic inventory.");
        }

        if (status is UnitStatus.Allocated or UnitStatus.Assigned
            or UnitStatus.Crossmatched or UnitStatus.Selected)
        {
            return RuleResult.HardStop(
                ConvertCode,
                "Release the patient reservation before converting this directed unit to allogeneic inventory.");
        }

        if (!IsConvertibleStatus(status))
        {
            return RuleResult.HardStop(
                ConvertCode,
                $"A directed unit with status {status} cannot be converted to allogeneic inventory.");
        }

        return RuleResult.Pass(ConvertCode);
    }
}
