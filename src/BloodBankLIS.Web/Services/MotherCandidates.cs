using BloodBankLIS.Application.Patients;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The patients offered when linking an infant's mother (end-user testing pass 301: the chart asked for the mother's MRN
/// typed from memory, so a mistyped MRN linked or refused with no name to check against). The picker lists female (or
/// sex unknown) patients, not merged, born before the infant, by name; the MRN can be searched. It suggests nothing:
/// the user chooses.
/// </summary>
public static class MotherCandidates
{
    public static IReadOnlyList<PatientDto> From(IEnumerable<PatientDto> patients, long infantId, DateOnly infantBorn) =>
        patients
            .Where(p => p.Id != infantId
                        && (p.Sex is Sex.Female or Sex.Unknown)
                        && p.MergedIntoPatientId is null
                        && p.Status != PatientStatus.Merged
                        && p.DateOfBirth < infantBorn)
            .OrderBy(p => p.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.FirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
