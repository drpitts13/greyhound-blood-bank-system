using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;

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
    string? ChangeReason)
{
    /// <summary>
    /// Catalog antigen name. A blank name stays the attribute id.
    /// The attribute code is not a substitute, and this label is not the stored name.
    /// </summary>
    public string NameLabel => BloodAttributeLabel.Name(Id, Name);

    /// <summary>
    /// Catalog antibody name. A blank name stays the attribute id.
    /// The antigen name and the code are not substitutes, and this label is not the stored antibody name.
    /// </summary>
    public string AntibodyNameLabel => BloodAttributeLabel.Antibody(Id, AntibodyName);
}

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
