namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Patient label for a unit timeline row. A missing or blank name stays the patient id.
/// </summary>
public static class PatientActivityLabel
{
    public static string Format(long patientId, string? lastName, string? firstName, string? medicalRecordNumber = null)
    {
        var last = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();
        var first = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim();
        var mrn = string.IsNullOrWhiteSpace(medicalRecordNumber) ? null : medicalRecordNumber.Trim();
        var name = last is null && first is null
            ? $"patient #{patientId}"
            : last is null
                ? first!
                : first is null ? last : $"{last}, {first}";
        return mrn is null ? name : $"{name} ({mrn})";
    }
}
