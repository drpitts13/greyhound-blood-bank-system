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

    /// <summary>
    /// Active antibody specificities, each once, sorted. One known only from another facility's report (every active
    /// record of it names a reporting facility) says so, as the downtime sheet does: "anti-Jka (reported by St. Mary's)".
    /// Found here as well, it reads plain.
    /// </summary>
    public static IReadOnlyList<string> ActiveAntibodies(IEnumerable<AntibodyHistory> rows) =>
        rows.Where(a => a.IsActive && !string.IsNullOrWhiteSpace(a.AntibodySpecificity))
            .GroupBy(a => a.AntibodySpecificity.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.All(a => !string.IsNullOrWhiteSpace(a.ReportingFacility))
                ? $"{g.Key} (reported by {g.First().ReportingFacility!.Trim()})"
                : g.Key)
            .ToList();

    /// <summary>
    /// Enforced special requirements in effect now (active, started, not ended), leaving out informational notes.
    /// A row whose catalog entry is unknown counts as enforced, so nothing that may constrain a unit is hidden.
    /// </summary>
    public static int RequirementsInEffect(
        IEnumerable<SpecialTransfusionRequirement> rows, DateTime nowUtc, IReadOnlySet<long>? informationalDefinitionIds = null) =>
        rows.Count(r => InEffect(r, nowUtc) && !IsInformational(r, informationalDefinitionIds));

    /// <summary>Informational notes in effect now: shown to the user, never enforced.</summary>
    public static int NotesInEffect(
        IEnumerable<SpecialTransfusionRequirement> rows, DateTime nowUtc, IReadOnlySet<long>? informationalDefinitionIds) =>
        rows.Count(r => InEffect(r, nowUtc) && IsInformational(r, informationalDefinitionIds));

    private static bool InEffect(SpecialTransfusionRequirement r, DateTime nowUtc) =>
        SpecialRequirementCatalog.IsClinicallyActive(r.IsActive, r.EffectiveUtc, r.ExpiresUtc, nowUtc);

    private static bool IsInformational(SpecialTransfusionRequirement r, IReadOnlySet<long>? informationalDefinitionIds) =>
        r.RequirementDefinitionId is long id && informationalDefinitionIds?.Contains(id) == true;

    /// <summary>The catalog entries that are informational notes.</summary>
    public static IReadOnlySet<long> InformationalIds(IEnumerable<Domain.Entities.Configuration.SpecialRequirementDefinition> definitions) =>
        definitions.Where(d => d.Category == Domain.Enums.SpecialRequirementCategory.Informational).Select(d => d.Id).ToHashSet();

    /// <summary>"2 special requirements" / "1 note", for badges and picker labels.</summary>
    public static string RequirementsLabel(int count) => $"{count} special {(count == 1 ? "requirement" : "requirements")}";

    public static string NotesLabel(int count) => $"{count} {(count == 1 ? "note" : "notes")}";
}
