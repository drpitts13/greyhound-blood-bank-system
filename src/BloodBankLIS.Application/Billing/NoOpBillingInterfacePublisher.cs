using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Billing;

/// <summary>Test/default publisher that does not emit an interface message.</summary>
public sealed class NoOpBillingInterfacePublisher : IBillingInterfacePublisher
{
    public Task<long?> PublishChargeAsync(BillingEvent billingEvent, CancellationToken ct = default) =>
        Task.FromResult<long?>(null);
}
