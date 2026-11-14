using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// How issue and bedside forms start their two patient-identifier fields (OCD-039), and the live hint shown while
/// they are entered. The hint uses <see cref="PatientIdentityMatchRule.Matches"/>, so it agrees with the issue
/// hard stop, and it never shows the expected value.
/// </summary>
public static class PatientIdentifierEntryRule
{
    public enum Hint
    {
        Empty = 0,
        Matches = 1,
        DoesNotMatch = 2
    }

    /// <summary>
    /// Values to start the MRN and date-of-birth fields with. Empty unless the facility allows pre-fill, so by
    /// default the check compares what staff read from the wristband or requisition with the record.
    /// </summary>
    public static (string? Mrn, string? DateOfBirth) Prefill(bool facilityAllowsPrefill, string? medicalRecordNumber, DateOnly? dateOfBirth) =>
        facilityAllowsPrefill
            ? (medicalRecordNumber, dateOfBirth?.ToString("yyyy-MM-dd"))
            : (null, null);

    public static Hint Check(
        IdentityTokenType type,
        string? entered,
        string medicalRecordNumber,
        DateOnly dateOfBirth,
        string lastName,
        string firstName)
    {
        if (string.IsNullOrWhiteSpace(entered))
        {
            return Hint.Empty;
        }

        return PatientIdentityMatchRule.Matches(medicalRecordNumber, dateOfBirth, lastName, firstName, new PatientIdentityMatchRule.IdentityToken(type, entered))
            ? Hint.Matches
            : Hint.DoesNotMatch;
    }
}
