using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Application.Admin;

public sealed record OrderCancellationReasonDto(
    long Id,
    string Code,
    string Text,
    int SortOrder,
    bool IsActive)
{
    public static OrderCancellationReasonDto From(OrderCancellationReason entity) =>
        new(entity.Id, entity.Code, entity.Text, entity.SortOrder, entity.IsActive);
}

public sealed record SaveOrderCancellationReasonRequest(string Code, string Text, int SortOrder);

public sealed record OrderCancellationReasonRefDto(string Code, string Text, int SortOrder);
