using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Application.Admin;

public sealed record UnitDiscardReasonDto(
    long Id,
    string Code,
    string Text,
    bool RequiresComment,
    int SortOrder,
    bool IsActive)
{
    public static UnitDiscardReasonDto From(UnitDiscardReason entity) =>
        new(entity.Id, entity.Code, entity.Text, entity.RequiresComment, entity.SortOrder, entity.IsActive);
}

public sealed record SaveUnitDiscardReasonRequest(string Code, string Text, int SortOrder, bool RequiresComment = false);

public sealed record UnitDiscardReasonRefDto(string Code, string Text, bool RequiresComment, int SortOrder);
