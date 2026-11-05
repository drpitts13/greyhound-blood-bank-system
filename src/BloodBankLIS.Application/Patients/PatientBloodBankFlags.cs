using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Patients;

/// <summary>
/// The blood bank facts shown wherever a patient is picked (patient list, top-bar lookup): current ABO/Rh, active
/// antibodies, and how many special requirements are in effect. One definition, so the two places agree.
/// </summary>
public static class PatientBloodBankFlags
{
    /// <summary>The current, not invalidated, type ("O+"), or null when none is on file.</summary>
    public static string? CurrentBloodType(IEnumerable<PatientBloodTypeHistory> rows) =>
        rows.Where(t => t.IsCurrent && t.InvalidatedUtc is null)
            .OrderByDescending(t => t.Id)
            .Select(t => t.BloodType.ToString())
            .FirstOrDefault();

    /// <summary>Active antibody specificities, each once, sorted.</summary>
    public static IReadOnlyList<string> ActiveAntibodies(IEnumerable<AntibodyHistory> rows) =>
        rows.Where(a => a.IsActive && !string.IsNullOrWhiteSpace(a.AntibodySpecificity))
            .Select(a => a.AntibodySpecificity)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Special requirements in effect now: active, started, not ended.</summary>
    public static int RequirementsInEffect(IEnumerable<SpecialTransfusionRequirement> rows, DateTime nowUtc) =>
        rows.Count(r => SpecialRequirementCatalog.IsClinicallyActive(r.IsActive, r.EffectiveUtc, r.ExpiresUtc, nowUtc));
}
