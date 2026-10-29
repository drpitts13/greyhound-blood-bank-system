using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>A reagent used for daily quality control. This is not a CLIA hold rule.</summary>
public class Reagent : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public enum ReagentLotState
{
    Received = 0,
    Opened = 1,
    Closed = 2
}

/// <summary>One received lot of a reagent, from receipt through close.</summary>
public class ReagentLot : BaseEntity
{
    public long ReagentId { get; set; }

    public Reagent? Reagent { get; set; }

    public string LotNumber { get; set; } = string.Empty;

    public DateTime ReceivedUtc { get; set; }

    public DateTime? OpenedUtc { get; set; }

    public DateTime? ClosedUtc { get; set; }

    public DateTime ExpiresUtc { get; set; }

    public ReagentLotState State { get; set; } = ReagentLotState.Received;
}

/// <summary>One daily QC row for a reagent lot, including a distinct reviewer when signed.</summary>
public class ReagentDailyQc : BaseEntity
{
    public long ReagentLotId { get; set; }

    public ReagentLot? ReagentLot { get; set; }

    public DateTime QcDateUtc { get; set; }

    public string ExpectedResult { get; set; } = string.Empty;

    public string ObservedResult { get; set; } = string.Empty;

    public bool Passed { get; set; }

    public string PerformedBy { get; set; } = string.Empty;

    public DateTime PerformedUtc { get; set; }

    public string? ReviewedBy { get; set; }

    public DateTime? ReviewedUtc { get; set; }
}
