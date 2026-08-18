using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// AABB two-independent-identifier check: two distinct token types whose values
/// match the patient record (MRN, DOB, full name).
/// </summary>
public static class PatientIdentityMatchRule
{
    public const string Code = IssueGate.IdentityCode;

    public sealed record IdentityToken(IdentityTokenType Type, string Value);

    public static RuleResult Evaluate(
        string medicalRecordNumber,
        DateOnly dateOfBirth,
        string lastName,
        string firstName,
        IdentityToken? first,
        IdentityToken? second)
    {
        if (first is null || second is null)
        {
            return RuleResult.HardStop(Code, "Two independent patient identifiers are required.");
        }

        if (first.Type == second.Type)
        {
            return RuleResult.HardStop(Code, "The two patient identifiers must be of different types.");
        }

        if (!Matches(medicalRecordNumber, dateOfBirth, lastName, firstName, first)
            || !Matches(medicalRecordNumber, dateOfBirth, lastName, firstName, second))
        {
            return RuleResult.HardStop(Code, "Entered identifiers do not match the patient record.");
        }

        return RuleResult.Pass(Code);
    }

    public static bool Matches(
        string medicalRecordNumber,
        DateOnly dateOfBirth,
        string lastName,
        string firstName,
        IdentityToken token)
    {
        var value = token.Value.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return token.Type switch
        {
            IdentityTokenType.MedicalRecordNumber =>
                string.Equals(value, medicalRecordNumber, StringComparison.OrdinalIgnoreCase),
            IdentityTokenType.DateOfBirth =>
                DateOnly.TryParse(value, out var dob) && dob == dateOfBirth,
            IdentityTokenType.FullName =>
                string.Equals(NormalizeName(value), NormalizeName($"{lastName}, {firstName}"), StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeName(value), NormalizeName($"{firstName} {lastName}"), StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string NormalizeName(string value) =>
        string.Join(' ', value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
