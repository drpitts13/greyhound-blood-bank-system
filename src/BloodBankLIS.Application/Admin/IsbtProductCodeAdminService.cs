using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Read-only admin listing of ISBT Product Description Codes used to validate inventory intake.
/// </summary>
public sealed class IsbtProductCodeAdminService
{
    private readonly IRepository<IsbtProductCode> _codes;
    private readonly IClock _clock;

    public IsbtProductCodeAdminService(IRepository<IsbtProductCode> codes, IClock clock)
    {
        _codes = codes;
        _clock = clock;
    }

    public async Task<IReadOnlyList<IsbtProductCodeDto>> ListAsync(CancellationToken ct = default)
    {
        var asOf = DateOnly.FromDateTime(_clock.UtcNow);
        var list = await _codes.ListAsync(_ => true, ct);
        return list
            .OrderBy(c => c.ProductDescriptionCode, StringComparer.Ordinal)
            .Select(c => ToDto(c, asOf))
            .ToList();
    }

    private static IsbtProductCodeDto ToDto(IsbtProductCode c, DateOnly asOf) =>
        new(
            c.Id,
            c.ProductDescriptionCode,
            c.Description,
            c.ComponentClass,
            c.Modifier,
            c.StorageRequirements,
            c.RequiresExtendedDivision,
            c.EffectiveDate,
            c.RetiredDate,
            c.StandardVersion,
            c.IsPlaceholder,
            c.RetiredDate is not null && c.RetiredDate < asOf);
}
