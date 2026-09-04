namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for specimen accession, metadata edit, and rejection.
/// Accession binds a specimen to a patient used at issue.
/// </summary>
public static class SpecimenAuthorizationRule
{
    public const string AccessionCode = "SPEC-ACC-PERM";
    public const string EditCode = "SPEC-EDIT-PERM";
    public const string RejectCode = "SPEC-REJ-PERM";

    public static RuleResult EvaluateAccession(bool hasSpecimenAccession) =>
        hasSpecimenAccession
            ? RuleResult.Pass(AccessionCode)
            : RuleResult.HardStop(
                AccessionCode,
                "Accessioning a specimen requires the specimen.accession permission.");

    public static RuleResult EvaluateEdit(bool hasSpecimenEdit) =>
        hasSpecimenEdit
            ? RuleResult.Pass(EditCode)
            : RuleResult.HardStop(
                EditCode,
                "Editing specimen collection metadata requires the specimen.edit permission.");

    public static RuleResult EvaluateReject(bool hasSpecimenReject) =>
        hasSpecimenReject
            ? RuleResult.Pass(RejectCode)
            : RuleResult.HardStop(
                RejectCode,
                "Rejecting a specimen requires the specimen.reject permission.");
}
