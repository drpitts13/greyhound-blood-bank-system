namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Tells the EHR that tests will have no result because their specimen was rejected (OCD-062, decided 2026-11-06):
/// one "no result" message per open test line on the tube, with the reason and "recollect". The order stays open for
/// the new specimen. The HL7 layer implements this; specimen services stay free of HL7 types.
/// </summary>
public interface ISpecimenRejectionPublisher
{
    /// <summary>
    /// Queues the notices for a rejected specimen when an outbound results interface is enabled. Returns how many
    /// messages were queued (0 when there is no enabled interface or no open test line on the tube).
    /// </summary>
    Task<int> PublishSpecimenRejectedAsync(long specimenId, CancellationToken ct = default);
}
