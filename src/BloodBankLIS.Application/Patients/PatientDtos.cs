using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Patients;

public sealed record CreatePatientRequest(
    string MedicalRecordNumber,
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly DateOfBirth,
    Sex Sex,
    string? Comment = null);

public sealed record UpdatePatientRequest(
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly DateOfBirth,
    Sex Sex,
    PatientStatus Status,
    DateTime? RecentPregnancyUtc = null,
    string? Comment = null,
    string? NameChangeReason = null);

public sealed record MergePatientsRequest(string DuplicateMrn, string Reason);

public sealed record UnmergePatientRequest(string Reason);

public sealed record PatientAliasDto(
    long Id,
    IdentityTokenType IdentifierType,
    string Value,
    bool IsActive)
{
    public static PatientAliasDto From(PatientIdentifier i) =>
        new(i.Id, i.IdentifierType, i.Value, i.IsActive);

    public string KindLabel => IdentifierType switch
    {
        IdentityTokenType.PriorFullName => "Prior name",
        IdentityTokenType.PriorMedicalRecordNumber => "Prior MRN",
        _ => IdentifierType.ToString()
    };
}

public sealed record PatientDto(
    long Id,
    string MedicalRecordNumber,
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly DateOfBirth,
    Sex Sex,
    PatientStatus Status,
    DateTime CreatedUtc,
    string CreatedBy,
    DateTime? RecentPregnancyUtc = null,
    long? MergedIntoPatientId = null,
    string? Comment = null,
    IReadOnlyList<PatientAliasDto>? Aliases = null)
{
    public static PatientDto From(Patient p, IReadOnlyList<PatientAliasDto>? aliases = null) => new(
        p.Id, p.MedicalRecordNumber, p.LastName, p.FirstName, p.MiddleName,
        p.DateOfBirth, p.Sex, p.Status, p.CreatedUtc, p.CreatedBy, p.RecentPregnancyUtc, p.MergedIntoPatientId,
        p.Comment, aliases);

    public bool MatchesSearch(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        if (MedicalRecordNumber.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || LastName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || FirstName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(MiddleName)
                && MiddleName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return Aliases?.Any(a => a.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)) == true;
    }
}
