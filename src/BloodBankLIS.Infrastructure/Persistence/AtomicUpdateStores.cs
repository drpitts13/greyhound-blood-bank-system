using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

// ExecuteUpdate goes straight to the database: it skips the change tracker and the
// SaveChanges audit hook. These stores therefore stamp ModifiedUtc/ModifiedBy
// themselves, and callers keep writing their explicit audit events. A copy of the
// row already tracked by this context is reloaded so later SaveChanges calls (and
// the SQL Server rowversion check) see the new values.

public sealed class SignatureConsumptionStore : ISignatureConsumptionStore
{
    private readonly BloodBankDbContext _context;
    private readonly ICurrentUser _currentUser;

    public SignatureConsumptionStore(BloodBankDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> TryConsumeAsync(long signatureId, DateTime consumedUtc, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Set<ElectronicSignature>()
            .Where(s => s.Id == signatureId && s.ConsumedUtc == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.ConsumedUtc, consumedUtc)
                .SetProperty(s => s.ModifiedUtc, consumedUtc)
                .SetProperty(s => s.ModifiedBy, _currentUser.UserName), cancellationToken);

        await TrackedRowReload.ReloadAsync<ElectronicSignature>(_context, signatureId, cancellationToken);
        return rows == 1;
    }
}

public sealed class UserSignInStore : IUserSignInStore
{
    private readonly BloodBankDbContext _context;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public UserSignInStore(BloodBankDbContext context, IClock clock, ICurrentUser currentUser)
    {
        _context = context;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<FailedSignInOutcome> RecordFailedSignInAsync(
        long userId,
        int lockoutThreshold,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var users = _context.Set<User>();

        await users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(u => u.FailedSignInCount, u => u.FailedSignInCount + 1)
                .SetProperty(u => u.ModifiedUtc, now)
                .SetProperty(u => u.ModifiedBy, _currentUser.UserName), cancellationToken);

        // A second conditional UPDATE flips the lock; only one concurrent attempt can
        // match "not yet locked", so exactly one caller reports the lockout.
        var locked = await users
            .Where(u => u.Id == userId && !u.IsLocked && u.FailedSignInCount >= lockoutThreshold)
            .ExecuteUpdateAsync(set => set
                .SetProperty(u => u.IsLocked, true)
                .SetProperty(u => u.ModifiedUtc, now)
                .SetProperty(u => u.ModifiedBy, _currentUser.UserName), cancellationToken);

        var count = await users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FailedSignInCount)
            .FirstOrDefaultAsync(cancellationToken);

        await TrackedRowReload.ReloadAsync<User>(_context, userId, cancellationToken);
        return new FailedSignInOutcome(count, locked == 1);
    }

    public async Task<bool> RecordSuccessfulSignInAsync(
        long userId,
        DateTime signedInUtc,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.Set<User>()
            .Where(u => u.Id == userId && u.IsActive && !u.IsLocked)
            .ExecuteUpdateAsync(set => set
                .SetProperty(u => u.FailedSignInCount, 0)
                .SetProperty(u => u.LastLoginUtc, signedInUtc)
                .SetProperty(u => u.ModifiedUtc, signedInUtc)
                .SetProperty(u => u.ModifiedBy, _currentUser.UserName), cancellationToken);

        await TrackedRowReload.ReloadAsync<User>(_context, userId, cancellationToken);
        return rows == 1;
    }
}

public sealed class Hl7OutboundClaimStore : IHl7OutboundClaimStore
{
    private readonly BloodBankDbContext _context;
    private readonly ICurrentUser _currentUser;

    public Hl7OutboundClaimStore(BloodBankDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Hl7MessageLog?> TryClaimAsync(
        long messageId,
        Hl7MessageStatus expectedStatus,
        DateTime claimedUtc,
        DateTime staleClaimBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var candidates = _context.Set<Hl7MessageLog>()
            .Where(m => m.Id == messageId && m.Direction == Hl7Direction.Outbound && m.Status == expectedStatus);
        if (expectedStatus == Hl7MessageStatus.Sending)
        {
            candidates = candidates.Where(m => m.ClaimedUtc == null || m.ClaimedUtc < staleClaimBeforeUtc);
        }

        var rows = await candidates.ExecuteUpdateAsync(set => set
            .SetProperty(m => m.Status, Hl7MessageStatus.Sending)
            .SetProperty(m => m.ClaimedUtc, claimedUtc)
            .SetProperty(m => m.ModifiedUtc, claimedUtc)
            .SetProperty(m => m.ModifiedBy, _currentUser.UserName), cancellationToken);

        var tracked = await TrackedRowReload.ReloadAsync<Hl7MessageLog>(_context, messageId, cancellationToken);
        if (rows != 1)
        {
            return null;
        }

        return tracked ?? await _context.Set<Hl7MessageLog>().FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
    }
}

internal static class TrackedRowReload
{
    public static async Task<TEntity?> ReloadAsync<TEntity>(
        BloodBankDbContext context, long id, CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        var entry = context.ChangeTracker.Entries<TEntity>().FirstOrDefault(e => e.Entity.Id == id);
        if (entry is null)
        {
            return null;
        }

        await entry.ReloadAsync(cancellationToken);
        return entry.Entity;
    }
}
