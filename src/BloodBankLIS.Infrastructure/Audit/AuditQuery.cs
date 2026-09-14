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
}
