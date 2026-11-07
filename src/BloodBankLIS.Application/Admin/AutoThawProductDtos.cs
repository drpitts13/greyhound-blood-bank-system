using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Admin;

public sealed record AutoThawProductDto(
    long Id,
    string ProductCode,
    string Description,
    bool IsActive,
    int ThawedShelfLifeHours = 24)
{
    public static AutoThawProductDto From(AutoThawProduct row, string? description) =>
        new(row.Id, row.ProductCode, description ?? string.Empty, row.IsActive, row.ThawedShelfLifeHours);
}

public sealed record SaveAutoThawProductRequest(string ProductCode, int? ThawedShelfLifeHours = null);

/// <summary>A new thawed shelf life for an auto-thaw product code, with the reason for the change.</summary>
public sealed record SetAutoThawShelfLifeRequest(int ThawedShelfLifeHours, string? Reason);
