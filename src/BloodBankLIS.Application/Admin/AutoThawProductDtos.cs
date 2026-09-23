using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Admin;

public sealed record AutoThawProductDto(
    long Id,
    string ProductCode,
    string Description,
    bool IsActive)
{
    public static AutoThawProductDto From(AutoThawProduct row, string? description) =>
        new(row.Id, row.ProductCode, description ?? string.Empty, row.IsActive);
}

public sealed record SaveAutoThawProductRequest(string ProductCode);
