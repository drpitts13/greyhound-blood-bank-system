using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public sealed class DeliveryLineStore : IDeliveryLineStore
{
    private readonly BloodBankDbContext _context;

    public DeliveryLineStore(BloodBankDbContext context) => _context = context;

    public async Task<IReadOnlyList<BloodDeliveryLine>> ListTrackedAsync(long deliveryId, CancellationToken cancellationToken = default) =>
        await _context.Set<BloodDeliveryLine>()
            .Where(l => l.BloodDeliveryId == deliveryId)
            .ToListAsync(cancellationToken);

    public void Remove(BloodDeliveryLine line) => _context.Set<BloodDeliveryLine>().Remove(line);
}
