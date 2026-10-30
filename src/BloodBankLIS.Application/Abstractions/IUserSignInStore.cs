namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Sign-in counters updated with conditional UPDATEs rather than read-modify-write,
/// so concurrent attempts against one account never lose an increment.
/// </summary>
public interface IUserSignInStore
{
    /// <summary>
    /// Increments <c>FailedSignInCount</c> and locks the account once it reaches
    /// <paramref name="lockoutThreshold"/>. <see cref="FailedSignInOutcome.LockedNow"/> is true
    /// for exactly one caller: the attempt that moved the account into the locked state.
    /// </summary>
    Task<FailedSignInOutcome> RecordFailedSignInAsync(
        long userId,
        int lockoutThreshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the failed-sign-in count and stamps <c>LastLoginUtc</c>, only while the
    /// account is still active and unlocked. Returns false when it was locked meanwhile.
    /// </summary>
    Task<bool> RecordSuccessfulSignInAsync(
        long userId,
        DateTime signedInUtc,
        CancellationToken cancellationToken = default);
}

public sealed record FailedSignInOutcome(int FailedSignInCount, bool LockedNow);
