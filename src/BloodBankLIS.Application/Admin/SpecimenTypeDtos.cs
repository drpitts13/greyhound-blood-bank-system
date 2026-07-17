using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Application.Admin;

public sealed record SpecimenTypeDefinitionDto(
    long Id,
    string Code,
    string Description,
    IReadOnlyList<string> ExcludedTestCodes,
    int SortOrder,
    int Version,
    bool IsActive,
    bool IsDraft,
    DateTime? EffectiveUtc,
    DateTime? RetiredUtc,
    string? ChangeReason);

public sealed record SaveSpecimenTypeDefinitionRequest(
    string Code,
    string Description,
    IReadOnlyList<string>? ExcludedTestCodes,
    int SortOrder,
    string? ChangeReason);

public sealed record SpecimenTypeListItemDto(string Code, string Description, int SortOrder);

public static class SpecimenTypeDefinitionDtoMapping
{
    public static SpecimenTypeDefinitionDto From(SpecimenTypeDefinition d) => new(
        d.Id,
        d.Code,
        d.Description,
        Domain.ValueObjects.SpecimenTypeExcludedTests.Parse(d.ExcludedTestCodesJson),
        d.SortOrder,
        d.Version,
        d.IsActive,
        d.IsDraft,
        d.EffectiveUtc,
        d.RetiredUtc,
        d.ChangeReason);
}
