using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Inventory;

public sealed record SaveUnitBloodAttributeRequest(
    long BloodAttributeDefinitionId,
    BloodAttributeKind AttributeKind,
    AntigenResult Result);

public sealed record UnitBloodAttributeDto(
    long Id,
    long BloodProductId,
    long BloodAttributeDefinitionId,
    string AntigenCode,
    string AntigenName,
    string AntibodyName,
    BloodAttributeKind AttributeKind,
    AntigenResult Result,
    long? SourceResultId)
{
    public static UnitBloodAttributeDto From(UnitBloodAttribute a, string code, string name, string antibodyName) => new(
        a.Id, a.BloodProductId, a.BloodAttributeDefinitionId, code, name, antibodyName,
        a.AttributeKind, a.Result, a.SourceResultId);
}
