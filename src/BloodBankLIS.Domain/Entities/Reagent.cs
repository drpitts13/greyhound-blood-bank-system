using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>A reagent used for daily quality control. This is not a CLIA hold rule.</summary>
public class Reagent : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The LIS tests this reagent is used for (comma-separated codes, e.g. "ABORH, ABSC"). A manual result for one of
    /// these tests records this reagent's open lots, so a lot that later fails QC can be traced to its results.
    /// </summary>
    public string? TestCodes { get; set; }

    /// <summary>
    /// The controls the reagent's daily QC is run with, from the package insert (for example "A1 cells (positive),
    /// B cells (negative)"), comma-separated; blank means one QC reading a day (end-user testing pass 93).
    /// </summary>
    public string? QcControls { get; set; }
}

/// <summary>
/// A reagent lot recorded against a result: a lot of a reagent used for the test that was open at the bench when the
/// result was entered. The reagent code and lot number are kept as they were.
/// </summary>
public class TestResultReagentLot : BaseEntity
{
    public long TestResultId { get; set; }

    public long ReagentLotId { get; set; }

    public string ReagentCode { get; set; } = string.Empty;

    public string LotNumber { get; set; } = string.Empty;

    public DateTime RecordedUtc { get; set; }

    /// <summary>When someone reviewed this result after the lot failed QC, and what they found or did.</summary>
    public DateTime? ReviewedUtc { get; set; }

    public string? ReviewedBy { get; set; }

    public string? ReviewNote { get; set; }
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

    /// <summary>Which of the reagent's QC controls this reading is for, when it has any (pass 93).</summary>
    public string? Control { get; set; }

    public bool Passed { get; set; }

    public string PerformedBy { get; set; } = string.Empty;

    public DateTime PerformedUtc { get; set; }

    public string? ReviewedBy { get; set; }

    public DateTime? ReviewedUtc { get; set; }
}
