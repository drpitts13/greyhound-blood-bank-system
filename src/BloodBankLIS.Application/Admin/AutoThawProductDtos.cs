using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Admin;

public sealed record AutoThawProductDto(
    long Id,
    long ProductTypeId,
    string ProductCode,
    string ProductName,
    bool IsActive)
{
    public static AutoThawProductDto From(AutoThawProduct row, ProductType? product) =>
        new(row.Id, row.ProductTypeId, product?.ProductCode ?? string.Empty, product?.Name ?? string.Empty, row.IsActive);
}

public sealed record SaveAutoThawProductRequest(long ProductTypeId);
