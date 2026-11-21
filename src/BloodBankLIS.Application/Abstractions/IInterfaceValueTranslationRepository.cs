using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Persistence for interface value-translation rows. Replace is allowed because these are configuration children, not
/// clinical records; it replaces one field's rows in one scope (all interfaces, or one interface).
/// </summary>
public interface IInterfaceValueTranslationRepository : IRepository<InterfaceValueTranslation>
{
    Task ReplaceForDataItemAsync(
        string dataItemKey,
        IReadOnlyList<InterfaceValueTranslation> rows,
        CancellationToken cancellationToken = default,
        long? endpointId = null);
}
