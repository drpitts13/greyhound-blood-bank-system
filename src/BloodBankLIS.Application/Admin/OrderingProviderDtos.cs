using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Admin;

public sealed record OrderingProviderDto(
    long Id,
    string ProviderId,
    string Name,
    string? Specialty,
    string? Location,
    bool IsActive,
    string? SourceSystem)
{
    public static OrderingProviderDto From(OrderingProvider p) => new(
        p.Id, p.ProviderId, p.Name, p.Specialty, p.Location, p.IsActive, p.SourceSystem);
}

public sealed record SaveOrderingProviderRequest(
    string ProviderId,
    string Name,
    string? Specialty,
    string? Location);
