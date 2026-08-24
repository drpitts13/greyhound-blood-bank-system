using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Persistence for interface field-mapping rows. Replace is allowed because these are
/// configuration children, not clinical records.
/// </summary>
public interface IInterfaceFieldMappingRepository : IRepository<InterfaceFieldMapping>
{
    Task ReplaceForEndpointAsync(
        InterfaceEndpoint endpoint,
        IReadOnlyList<InterfaceFieldMapping> mappings,
        CancellationToken cancellationToken = default);
}
