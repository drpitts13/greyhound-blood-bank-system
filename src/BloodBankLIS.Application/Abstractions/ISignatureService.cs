using BloodBankLIS.Application.Common;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Records append-only electronic signatures attesting to a meaning-of-signature for a
/// given action and (optional) context. Dangerous actions reference the returned id so
/// the attestation is preserved alongside the audit trail (docs/safety-rules.md).
/// </summary>
public interface ISignatureService
{
    /// <summary>
    /// Records a signature for the current user. Fails if the current user cannot be
    /// resolved to an active account or the meaning-of-signature is blank.
    /// </summary>
    Task<OperationResult<long>> RecordAsync(
        string action,
        string meaningOfSignature,
        string? contextType = null,
        long? contextId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a previously recorded signature exists, was made by the current user,
    /// and (when supplied) matches the expected action — used to gate override paths.
    /// </summary>
    Task<bool> IsValidForCurrentUserAsync(
        long signatureId,
        string? expectedAction = null,
        CancellationToken cancellationToken = default);
}
