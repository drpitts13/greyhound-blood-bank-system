using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Admin;

public sealed record SpecialRequirementDefinitionDto(
    long Id,
    string Code,
    string Name,
    SpecialRequirementLevel Level,
    SpecialRequirementEnforcementKind EnforcementKind,
    string? ProductAttributeCode,
    string? Instruction,
    int SortOrder,
    int Version,
    bool IsActive,
    bool IsDraft,
    DateTime? EffectiveUtc,
    DateTime? RetiredUtc,
    string? ChangeReason);

public sealed record SaveSpecialRequirementDefinitionRequest(
    string Code,
    string Name,
    SpecialRequirementLevel Level,
    SpecialRequirementEnforcementKind EnforcementKind,
    string? ProductAttributeCode,
    string? Instruction,
    int SortOrder,
    string? ChangeReason);

public static class SpecialRequirementDefinitionDtoMapping
{
    public static SpecialRequirementDefinitionDto From(SpecialRequirementDefinition d) => new(
        d.Id,
        d.Code,
        d.Name,
        d.Level,
        d.EnforcementKind,
        d.ProductAttributeCode,
        d.Instruction,
        d.SortOrder,
        d.Version,
        d.IsActive,
        d.IsDraft,
        d.EffectiveUtc,
        d.RetiredUtc,
        d.ChangeReason);
}
