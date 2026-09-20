using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// TCP/IP connection for an instrument. When <see cref="IpAddress"/> is empty the
/// application listens (server). When set, the application connects as the client.
/// </summary>
public class InstrumentConnection : BaseEntity
{
    public long InterfaceId { get; set; }

    public InstrumentInterface? Interface { get; set; }

    public string? IpAddress { get; set; }

    public int Port { get; set; }

    public bool IsServer => string.IsNullOrWhiteSpace(IpAddress);
}
