using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record FacilityPolicyDto(
    long Id,
    string Key,
    string Value,
    string Category,
    string DisplayName,
    string Description,
    string Citation,
    string Kind,
    string DefaultValue,
    int? MinInclusive,
    int? MaxInclusive,
    bool LegalHold)
{
    public static FacilityPolicyDto From(SystemSetting setting, FacilityPolicyDefinition definition) => new(
        setting.Id,
        setting.Key,
        setting.Value,
        definition.Category,
        definition.DisplayName,
        setting.Description ?? definition.Description,
        definition.Citation,
        definition.Kind.ToString(),
        definition.DefaultValue,
        definition.MinInclusive,
        definition.MaxInclusive,
        setting.LegalHold);
}

public sealed record SaveFacilityPolicyRequest(string Value, string Reason);
