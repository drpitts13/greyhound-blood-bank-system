using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// An in-app alert. <see cref="Key"/> identifies the condition so the same condition is
/// not raised twice while it is still active. A condition alert is resolved by the
/// system when the condition clears; any alert leaves the inbox once acknowledged.
/// </summary>
public class Alert : BaseEntity
{
    public string Source { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Link { get; set; }

    public DateTime RaisedUtc { get; set; }

    public string? AcknowledgedBy { get; set; }

    public DateTime? AcknowledgedUtc { get; set; }

    public string? AcknowledgeNote { get; set; }

    public DateTime? ResolvedUtc { get; set; }
}
