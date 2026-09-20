using BloodBankLIS.Domain.Entities;

namespace BloodBankLIS.Application.Abstractions;

public interface IInstrumentInterfaceRepository : IRepository<InstrumentInterface>
{
    Task<IReadOnlyList<InstrumentInterface>> ListWithChildrenAsync(CancellationToken cancellationToken = default);

    Task<InstrumentInterface?> GetWithChildrenAsync(long id, CancellationToken cancellationToken = default);

    Task ReplaceChildrenAsync(
        InstrumentInterface entity,
        InstrumentConnection connection,
        IReadOnlyList<InstrumentFieldMapping> fieldMappings,
        IReadOnlyList<InstrumentTestMapping> testMappings,
        IReadOnlyList<InstrumentInterfaceRule> rules,
        CancellationToken cancellationToken = default);
}
