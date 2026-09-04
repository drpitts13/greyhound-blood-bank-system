using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Patients;

/// <summary>
/// Walks <see cref="Patient.MergedIntoPatientId"/> so interface and lookup paths
/// use the surviving record. Does not create or apply a merge.
/// </summary>
public static class PatientMergeFollow
{
    public static async Task<Patient?> ResolveClinicalRecordAsync(
        IRepository<Patient> patients,
        Patient? patient,
        CancellationToken ct = default)
    {
        if (patient is null)
        {
            return null;
        }

        var seen = new HashSet<long>();
        while (patient.Status == PatientStatus.Merged
               && patient.MergedIntoPatientId is long survivorId
               && seen.Add(patient.Id))
        {
            var survivor = await patients.GetByIdAsync(survivorId, ct);
            if (survivor is null)
            {
                break;
            }

            patient = survivor;
        }

        return patient;
    }
}
