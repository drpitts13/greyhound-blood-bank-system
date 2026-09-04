using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Patients;

public sealed record CreatePatientRequest(
    string MedicalRecordNumber,
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly DateOfBirth,
    Sex Sex);

public sealed record UpdatePatientRequest(
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly DateOfBirth,
    Sex Sex,
    PatientStatus Status,
    DateTime? RecentPregnancyUtc = null);

public sealed record MergePatientsRequest(string DuplicateMrn, string Reason);

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
    long? MergedIntoPatientId = null)
{
    public static PatientDto From(Patient p) => new(
        p.Id, p.MedicalRecordNumber, p.LastName, p.FirstName, p.MiddleName,
        p.DateOfBirth, p.Sex, p.Status, p.CreatedUtc, p.CreatedBy, p.RecentPregnancyUtc, p.MergedIntoPatientId);
}
