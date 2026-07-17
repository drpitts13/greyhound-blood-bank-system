using BloodBankLIS.Application.Admin;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Audit;

/// <summary>EF Core read model over <c>ConfigurationChangeHistory</c>.</summary>
public sealed class ConfigurationHistoryReader : IConfigurationHistoryReader
{
    private readonly BloodBankDbContext _context;

    public ConfigurationHistoryReader(BloodBankDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ConfigHistoryDto>> GetForEntityAsync(string entityType, long entityId, int max = 100, CancellationToken ct = default)
    {
        var rows = await _context.ConfigurationChangeHistory.AsNoTracking()
            .Where(h => h.EntityType == entityType && h.EntityId == entityId)
            .OrderByDescending(h => h.ChangedUtc).ThenByDescending(h => h.Id)
            .Take(max)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ConfigHistoryDto>> RecentAsync(string? entityType, int max = 100, CancellationToken ct = default)
    {
        var query = _context.ConfigurationChangeHistory.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(h => h.EntityType == entityType);
        }

        var rows = await query.OrderByDescending(h => h.ChangedUtc).ThenByDescending(h => h.Id).Take(max).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    private static ConfigHistoryDto Map(Domain.Audit.ConfigurationChangeHistory h) => new(
        h.Id, h.EntityType, h.EntityId, h.Version, h.Action, h.OldValueJson, h.NewValueJson,
        h.ChangeReason, h.ChangedBy, h.Workstation, h.ChangedUtc, h.Environment, h.IsDevMode);
}
