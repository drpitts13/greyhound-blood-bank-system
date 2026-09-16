using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Seeded special-requirement codes and legacy enum mapping. Runtime prefers the
/// catalog row; this map keeps issue/allocate evaluation working when a patient
/// assignment still has only <see cref="SpecialTransfusionRequirementType"/>.
/// </summary>
public static class SpecialRequirementCatalog
{
    public const string Irradiated = "IRRAD";
    public const string CmvNegative = "CMVNEG";
    public const string Leukoreduced = "LR";
    public const string Washed = "WASHED";
    public const string AntigenNegative = "AGNEG";
    public const string Other = "OTHER";
    public const string BloodWarmer = "WARMER";
    public const string ExtendedCrossmatch = "EXTXM";
    public const string TypeForA2 = "TYPEA2";

    public static bool IsClinicallyActive(bool isActive, DateTime effectiveUtc, DateTime? expiresUtc, DateTime nowUtc) =>
        isActive && effectiveUtc <= nowUtc && (expiresUtc is null || expiresUtc > nowUtc);

    public static AboSubgroup DefaultSubgroup(AboGroup abo) =>
        abo is AboGroup.A or AboGroup.AB ? AboSubgroup.Unknown : AboSubgroup.NotApplicable;

    public static bool AboSubgroupSatisfied(AboGroup abo, AboSubgroup subgroup)
    {
        if (abo is not AboGroup.A and not AboGroup.AB)
        {
            return true;
        }

        return subgroup is AboSubgroup.A1 or AboSubgroup.A2;
    }

    public static string CodeFor(SpecialTransfusionRequirementType type) => type switch
    {
        SpecialTransfusionRequirementType.Irradiated => Irradiated,
        SpecialTransfusionRequirementType.CmvNegative => CmvNegative,
        SpecialTransfusionRequirementType.Leukoreduced => Leukoreduced,
        SpecialTransfusionRequirementType.Washed => Washed,
        SpecialTransfusionRequirementType.AntigenNegative => AntigenNegative,
        SpecialTransfusionRequirementType.Other => Other,
        _ => Other
    };

    public static SpecialTransfusionRequirementType? TypeFor(string code) => code.Trim().ToUpperInvariant() switch
    {
        Irradiated => SpecialTransfusionRequirementType.Irradiated,
        CmvNegative => SpecialTransfusionRequirementType.CmvNegative,
        Leukoreduced => SpecialTransfusionRequirementType.Leukoreduced,
        Washed => SpecialTransfusionRequirementType.Washed,
        AntigenNegative => SpecialTransfusionRequirementType.AntigenNegative,
        Other => SpecialTransfusionRequirementType.Other,
        _ => null
    };

    public static SpecialTransfusionRequirementRule.RequirementRef ToRef(
        SpecialTransfusionRequirementType type,
        string? antigenCode,
        DateTime effectiveUtc,
        DateTime? expiresUtc,
        bool isActive)
    {
        var (kind, productAttribute) = type switch
        {
            SpecialTransfusionRequirementType.Irradiated =>
                (SpecialRequirementEnforcementKind.RequireProductAttribute, Irradiated),
            SpecialTransfusionRequirementType.CmvNegative =>
                (SpecialRequirementEnforcementKind.RequireProductAttribute, CmvNegative),
            SpecialTransfusionRequirementType.Leukoreduced =>
                (SpecialRequirementEnforcementKind.RequireProductAttribute, Leukoreduced),
            SpecialTransfusionRequirementType.Washed =>
                (SpecialRequirementEnforcementKind.RequireProductAttribute, Washed),
            SpecialTransfusionRequirementType.AntigenNegative =>
                (SpecialRequirementEnforcementKind.RequireAntigenNegative, (string?)null),
            _ => (SpecialRequirementEnforcementKind.RequireIssueAcknowledgment, (string?)null)
        };

        return new SpecialTransfusionRequirementRule.RequirementRef(
            CodeFor(type),
            kind,
            productAttribute,
            antigenCode,
            effectiveUtc,
            expiresUtc,
            isActive);
    }
}
