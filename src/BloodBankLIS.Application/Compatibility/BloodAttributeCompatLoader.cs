using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Compatibility;

/// <summary>
/// Loads patient/unit blood attribute data for compatibility evaluation.
/// </summary>
public sealed class BloodAttributeCompatLoader
{
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<AntigenProfile> _antigenProfiles;
    private readonly IRepository<UnitBloodAttribute> _unitAttributes;
    private readonly IRepository<BloodAttributeDefinition> _definitions;

    public BloodAttributeCompatLoader(
        IRepository<AntibodyHistory> antibodies,
        IRepository<AntigenProfile> antigenProfiles,
        IRepository<UnitBloodAttribute> unitAttributes,
        IRepository<BloodAttributeDefinition> definitions)
    {
        _antibodies = antibodies;
        _antigenProfiles = antigenProfiles;
        _unitAttributes = unitAttributes;
        _definitions = definitions;
    }

    public async Task<BloodAttributeCompatSnapshot> LoadAsync(long patientId, long unitId, CancellationToken ct = default)
    {
        var defs = (await _definitions.ListAsync(d => d.IsActive, ct))
            .ToDictionary(d => d.Id);

        // Current and historical antibodies both drive antigen-negative requirements.
        var patientAntibodies = await _antibodies.ListAsync(a => a.PatientId == patientId, ct);
        var patientAntigens = await _antigenProfiles.ListAsync(p => p.PatientId == patientId, ct);
        var unitAttrs = await _unitAttributes.ListAsync(u => u.BloodProductId == unitId, ct);

        return new BloodAttributeCompatSnapshot(
            BuildPatientAntibodies(patientAntibodies, defs),
            BuildAntigens(patientAntigens.Select(p => (p.BloodAttributeDefinitionId, p.Result)), defs),
            BuildUnitAntibodies(unitAttrs, defs),
            BuildAntigens(unitAttrs.Where(u => u.AttributeKind == BloodAttributeKind.Antigen)
                .Select(u => (u.BloodAttributeDefinitionId, u.Result)), defs));
    }

    private static IReadOnlyList<BloodAttributeCompatibilityRule.AntibodyRef> BuildPatientAntibodies(
        IReadOnlyList<AntibodyHistory> antibodies,
        IReadOnlyDictionary<long, BloodAttributeDefinition> defs)
    {
        var results = new List<BloodAttributeCompatibilityRule.AntibodyRef>();
        var catalog = defs.Values
            .Select(d => new AntibodyCatalogItem(d.Id, d.Code, d.Name, d.AntibodyName))
            .ToList();
        foreach (var ab in antibodies)
        {
            BloodAttributeDefinition? def = null;
            if (ab.BloodAttributeDefinitionId is long defId)
            {
                defs.TryGetValue(defId, out def);
            }

            if (def is null)
            {
                var resolved = AntibodyIdentificationCatalogResolver.Resolve(null, ab.AntibodySpecificity, catalog);
                if (resolved.DefinitionId is long resolvedId)
                {
                    defs.TryGetValue(resolvedId, out def);
                }
            }

            if (def is not null && def.IsClinicallySignificant)
            {
                results.Add(new BloodAttributeCompatibilityRule.AntibodyRef(def.Code, def.AntibodyName));
            }
        }

        return results;
    }

    private static IReadOnlyList<BloodAttributeCompatibilityRule.AntibodyRef> BuildUnitAntibodies(
        IReadOnlyList<UnitBloodAttribute> attrs,
        IReadOnlyDictionary<long, BloodAttributeDefinition> defs)
    {
        var results = new List<BloodAttributeCompatibilityRule.AntibodyRef>();
        foreach (var attr in attrs.Where(a => a.AttributeKind == BloodAttributeKind.Antibody && a.Result == AntigenResult.Positive))
        {
            if (defs.TryGetValue(attr.BloodAttributeDefinitionId, out var def) && def.IsClinicallySignificant)
            {
                results.Add(new BloodAttributeCompatibilityRule.AntibodyRef(def.Code, def.AntibodyName));
            }
        }

        return results;
    }

    private static IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> BuildAntigens(
        IEnumerable<(long DefinitionId, AntigenResult Result)> rows,
        IReadOnlyDictionary<long, BloodAttributeDefinition> defs)
    {
        var results = new List<BloodAttributeCompatibilityRule.AntigenRef>();
        foreach (var (defId, result) in rows)
        {
            if (defs.TryGetValue(defId, out var def))
            {
                results.Add(new BloodAttributeCompatibilityRule.AntigenRef(def.Code, result));
            }
        }

        return results;
    }
}

public sealed record BloodAttributeCompatSnapshot(
    IReadOnlyList<BloodAttributeCompatibilityRule.AntibodyRef> PatientSignificantAntibodies,
    IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> PatientAntigens,
    IReadOnlyList<BloodAttributeCompatibilityRule.AntibodyRef> UnitSignificantAntibodies,
    IReadOnlyList<BloodAttributeCompatibilityRule.AntigenRef> UnitAntigens);
