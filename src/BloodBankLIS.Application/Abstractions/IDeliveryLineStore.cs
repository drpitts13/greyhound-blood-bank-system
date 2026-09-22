using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Removes an unused delivery line. The generic repository does not expose hard delete.
/// </summary>
public interface IDeliveryLineStore
{
    Task<IReadOnlyList<BloodDeliveryLine>> ListTrackedAsync(long deliveryId, CancellationToken cancellationToken = default);

    void Remove(BloodDeliveryLine line);
}
