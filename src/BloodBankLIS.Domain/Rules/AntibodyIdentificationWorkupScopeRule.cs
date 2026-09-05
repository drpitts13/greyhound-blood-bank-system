using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Keeps antibody-identification workups specimen-scoped when possible and
/// prevents two open workups from claiming the same identification-of-record.
/// This does not identify antibodies.
/// </summary>
public static class AntibodyIdentificationWorkupScopeRule
{
    public const string UnscopedCode = "ABID-WORKUP-UNSCOPED";
    public const string OverlappingOpenCode = "ABID-WORKUP-DUP-OPEN";
    public const string SpecimenLinkCode = "ABID-WORKUP-SPECIMEN";
    public const string SpecimenUnusableCode = "ABID-WORKUP-SPEC-UNUSABLE";
    public const string SpecimenExpiredCode = "ABID-WORKUP-SPEC-EXPIRED";
    public const string SpecimenNotReadyCode = "ABID-WORKUP-SPEC-NOT-READY";
    public const string SpecimenUnacceptedCode = "ABID-WORKUP-SPEC-UNACCEPTED";

    public static RuleResult EvaluateSpecimenScope(bool hasSpecimen) =>
        hasSpecimen
            ? RuleResult.Pass(UnscopedCode)
            : RuleResult.Warning(
                UnscopedCode,
                "This antibody-identification workup is not linked to a specimen. It is the identification of record for the whole patient and will HardStop free-text ABID verify until completed or voided. Link a specimen on this workup to narrow the identification of record.");

    public static RuleResult EvaluateOverlappingOpen(
        bool creatingUnscoped,
        bool hasOpenUnscoped,
        bool hasOpenOnSameSpecimen,
        bool hasAnyOpen)
    {
        if (hasOpenUnscoped)
        {
            return RuleResult.HardStop(
                OverlappingOpenCode,
                "An unscoped open antibody-identification workup already exists for this patient. Complete or void it before opening another.");
        }

        if (creatingUnscoped && hasAnyOpen)
        {
            return RuleResult.HardStop(
                OverlappingOpenCode,
                "An open antibody-identification workup already exists. An unscoped workup cannot be added while another workup is open.");
        }

        if (hasOpenOnSameSpecimen)
        {
            return RuleResult.HardStop(
                OverlappingOpenCode,
                "An open antibody-identification workup already exists for this specimen. Complete or void it before opening another.");
        }

        return RuleResult.Pass(OverlappingOpenCode);
    }

    public static RuleResult EvaluateCanLinkSpecimen(AntibodyWorkupStatus status) =>
        status is AntibodyWorkupStatus.Completed or AntibodyWorkupStatus.Voided
            ? RuleResult.HardStop(
                SpecimenLinkCode,
                "A completed or voided antibody-identification workup cannot change specimen scope.")
            : RuleResult.Pass(SpecimenLinkCode);

    public static RuleResult EvaluateSpecimenUsable(SpecimenStatus status, bool completing)
    {
        if (status is SpecimenStatus.Rejected or SpecimenStatus.Cancelled)
        {
            return RuleResult.HardStop(
                SpecimenUnusableCode,
                "A rejected or cancelled specimen cannot be the identification-of-record scope for an antibody-identification workup.");
        }

        if (status == SpecimenStatus.Expired)
        {
            return completing
                ? RuleResult.Warning(
                    SpecimenExpiredCode,
                    "The linked specimen is expired. Completing still posts only technologist-Identified antibodies. Confirm this specimen remains the identification of record.")
                : RuleResult.HardStop(
                    SpecimenUnusableCode,
                    "An expired specimen cannot be linked as the identification-of-record scope for an antibody-identification workup.");
        }

        return RuleResult.Pass(SpecimenUnusableCode);
    }

    public static RuleResult EvaluateSpecimenExpiration(DateTime? expiresUtc, DateTime nowUtc, bool completing)
    {
        if (expiresUtc is DateTime expired && nowUtc >= expired)
        {
            return completing
                ? RuleResult.Warning(
                    SpecimenExpiredCode,
                    "The linked specimen expiration has passed. Completing still posts only technologist-Identified antibodies. Confirm this specimen remains the identification of record.")
                : RuleResult.HardStop(
                    SpecimenExpiredCode,
                    "An expired specimen cannot be the identification-of-record scope for a new or relinked antibody-identification workup.");
        }

        return RuleResult.Pass(SpecimenExpiredCode);
    }

    /// <summary>
    /// Collected specimens have not entered the lab. Received specimens are
    /// in the lab but not yet accepted. This does not identify antibodies.
    /// </summary>
    public static RuleResult EvaluateSpecimenReadiness(SpecimenStatus status, bool completing)
    {
        if (status == SpecimenStatus.Collected)
        {
            return RuleResult.HardStop(
                SpecimenNotReadyCode,
                "A collected specimen that has not been received cannot be the identification-of-record scope for an antibody-identification workup.");
        }

        if (status == SpecimenStatus.Received)
        {
            return RuleResult.Warning(
                SpecimenUnacceptedCode,
                completing
                    ? "The linked specimen is received but not accepted. Completing still posts only technologist-Identified antibodies. Confirm this specimen remains the identification of record."
                    : "The specimen is received but not accepted. It can be linked as identification-of-record scope; accept the specimen before treating it as fully released for testing.");
        }

        return RuleResult.Pass(SpecimenUnacceptedCode);
    }
}
