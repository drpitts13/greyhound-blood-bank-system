using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Queues an outbound billing interface message (DFT) for a captured charge.
/// The HL7 layer implements this; the billing service stays free of HL7 types.
/// </summary>
public interface IBillingInterfacePublisher
{
    /// <summary>
    /// Persists a standard outbound DFT for <paramref name="billingEvent"/> and
    /// returns the <c>HL7Messages</c> id, or null when nothing was queued.
    /// </summary>
    Task<long?> PublishChargeAsync(BillingEvent billingEvent, CancellationToken ct = default);
}
