using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-358 (OCD-049): a correction or withdrawal of a verified result reaches the EHR without anyone asking.
public class ResultEhrUpdateTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static ICurrentUser Verifier => new TestCurrentUser("tech-verify", "WORKSTATION-2");

    private Hl7OutboundService Outbound(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Patient>(c), new EfRepository<Hl7MessageLog>(c), c, _factory.Clock,
            endpoints: new EfRepository<InterfaceEndpoint>(c));

    private ResultService Results(BloodBankDbContext c, ICurrentUser? user = null, IResultReleasePublisher? releases = null)
    {
        var current = user ?? _factory.CurrentUser;
        return new(
            new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, current, new AuditWriter(c, _factory.Clock, current),
            patients: new EfRepository<Patient>(c), permissions: new FixedPermissionEvaluator(2),
            releases: releases ?? Outbound(c));
    }

    private async Task<long> VerifiedAboRhAsync(string key)
    {
        await using var c = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = "MRN-" + key, LastName = "Ehr", FirstName = "Update", DateOfBirth = new DateOnly(1985, 4, 4), Sex = Sex.Unknown };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var specimen = await new SpecimenService(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
                c, _factory.Clock, currentUser: _factory.CurrentUser)
            .AccessionAsync(new AccessionSpecimenRequest("ACC-" + key, patient.Id, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(specimen.Succeeded, specimen.Error);
        var entered = await Results(c).EnterAboRhAsync(new EnterAboRhRequest(specimen.Value!.Id, AboGroup.A, RhType.Positive));
        Assert.True(entered.Succeeded, entered.Error);
        Assert.True((await Results(c, Verifier).VerifyResultAsync(entered.Value!.Id)).Succeeded);
        return entered.Value.Id;
    }

    private async Task EnableResultsInterfaceAsync()
    {
        await using var c = _factory.Create();
        c.InterfaceEndpoints.Add(new InterfaceEndpoint
        {
            Name = "EHR results", Direction = Hl7Direction.Outbound, InterfaceType = InterfaceType.Results,
            MessageTypes = "ORU", IsEnabled = true
        });
        await c.SaveChangesAsync();
    }

    private async Task<int> OutboundOruCountAsync()
    {
        await using var c = _factory.Create();
        return await c.Hl7Messages.CountAsync(m => m.Direction == Hl7Direction.Outbound && m.MessageType == "ORU");
    }

    [Fact]
    public async Task InvalidatingAVerifiedResult_QueuesAWithdrawal_WhenAResultsInterfaceIsEnabled()
    {
        await EnableResultsInterfaceAsync();
        var resultId = await VerifiedAboRhAsync("EHR-W");
        await using (var c = _factory.Create())
        {
            var r = await Results(c).InvalidateResultAsync(resultId, "Wrong blood in tube");
            Assert.True(r.Succeeded, r.Error);
            Assert.DoesNotContain(r.Warnings, w => w.Code == ResultReleaseRule.NotSentCode);
        }

        await using var v = _factory.Create();
        var message = Assert.Single(await v.Hl7Messages.Where(m => m.Direction == Hl7Direction.Outbound && m.MessageType == "ORU").ToListAsync());
        var obx = message.RawMessage.Split('\r', StringSplitOptions.RemoveEmptyEntries).Single(s => s.StartsWith("OBX|", StringComparison.Ordinal));
        Assert.Equal("W", obx.Split('|')[11]);
    }

    [Fact]
    public async Task VerifyingACorrection_QueuesACorrection()
    {
        await EnableResultsInterfaceAsync();
        var resultId = await VerifiedAboRhAsync("EHR-C");
        long correctionId;
        await using (var c = _factory.Create())
        {
            var corrected = await Results(c).CorrectResultAsync(resultId, "O Positive", "Transcription error");
            Assert.True(corrected.Succeeded, corrected.Error);
            correctionId = corrected.Value!.Id;
            Assert.Equal(0, await c.Hl7Messages.CountAsync(m => m.MessageType == "ORU"));
        }

        await using (var c = _factory.Create())
        {
            var verified = await Results(c, Verifier).VerifyResultAsync(correctionId);
            Assert.True(verified.Succeeded, verified.Error ?? string.Join("; ", verified.Evaluation?.HardStops.Select(h => h.Message) ?? []));
        }

        await using var v = _factory.Create();
        var message = Assert.Single(await v.Hl7Messages.Where(m => m.MessageType == "ORU").ToListAsync());
        Assert.Equal("C", message.RawMessage.Split('\r', StringSplitOptions.RemoveEmptyEntries).Single(s => s.StartsWith("OBX|", StringComparison.Ordinal)).Split('|')[11]);
    }

    [Fact]
    public async Task WithoutAnEnabledResultsInterface_NothingIsQueued()
    {
        var resultId = await VerifiedAboRhAsync("EHR-NONE");
        await using (var c = _factory.Create())
        {
            Assert.True((await Results(c).InvalidateResultAsync(resultId, "Wrong blood in tube")).Succeeded);
        }

        Assert.Equal(0, await OutboundOruCountAsync());
    }

    [Fact]
    public async Task WhenQueueingFails_TheInvalidationStands_AndAWarningSaysToSendByHand()
    {
        var resultId = await VerifiedAboRhAsync("EHR-FAIL");
        await using var c = _factory.Create();
        var r = await Results(c, releases: new FailingPublisher()).InvalidateResultAsync(resultId, "Wrong blood in tube");
        Assert.True(r.Succeeded, r.Error);
        Assert.Contains(r.Warnings, w => w.Code == ResultReleaseRule.NotSentCode);
        Assert.Equal(ResultStatus.Invalidated, (await c.TestResults.AsNoTracking().SingleAsync(x => x.SupersededByResultId == null && x.Status == ResultStatus.Invalidated && x.TestCode == "ABORH")).Status);
    }

    private sealed class FailingPublisher : IResultReleasePublisher
    {
        public Task<long?> PublishResultChangeAsync(long resultId, CancellationToken ct = default) =>
            throw new InvalidOperationException("Interface database unavailable.");
    }
}
