using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Persistence for report definitions. Unlike clinical repositories, a non-system
/// report may be removed. System-owned rows are rejected by <c>ReportService</c>.
/// </summary>
public interface IReportDefinitionStore
{
    Task<IReadOnlyList<ReportDefinition>> ListAsync(CancellationToken cancellationToken = default);

    Task<ReportDefinition?> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, long? exceptId, CancellationToken cancellationToken = default);

    Task AddAsync(ReportDefinition entity, CancellationToken cancellationToken = default);

    void Remove(ReportDefinition entity);
}
