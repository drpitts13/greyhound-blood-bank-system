using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Admin;

public sealed record OrderingLocationDto(
    long Id,
    string Code,
    string? Name,
    string? Department,
    string? Hl7MappingCode,
    bool IsActive,
    string DisplayName)
{
    public static OrderingLocationDto From(OrderingLocation l) => new(
        l.Id,
        l.Code,
        l.Name,
        l.Department,
        l.Hl7MappingCode,
        l.IsActive,
        DisplayName: string.IsNullOrWhiteSpace(l.Name) ? l.Code : l.Name);
}

public sealed record SaveOrderingLocationRequest(
    string Code,
    string? Name,
    string? Department,
    string? Hl7MappingCode);
