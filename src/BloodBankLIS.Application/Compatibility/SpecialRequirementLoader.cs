using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compatibility;

/// <summary>
/// Builds what the special requirement rule needs, the same way for selection, allocation, ready-to-issue, and issue:
/// the patient's requirements as their catalog entries read now, the product attributes by product type, and the
/// tests recorded negative on each unit.
/// </summary>
public static class SpecialRequirementLoader
{
    public static SpecialTransfusionRequirementRule.RequirementRef ToRef(
        SpecialTransfusionRequirement row,
        IReadOnlyDictionary<long, SpecialRequirementDefinition> definitions)
    {
        if (row.RequirementDefinitionId is long id && definitions.TryGetValue(id, out var definition))
        {
            return new SpecialTransfusionRequirementRule.RequirementRef(
                definition.Code,
                definition.EnforcementKind,
                definition.ProductAttributeCode,
                // A fixed antigen on the definition wins over one typed on the chart.
                string.IsNullOrWhiteSpace(definition.AntigenCode) ? row.AntigenCode : definition.AntigenCode,
                row.EffectiveUtc,
                row.ExpiresUtc,
                row.IsActive,
                definition.ComponentClass,
                definition.AllowedAboGroups,
                definition.UnitTestCode,
                definition.Name);
        }

        return SpecialTransfusionRequirementRule.RequirementRef.FromLegacy(
            row.RequirementType, row.AntigenCode, row.EffectiveUtc, row.ExpiresUtc, row.IsActive);
    }

    public static async Task<Dictionary<long, IReadOnlyList<SpecialTransfusionRequirementRule.RequirementRef>>> ByPatientAsync(
        IRepository<SpecialTransfusionRequirement>? requirements,
        IRepository<SpecialRequirementDefinition>? definitions,
        IReadOnlyCollection<long> patientIds,
        CancellationToken ct)
    {
        if (requirements is null || patientIds.Count == 0)
        {
            return [];
        }

        var rows = await requirements.ListAsync(r => patientIds.Contains(r.PatientId) && r.IsActive, ct);
        var byId = definitions is null
            ? new Dictionary<long, SpecialRequirementDefinition>()
            : (await definitions.ListAsync(ct)).ToDictionary(d => d.Id);
        return rows
            .GroupBy(r => r.PatientId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SpecialTransfusionRequirementRule.RequirementRef>)g.Select(r => ToRef(r, byId)).ToList());
    }

    public static async Task<IReadOnlyList<SpecialTransfusionRequirementRule.RequirementRef>> ForPatientAsync(
        IRepository<SpecialTransfusionRequirement>? requirements,
        IRepository<SpecialRequirementDefinition>? definitions,
        long patientId,
        CancellationToken ct) =>
        (await ByPatientAsync(requirements, definitions, [patientId], ct)).GetValueOrDefault(patientId) ?? [];

    public static async Task<Dictionary<long, HashSet<string>>> ProductAttributeCodesByProductTypeAsync(
        IRepository<ProductAttributeAssignment>? assignments,
        IRepository<ProductAttribute>? attributes,
        CancellationToken ct)
    {
        if (assignments is null || attributes is null)
        {
            return [];
        }

        var active = await assignments.ListAsync(a => a.IsActive, ct);
        var byId = (await attributes.ListAsync(a => a.IsActive, ct)).ToDictionary(a => a.Id);
        var byProduct = new Dictionary<long, HashSet<string>>();
        foreach (var assignment in active)
        {
            if (!byId.TryGetValue(assignment.ProductAttributeId, out var attribute))
            {
                continue;
            }

            if (!byProduct.TryGetValue(assignment.ProductTypeId, out var codes))
            {
                codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                byProduct[assignment.ProductTypeId] = codes;
            }

            codes.Add(attribute.Code);
        }

        return byProduct;
    }

    /// <summary>Upper-case test codes recorded negative on each unit (CMV, HBS).</summary>
    public static async Task<Dictionary<long, HashSet<string>>> NegativeTestCodesByUnitAsync(
        IRepository<BloodComponentSpecialTest>? tests,
        IReadOnlyCollection<long> unitIds,
        CancellationToken ct)
    {
        if (tests is null || unitIds.Count == 0)
        {
            return [];
        }

        var rows = await tests.ListAsync(t => unitIds.Contains(t.BloodProductId), ct);
        return rows
            .Where(t => SpecialTransfusionRequirementRule.IsNegativeResult(t.Result))
            .GroupBy(t => t.BloodProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => t.TestCode.Trim().ToUpperInvariant()).ToHashSet(StringComparer.Ordinal));
    }

    public static SpecialTransfusionRequirementRule.UnitTyping Typing(
        BloodUnit unit,
        ComponentClass componentClass,
        IReadOnlyDictionary<long, HashSet<string>> negativeTestsByUnit) =>
        new(
            componentClass,
            unit.Abo,
            unit.RhD,
            negativeTestsByUnit.TryGetValue(unit.Id, out var codes) ? codes : new HashSet<string>(StringComparer.Ordinal));
}
