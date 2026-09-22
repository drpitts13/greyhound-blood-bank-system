using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Reporting;

public sealed class ReportDefinitionStore : IReportDefinitionStore
{
    private readonly BloodBankDbContext _db;

    public ReportDefinitionStore(BloodBankDbContext db) => _db = db;

    public async Task<IReadOnlyList<ReportDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.ReportDefinitions.AsNoTracking().ToListAsync(cancellationToken);

    public Task<ReportDefinition?> GetAsync(long id, CancellationToken cancellationToken = default) =>
        _db.ReportDefinitions.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, long? exceptId, CancellationToken cancellationToken = default) =>
        _db.ReportDefinitions.AnyAsync(
            row => row.Code == code && (exceptId == null || row.Id != exceptId),
            cancellationToken);

    public async Task AddAsync(ReportDefinition entity, CancellationToken cancellationToken = default) =>
        await _db.ReportDefinitions.AddAsync(entity, cancellationToken);

    public void Remove(ReportDefinition entity) => _db.ReportDefinitions.Remove(entity);
}
