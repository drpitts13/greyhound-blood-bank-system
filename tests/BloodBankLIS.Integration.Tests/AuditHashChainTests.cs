using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AuditHashChainTests
{
    private static Patient NewPatient(string mrn) => new()
    {
        MedicalRecordNumber = mrn,
        LastName = "Chain",
        FirstName = "Audit",
        DateOfBirth = new DateOnly(1975, 6, 15),
        Sex = Sex.Male
    };

    [Fact]
    public async Task Create_StampsGenesisHash_AndChainsTheNextRow()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.Create();

        context.Patients.Add(NewPatient("MRN-CHAIN-1"));
        await context.SaveChangesAsync();
        context.Patients.Add(NewPatient("MRN-CHAIN-2"));
        await context.SaveChangesAsync();

        var events = await context.AuditEvents.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        var created = events.Where(a => a.EntityType == nameof(Patient)).ToList();
        Assert.Equal(2, created.Count);
        Assert.Equal(created[0].RecordHash, created[1].PreviousHash);
        Assert.Equal(AuditHashChainRule.ComputeRecordHash(created[0]), created[0].RecordHash);
        Assert.Equal(AuditHashChainRule.OkCode, AuditHashChainRule.Verify(events).Code);
        Assert.Equal(1, events.Count(a => a.PreviousHash == AuditHashChainRule.GenesisPreviousHash));
    }

    [Fact]
    public async Task NamedAndInterceptorEvents_InOneSave_ShareOneChain()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.Create();

        var seederMaxId = await context.AuditEvents.MaxAsync(a => (long?)a.Id) ?? 0;

        context.AuditEvents.Add(new AuditEvent
        {
            EventType = AuditEventType.Issue,
            EntityType = nameof(BloodUnit),
            EntityId = 3,
            UserName = "tech-test",
            OccurredUtc = factory.Clock.UtcNow,
            Reason = "Named"
        });
        context.Patients.Add(NewPatient("MRN-CHAIN-NAMED"));
        await context.SaveChangesAsync();

        var events = await context.AuditEvents.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        var added = events.Where(a => a.Id > seederMaxId).ToList();
        Assert.Equal(2, added.Count);
        Assert.Equal(added[0].RecordHash, added[1].PreviousHash);
        Assert.Equal(AuditHashChainRule.OkCode, AuditHashChainRule.Verify(events).Code);
    }

    [Fact]
    public async Task RawSqlPayloadEdit_FailsVerify()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.Create();
        context.Patients.Add(NewPatient("MRN-CHAIN-TAMPER"));
        await context.SaveChangesAsync();

        var id = await context.AuditEvents
            .Where(a => a.EntityType == nameof(Patient))
            .Select(a => a.Id)
            .SingleAsync();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE AuditEvents SET NewValueJson = '{{}}' WHERE Id = {0}", id);

        context.ChangeTracker.Clear();
        var events = await context.AuditEvents.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(AuditHashChainRule.PayloadCode, AuditHashChainRule.Verify(events).Code);
    }

    [Fact]
    public async Task RawSqlDelete_FailsVerify()
    {
        using var factory = new SqliteContextFactory();
        await using var context = factory.Create();
        context.Patients.Add(NewPatient("MRN-CHAIN-DEL-1"));
        await context.SaveChangesAsync();
        context.Patients.Add(NewPatient("MRN-CHAIN-DEL-2"));
        await context.SaveChangesAsync();

        var firstPatientId = await context.AuditEvents
            .Where(a => a.EntityType == nameof(Patient))
            .OrderBy(a => a.Id)
            .Select(a => a.Id)
            .FirstAsync();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM AuditEvents WHERE Id = {0}", firstPatientId);

        context.ChangeTracker.Clear();
        var events = await context.AuditEvents.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(AuditHashChainRule.LinkCode, AuditHashChainRule.Verify(events).Code);
    }
}
