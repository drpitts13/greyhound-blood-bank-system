using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Application.Compatibility;

/// <summary>Loads the active singleton crossmatch settings, or in-memory defaults.</summary>
public sealed class CrossmatchSettingsReader
{
    private readonly IRepository<CrossmatchSettings> _settings;

    public CrossmatchSettingsReader(IRepository<CrossmatchSettings> settings)
    {
        _settings = settings;
    }

    public async Task<CrossmatchSettings> GetActiveAsync(CancellationToken ct = default)
    {
        var rows = await _settings.ListAsync(ct);
        return rows
            .Where(s => s.IsActive && !s.IsDraft)
            .OrderByDescending(s => s.Version)
            .FirstOrDefault()
            ?? rows.OrderByDescending(s => s.Version).FirstOrDefault()
            ?? CrossmatchSettings.CreateDefault();
    }
}

public sealed record NegativeAntibodyScreenCounts(int Visits, int Specimens, int Tests);
