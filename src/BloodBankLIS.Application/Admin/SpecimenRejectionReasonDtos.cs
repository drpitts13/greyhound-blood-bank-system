using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Application.Admin;

public sealed record SpecimenRejectionReasonDto(
    long Id,
    string Code,
    string Text,
    bool RequiresComment,
    int SortOrder,
    bool IsActive)
{
    public static SpecimenRejectionReasonDto From(SpecimenRejectionReason entity) =>
        new(entity.Id, entity.Code, entity.Text, entity.RequiresComment, entity.SortOrder, entity.IsActive);
}

public sealed record SaveSpecimenRejectionReasonRequest(string Code, string Text, int SortOrder, bool RequiresComment = false);

public sealed record SpecimenRejectionReasonRefDto(string Code, string Text, bool RequiresComment, int SortOrder);
