using System.Collections.Concurrent;
using BloodBankLIS.Application.Abstractions;

namespace BloodBankLIS.Infrastructure.Instruments;

public sealed class InstrumentRuntimeStatus : IInstrumentRuntimeStatus
{
    private readonly ConcurrentDictionary<long, InstrumentRuntimeSnapshot> _states = new();

    public void Set(long interfaceId, string state, string? detail = null) =>
        _states[interfaceId] = new InstrumentRuntimeSnapshot(state, detail, DateTime.UtcNow);

    public InstrumentRuntimeSnapshot Get(long interfaceId) =>
        _states.TryGetValue(interfaceId, out var snapshot)
            ? snapshot
            : new InstrumentRuntimeSnapshot("Idle", null, null);
}
