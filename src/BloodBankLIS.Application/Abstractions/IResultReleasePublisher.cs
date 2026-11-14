namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Tells the EHR when a released result changes: a verified correction (OBX-11 C) or a verified result that was
/// invalidated (OBX-11 W). Decided by the product owner, 2026-11-02 (OCD-049): these go automatically. The HL7 layer
/// implements this; result services stay free of HL7 types.
/// </summary>
public interface IResultReleasePublisher
{
    /// <summary>
    /// Queues <paramref name="resultId"/> (a final, a correction or a withdrawal; the caller decides which go
    /// automatically, OCD-049 / OCD-086) when an outbound results interface is enabled. Returns the <c>HL7Messages</c> id, or null when nothing was queued.
    /// </summary>
    Task<long?> PublishResultChangeAsync(long resultId, CancellationToken ct = default);
}
