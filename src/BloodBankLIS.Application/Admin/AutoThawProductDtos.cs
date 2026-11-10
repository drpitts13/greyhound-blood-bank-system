using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Admin;

public sealed record AutoThawProductDto(
    long Id,
    string ProductCode,
    string Description,
    bool IsActive,
    int ThawedShelfLifeHours = 24,
    // OCD-068: null until the facility sets them; a thaw that needs one is refused until then.
    int? OpenSystemShelfLifeHours = null,
    int? PooledShelfLifeHours = null)
{
    public static AutoThawProductDto From(AutoThawProduct row, string? description) =>
        new(row.Id, row.ProductCode, description ?? string.Empty, row.IsActive, row.ThawedShelfLifeHours,
            row.OpenSystemShelfLifeHours, row.PooledShelfLifeHours);
}

public sealed record SaveAutoThawProductRequest(string ProductCode, int? ThawedShelfLifeHours = null);

/// <summary>
/// The thawed shelf lives for an auto-thaw product code, with the reason for the change. All three are set together:
/// the open-system and pooled hours are blank (null) when not set (OCD-068).
/// </summary>
public sealed record SetAutoThawShelfLifeRequest(
    int ThawedShelfLifeHours, string? Reason, int? OpenSystemShelfLifeHours = null, int? PooledShelfLifeHours = null);
