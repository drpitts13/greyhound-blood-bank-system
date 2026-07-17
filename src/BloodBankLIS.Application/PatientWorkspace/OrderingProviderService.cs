using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// Resolves ordering providers from the catalog and upserts providers received via HL7.
/// </summary>
public sealed class OrderingProviderService
{
    private readonly IRepository<OrderingProvider> _providers;
    private readonly IUnitOfWork _unitOfWork;

    public OrderingProviderService(IRepository<OrderingProvider> providers, IUnitOfWork unitOfWork)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<OrderingProvider>> ListActiveAsync(CancellationToken ct = default) =>
        _providers.ListAsync(p => p.IsActive, ct);

    public Task<OrderingProvider?> GetByProviderIdAsync(string providerId, CancellationToken ct = default) =>
        _providers.FirstOrDefaultAsync(p => p.ProviderId == providerId.Trim(), ct);

    public async Task<OrderingProvider?> EnsureFromHl7Async(
        string? providerId,
        string? name,
        string? specialty,
        string? location,
        string sourceSystem,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var id = providerId.Trim();
        var displayName = name.Trim();
        var existing = await _providers.FirstOrDefaultAsync(p => p.ProviderId == id, ct);
        if (existing is not null)
        {
            var changed = false;
            if (!string.Equals(existing.Name, displayName, StringComparison.Ordinal))
            {
                existing.Name = displayName;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(specialty) && existing.Specialty != specialty)
            {
                existing.Specialty = specialty.Trim();
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(location) && existing.Location != location)
            {
                existing.Location = location.Trim();
                changed = true;
            }

            if (changed)
            {
                _providers.Update(existing);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return existing;
        }

        var provider = new OrderingProvider
        {
            ProviderId = id,
            Name = displayName,
            Specialty = string.IsNullOrWhiteSpace(specialty) ? null : specialty.Trim(),
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            IsActive = true,
            SourceSystem = sourceSystem
        };

        var validation = OrderingProviderValidator.Validate(provider, duplicateProviderId: false);
        if (validation.IsHardStopped)
        {
            return null;
        }

        await _providers.AddAsync(provider, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return provider;
    }

    public async Task<(long? Id, string? DisplayName)> ResolveDisplayAsync(long? providerId, CancellationToken ct = default)
    {
        if (!providerId.HasValue)
        {
            return (null, null);
        }

        var provider = await _providers.GetByIdAsync(providerId.Value, ct);
        return provider is null ? (null, null) : (provider.Id, provider.Name);
    }
}
