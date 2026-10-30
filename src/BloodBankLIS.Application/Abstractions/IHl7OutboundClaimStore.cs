using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Claims an outbound HL7 message for transmission with one conditional UPDATE
/// (status to <see cref="Hl7MessageStatus.Sending"/> plus <c>ClaimedUtc</c>), so the
/// background sender and a manual send can never transmit the same message twice.
/// </summary>
public interface IHl7OutboundClaimStore
{
    /// <summary>
    /// Claims the message only while its status is still <paramref name="expectedStatus"/>.
    /// When <paramref name="expectedStatus"/> is <see cref="Hl7MessageStatus.Sending"/>, only
    /// a claim older than <paramref name="staleClaimBeforeUtc"/> is taken over. Returns the
    /// claimed row, tracked and reloaded, or null when another caller holds it.
    /// </summary>
    Task<Hl7MessageLog?> TryClaimAsync(
        long messageId,
        Hl7MessageStatus expectedStatus,
        DateTime claimedUtc,
        DateTime staleClaimBeforeUtc,
        CancellationToken cancellationToken = default);
}
