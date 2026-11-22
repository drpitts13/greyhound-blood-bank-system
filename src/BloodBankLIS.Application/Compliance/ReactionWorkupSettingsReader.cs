using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities.Configuration;

namespace BloodBankLIS.Application.Compliance;

/// <summary>Loads the active reaction workup settings. With no row yet, no Test Group is chosen for either specimen.</summary>
public sealed class ReactionWorkupSettingsReader
{
    private readonly IRepository<ReactionWorkupSettings> _settings;

    public ReactionWorkupSettingsReader(IRepository<ReactionWorkupSettings> settings)
    {
        _settings = settings;
    }

    public async Task<ReactionWorkupSettings?> GetActiveAsync(CancellationToken ct = default)
    {
        var rows = await _settings.ListAsync(ct);
        return rows
            .Where(s => s.IsActive && !s.IsDraft)
            .OrderByDescending(s => s.Version)
            .FirstOrDefault();
    }
}
