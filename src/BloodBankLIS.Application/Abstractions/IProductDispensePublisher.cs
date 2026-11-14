namespace BloodBankLIS.Application.Abstractions;

/// <summary>What happened to an issued unit that the EHR is told about.</summary>
public enum ProductDispenseEvent
{
    /// <summary>The unit left the blood bank for the patient.</summary>
    Dispensed = 0,

    /// <summary>The unit came back to the blood bank unused.</summary>
    Returned = 1
}

/// <summary>
/// Tells the EHR when a unit is dispensed to a patient or returned, so bedside administration can match the unit it
/// scans to the issue. Queued only when an outbound blood product (BPAM) interface is enabled. The HL7 layer
/// implements this; issuing stays free of HL7 types.
/// </summary>
public interface IProductDispensePublisher
{
    /// <summary>Queues the message for the issue; returns the <c>HL7Messages</c> id, or null when nothing was queued.</summary>
    Task<long?> PublishDispenseAsync(long issueId, ProductDispenseEvent dispenseEvent, CancellationToken ct = default);
}
