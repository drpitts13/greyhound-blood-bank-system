using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// End-of-shift packet. Captures the queue counts already used on the dashboard
/// plus a required note. Acknowledgment is a separate user action and is audited.
/// Counts do not change issue, result, or investigation gates.
/// </summary>
public class ShiftHandoff : BaseEntity
{
    public DateTime RecordedUtc { get; set; }

    public string FromUser { get; set; } = string.Empty;

    public string? ToUser { get; set; }

    public string Notes { get; set; } = string.Empty;

    public int RetrospectiveDue { get; set; }

    public int RetrospectiveOverdue { get; set; }

    public int OutstandingIssued { get; set; }

    public int InTransit { get; set; }

    public int InTransitOverdue { get; set; }

    public int PendingTests { get; set; }

    public int PendingSpecimens { get; set; }

    public int OpenReactions { get; set; }

    public int FatalityFollowUpDue { get; set; }

    /// <summary>Massive transfusion protocols still active at handoff.</summary>
    public int ActiveMtp { get; set; }

    /// <summary>Transfusions started at the bedside and not yet completed at handoff.</summary>
    public int TransfusionsRunning { get; set; }

    /// <summary>Of those, how many are past the facility completion reminder hours.</summary>
    public int TransfusionsCompletionOverdue { get; set; }

    /// <summary>Emergency or MTP issues whose retrospective crossmatch came back incompatible and are still open.</summary>
    public int RetrospectiveIncompatible { get; set; }

    /// <summary>Pending results whose specimen expires within the near-expiry window, so they must be verified first.</summary>
    public int PendingTestsVerifyBeforeExpiry { get; set; }

    /// <summary>Pending results already entered and waiting for a second person to verify them.</summary>
    public int ResultsAwaitingVerification { get; set; }

    /// <summary>Reservations past their own end time, still holding a unit for a patient.</summary>
    public int ReservationsLapsed { get; set; }

    /// <summary>Units inside the near-expiry window, and how many of them are reserved for a patient.</summary>
    public int NearExpiryUnits { get; set; }

    public int NearExpiryReserved { get; set; }

    /// <summary>Coolers with units still in transit, and of those how many were never checked or checked with differences.</summary>
    public int CoolersInTransit { get; set; }

    public int CoolersNotChecked { get; set; }

    public int CoolersWithDifferences { get; set; }

    /// <summary>Expired units still recorded on a shelf, to pull and discard.</summary>
    public int ExpiredOnShelf { get; set; }

    /// <summary>Quality deviations not yet closed, and how many of them are critical.</summary>
    public int OpenDeviations { get; set; }

    public int CriticalDeviations { get; set; }

    /// <summary>Products (by ABO where the level names one) with fewer available units than their minimum stock level.</summary>
    public int ProductsBelowMinimum { get; set; }

    /// <summary>Instrument messages that failed and are still open (replay, release, or resolve).</summary>
    public int InstrumentFailuresOpen { get; set; }

    /// <summary>Of those, results an instrument error code held for review.</summary>
    public int InstrumentResultsHeld { get; set; }

    /// <summary>Reagent lots whose latest daily QC failed.</summary>
    public int ReagentLotsFailedQc { get; set; }

    /// <summary>Results tested with those lots since they last passed QC and not yet reviewed.</summary>
    public int ReagentResultsToReview { get; set; }

    /// <summary>HL7 messages in the interface error queue not yet resolved (an order, result, or admission that did not file or send).</summary>
    public int Hl7ErrorsOpen { get; set; }

    /// <summary>Interface values with no translation, still open on the Unmapped values list.</summary>
    public int UnmappedInterfaceValues { get; set; }

    /// <summary>RhIG evaluations still needing work: not completed, or a completed candidate with no RhIG given recorded.</summary>
    public int RhigOpen { get; set; }

    /// <summary>Of those, completed candidates with no RhIG given recorded.</summary>
    public int RhigToGive { get; set; }

    /// <summary>Unidentified emergency patients not yet merged into an identified chart.</summary>
    public int UnidentifiedPatients { get; set; }

    /// <summary>Stored tubes past the facility's keep-until date (0 when no retention period is set).</summary>
    public int TubesDueForDiscard { get; set; }

    /// <summary>Open blood orders whose units are not all reserved or issued yet.</summary>
    public int BloodOrdersToPrepare { get; set; }

    /// <summary>Of those, orders whose needed-by time has passed.</summary>
    public int BloodOrdersPastNeededBy { get; set; }

    public string? AcknowledgedBy { get; set; }

    public DateTime? AcknowledgedUtc { get; set; }
}
