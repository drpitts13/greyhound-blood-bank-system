using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Ordered if/then transform applied to a parsed instrument message.</summary>
public class InstrumentInterfaceRule : BaseEntity
{
    public long InterfaceId { get; set; }

    public InstrumentInterface? Interface { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ConditionExpression { get; set; } = string.Empty;

    public string ActionExpression { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool StopOnMatch { get; set; }
}
