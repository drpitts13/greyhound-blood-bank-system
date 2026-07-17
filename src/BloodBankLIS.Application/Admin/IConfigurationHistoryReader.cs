namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Read access to the append-only configuration change history for the admin UI
/// (history timeline and version comparison).
/// </summary>
public interface IConfigurationHistoryReader
{
    Task<IReadOnlyList<ConfigHistoryDto>> GetForEntityAsync(string entityType, long entityId, int max = 100, CancellationToken ct = default);

    Task<IReadOnlyList<ConfigHistoryDto>> RecentAsync(string? entityType, int max = 100, CancellationToken ct = default);
}
