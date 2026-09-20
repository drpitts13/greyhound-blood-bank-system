namespace BloodBankLIS.Application.Abstractions;

public sealed record InstrumentRuntimeSnapshot(string State, string? Detail, DateTime? LastEventUtc);

/// <summary>In-memory listen/connect status published by ASTM hosted services.</summary>
public interface IInstrumentRuntimeStatus
{
    void Set(long interfaceId, string state, string? detail = null);

    InstrumentRuntimeSnapshot Get(long interfaceId);
}
