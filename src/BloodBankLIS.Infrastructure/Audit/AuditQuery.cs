using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Audit;

public sealed class AuditQuery : IAuditQuery
{
    private readonly BloodBankDbContext _context;

    public AuditQuery(BloodBankDbContext context) => _context = context;

    public async Task<IReadOnlyList<AuditEvent>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.AuditEvents.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditEvent>> ListForEntityAsync(string entityType, long entityId, CancellationToken cancellationToken = default) =>
        await _context.AuditEvents.AsNoTracking()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);
}
