namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Consumes a single-use electronic signature with one conditional UPDATE so two
/// concurrent requests can never both bind the same signature to an action.
/// </summary>
public interface ISignatureConsumptionStore
{
    /// <summary>
    /// Sets <c>ConsumedUtc</c> only where it is still null. Returns false when the
    /// signature does not exist or was already consumed.
    /// </summary>
    Task<bool> TryConsumeAsync(long signatureId, DateTime consumedUtc, CancellationToken cancellationToken = default);
}
