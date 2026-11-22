namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The two patient identifiers recorded at accession are the ones read from the tube label, so the identity check
/// compares the label with the record. Copying them from the record would make the check compare the record with
/// itself. They may be taken from the record only when the facility has chosen to pre-fill identifiers (OCD-039).
/// </summary>
public static class SpecimenLabelIdentityRule
{
    public const string Code = "SPEC-LABEL-IDS";

    public static RuleResult Evaluate(string? identifier1, string? identifier2, bool facilityPrefills) =>
        facilityPrefills || (!string.IsNullOrWhiteSpace(identifier1) && !string.IsNullOrWhiteSpace(identifier2))
            ? RuleResult.Pass(Code)
            : RuleResult.HardStop(Code,
                "Enter both patient identifiers as they appear on the tube label (scan or type them). They are checked against the patient record.");
}
