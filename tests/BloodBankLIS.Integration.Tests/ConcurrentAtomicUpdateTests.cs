using BloodBankLIS.Application.Identity;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Security.Signatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Fires parallel attempts, each on its own DbContext and connection, at a file-backed
/// SQLite database to prove the check-then-set races are closed: a single-use signature
/// is consumed once, failed sign-ins never lose a count, and an outbound HL7 message is
/// transmitted once even when the background sender and manual sends overlap.
/// </summary>
public sealed class ConcurrentAtomicUpdateTests : IDisposable
{
    private const int Attempts = 20;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "bb-atomic-" + Guid.NewGuid().ToString("N"));
    private readonly string _connectionString;

    private FixedClock Clock { get; } = new(new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));

    private TestCurrentUser CurrentUser { get; } = new("tech-test", "WORKSTATION-1");

    public ConcurrentAtomicUpdateTests()
    {
        Directory.CreateDirectory(_root);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_root, "atomic.db"),
            DefaultTimeout = 60
        }.ToString();

        using var context = Create();
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort.
        }
    }

    private BloodBankDbContext Create() =>
        new(new DbContextOptionsBuilder<BloodBankDbContext>().UseSqlite(_connectionString).Options, Clock, CurrentUser);

    private static async Task<T[]> RunInParallelAsync<T>(Func<int, Task<T>> attempt)
    {
        using var gate = new SemaphoreSlim(0);
        var tasks = Enumerable.Range(0, Attempts)
            .Select(i => Task.Run(async () =>
            {
                await gate.WaitAsync();
                return await attempt(i);
            }))
            .ToArray();
        gate.Release(Attempts);
        return await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ParallelConsume_ConsumesSignatureExactlyOnce()
    {
        long signatureId;
        await using (var setup = Create())
        {
            var user = new User { UserName = CurrentUser.UserName, DisplayName = "Tech Test", IsActive = true };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            var signature = new ElectronicSignature
            {
                UserId = user.Id,
                Action = "IssueOverride",
                MeaningOfSignature = "Override approved",
                SignedUtc = Clock.UtcNow,
                SignatureHash = "test",
                ExpiresUtc = Clock.UtcNow.AddMinutes(15)
            };
            setup.ElectronicSignatures.Add(signature);
            await setup.SaveChangesAsync();
            signatureId = signature.Id;
        }

        var results = await RunInParallelAsync(async _ =>
        {
            await using var c = Create();
            var service = new SignatureService(
                new EfRepository<User>(c),
                new EfRepository<ElectronicSignature>(c),
                new SignatureConsumptionStore(c, CurrentUser),
                c,
                Clock,
                CurrentUser,
                audit: new AuditWriter(c, Clock, CurrentUser, new StaticEnvironmentInfo("Development", isDevMode: false)));
            return await service.ConsumeAsync(signatureId);
        });

        Assert.Equal(1, results.Count(r => r));

        await using var verify = Create();
        Assert.NotNull((await verify.ElectronicSignatures.SingleAsync(s => s.Id == signatureId)).ConsumedUtc);
        Assert.Equal(1, await verify.AuditEvents.CountAsync(e =>
            e.EntityType == nameof(ElectronicSignature) && e.EntityId == signatureId && e.EventType == AuditEventType.Update));
    }

    [Fact]
    public async Task ParallelFailedSignIns_CountEveryAttemptAndLockOnce()
    {
        long userId;
        await using (var setup = Create())
        {
            var user = new User { UserName = "lockme", DisplayName = "Lock Me", IsActive = true };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            userId = user.Id;
        }

        var outcomes = await RunInParallelAsync(async _ =>
        {
            await using var c = Create();
            return await new UserSignInStore(c, Clock, CurrentUser)
                .RecordFailedSignInAsync(userId, AuthSessionService.FailedSignInLockoutThreshold);
        });

        Assert.Equal(1, outcomes.Count(o => o.LockedNow));

        await using var verify = Create();
        var stored = await verify.Users.SingleAsync(u => u.Id == userId);
        Assert.Equal(Attempts, stored.FailedSignInCount);
        Assert.True(stored.IsLocked);
    }

    [Fact]
    public async Task ParallelBackgroundAndManualSends_TransmitMessageExactlyOnce()
    {
        var dropFolder = Path.Combine(_root, "out");
        long messageId;
        await using (var setup = Create())
        {
            setup.InterfaceEndpoints.Add(new InterfaceEndpoint
            {
                Name = "ORU file out",
                Direction = Hl7Direction.Outbound,
                Transport = InterfaceTransport.File,
                InterfaceType = InterfaceType.Results,
                Path = dropFolder,
                IsEnabled = true,
                MessageTypes = "ORU"
            });
            var log = OutboundMessage("RACE-1", Hl7MessageStatus.Received);
            setup.Hl7Messages.Add(log);
            await setup.SaveChangesAsync();
            messageId = log.Id;
        }

        // Even attempts are the background loop, odd ones a user pressing "Send".
        var sent = await RunInParallelAsync(async i =>
        {
            await using var c = Create();
            var sender = Sender(c);
            return i % 2 == 0
                ? await sender.SendPendingAsync()
                : (await sender.SendOneAsync(messageId)).Succeeded ? 1 : 0;
        });

        Assert.Equal(1, sent.Sum());
        Assert.Single(Directory.GetFiles(dropFolder, "*.hl7"));

        await using var verify = Create();
        var row = await verify.Hl7Messages.SingleAsync(m => m.Id == messageId);
        Assert.Equal(Hl7MessageStatus.Acked, row.Status);
        Assert.Equal(1, row.RetryCount);
    }

    [Fact]
    public async Task StaleSendingClaim_IsReclaimed_FreshClaimIsLeftAlone()
    {
        var dropFolder = Path.Combine(_root, "stale");
        long staleId;
        long freshId;
        await using (var setup = Create())
        {
            setup.InterfaceEndpoints.Add(new InterfaceEndpoint
            {
                Name = "ORU file out",
                Direction = Hl7Direction.Outbound,
                Transport = InterfaceTransport.File,
                InterfaceType = InterfaceType.Results,
                Path = dropFolder,
                IsEnabled = true,
                MessageTypes = "ORU"
            });
            var stale = OutboundMessage("STALE-1", Hl7MessageStatus.Sending);
            stale.ClaimedUtc = Clock.UtcNow - Hl7OutboundSender.ClaimTimeout - TimeSpan.FromMinutes(1);
            var fresh = OutboundMessage("FRESH-1", Hl7MessageStatus.Sending);
            fresh.ClaimedUtc = Clock.UtcNow.AddSeconds(-10);
            setup.Hl7Messages.AddRange(stale, fresh);
            await setup.SaveChangesAsync();
            staleId = stale.Id;
            freshId = fresh.Id;
        }

        await using (var c = Create())
        {
            var sender = Sender(c);
            Assert.Equal(1, await sender.SendPendingAsync());
            Assert.False((await sender.SendOneAsync(freshId)).Succeeded);
        }

        await using var verify = Create();
        Assert.Equal(Hl7MessageStatus.Acked, (await verify.Hl7Messages.SingleAsync(m => m.Id == staleId)).Status);
        Assert.Equal(Hl7MessageStatus.Sending, (await verify.Hl7Messages.SingleAsync(m => m.Id == freshId)).Status);
    }

    private Hl7MessageLog OutboundMessage(string controlId, Hl7MessageStatus status) => new()
    {
        Direction = Hl7Direction.Outbound,
        MessageType = "ORU",
        TriggerEvent = "R01",
        MessageControlId = controlId,
        RawMessage = $"MSH|^~\\&|BBLIS|LAB|EHR|HOSP|20260530120000||ORU^R01|{controlId}|P|2.5\rPID|1||MRN1",
        Status = status,
        ReceivedUtc = Clock.UtcNow
    };

    private Hl7OutboundSender Sender(BloodBankDbContext c) => new(
        new EfRepository<Hl7MessageLog>(c),
        new EfRepository<InterfaceEndpoint>(c),
        new EfRepository<InterfaceErrorQueueItem>(c),
        new Hl7OutboundClaimStore(c, CurrentUser),
        c,
        Clock);
}
