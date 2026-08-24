using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Persistence for global interface value-translation rows. Replace is allowed because
/// these are configuration children, not clinical records.
/// </summary>
public interface IInterfaceValueTranslationRepository : IRepository<InterfaceValueTranslation>
{
    Task ReplaceForDataItemAsync(
        string dataItemKey,
        IReadOnlyList<InterfaceValueTranslation> rows,
        CancellationToken cancellationToken = default);
}
