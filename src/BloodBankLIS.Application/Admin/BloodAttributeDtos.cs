using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Application.Admin;

public sealed record BloodAttributeDefinitionDto(
    long Id,
    string Code,
    string Name,
    string AntibodyName,
    bool IsClinicallySignificant,
    int SortOrder,
    int Version,
    bool IsActive,
    bool IsDraft,
    DateTime? EffectiveUtc,
    DateTime? RetiredUtc,
    string? ChangeReason);

public sealed record SaveBloodAttributeDefinitionRequest(
    string Code,
    string Name,
    string AntibodyName,
    bool IsClinicallySignificant,
    int SortOrder,
    string? ChangeReason);

public static class BloodAttributeDefinitionDtoMapping
{
    public static BloodAttributeDefinitionDto From(BloodAttributeDefinition d) => new(
        d.Id,
        d.Code,
        d.Name,
        d.AntibodyName,
        d.IsClinicallySignificant,
        d.SortOrder,
        d.Version,
        d.IsActive,
        d.IsDraft,
        d.EffectiveUtc,
        d.RetiredUtc,
        d.ChangeReason);
}
