namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A crossmatch may not be recorded on a specimen that has already expired: it would
/// be out of date the moment it is saved. The one exception (OCD-050, decided
/// 2026-11-02) is the retrospective crossmatch after emergency release on the
/// pre-transfusion specimen, which may be past its expiration: that is a warning the
/// user overrides with a reason and an authorizer, recorded as an exception.
/// A specimen with no expiration keeps the default crossmatch validity.
/// </summary>
public static class CrossmatchSpecimenRule
{
    public const string Code = "XM-SPEC-EXPIRED";

    public const string RetrospectiveCode = "XM-SPEC-EXPIRED-RETRO";

    /// <param name="retrospectiveOnPreTransfusionSpecimen">
    /// The unit is on the retrospective crossmatch worklist for this patient and the
    /// specimen was collected at or before the unit was issued.
    /// </param>
    public static RuleResult Evaluate(
        string? accessionNumber,
        DateTime? specimenExpiresUtc,
        DateTime nowUtc,
        bool retrospectiveOnPreTransfusionSpecimen = false)
    {
        if (specimenExpiresUtc is not DateTime expires || nowUtc < expires)
        {
            return RuleResult.Pass(Code);
        }

        var name = string.IsNullOrWhiteSpace(accessionNumber) ? "The specimen" : $"Specimen {accessionNumber}";
        return retrospectiveOnPreTransfusionSpecimen
            ? RuleResult.Warning(
                RetrospectiveCode,
                $"{name} expired at {expires:yyyy-MM-dd HH:mm} UTC. It is the pre-transfusion specimen, so the retrospective crossmatch may be recorded on it as an exception: give the reason and who authorized it.")
            : RuleResult.HardStop(
                Code,
                $"{name} expired at {expires:yyyy-MM-dd HH:mm} UTC. Collect a new specimen and crossmatch on it.");
    }

    public static bool IsPreTransfusionSpecimen(DateTime specimenCollectedUtc, DateTime issuedUtc) =>
        specimenCollectedUtc <= issuedUtc;
}
