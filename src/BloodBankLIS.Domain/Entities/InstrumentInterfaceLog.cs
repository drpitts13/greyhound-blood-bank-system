using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Connection and session event for an instrument interface (log-file view).</summary>
public class InstrumentInterfaceLog : BaseEntity
{
    public long InterfaceId { get; set; }

    public DateTime OccurredUtc { get; set; }

    public string Level { get; set; } = "Info";

    public string EventType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
