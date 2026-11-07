using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-497: batch verification (OCD-061) is off unless the facility turns it on; once on, only eligible results are
// verified (each through the normal verification), the rest are left with the reason, and each verified one is
// audited with the batch id.
public class BatchVerifyServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();
    private readonly ICurrentUser _verifier = new StaticCurrentUser("tech2");

    public void Dispose() => _factory.Dispose();

    private BatchVerifyService Service(BloodBankDbContext c)
    {
        var audit = new AuditWriter(c, _factory.Clock, _verifier);
        var results = new ResultService(
            new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _verifier, audit,
            exceptionDefinitions: new EfRepository<ExceptionDefinition>(c),
            overrides: new EfRepository<Override>(c),
            permissions: new FixedPermissionEvaluator(2));
        var worklist = new TestWorklistService(
            new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Specimen>(c), new EfRepository<Patient>(c),
            new EfRepository<PatientBloodTypeHistory>(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<TestResult>(c), new EfRepository<TestDefinition>(c), new EfRepository<SpecimenTypeDefinition>(c),
            _factory.Clock);
        return new BatchVerifyService(
            results, worklist, new EfRepository<TestResult>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c), new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<SpecialRequirementDefinition>(c), new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            _verifier, _factory.Clock, audit, c, new EfRepository<Patient>(c));
    }

    private async Task<(long PatientId, long SpecimenId)> PatientAsync(BloodBankDbContext c, string key)
    {
        var patient = new Patient { MedicalRecordNumber = $"MRN-BV-{key}", LastName = "Batch", FirstName = key, DateOfBirth = new DateOnly(1980, 1, 1), Sex = Sex.Unknown };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var specimens = new SpecimenService(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c), c, _factory.Clock);
        var specimen = await specimens.AccessionAsync(new AccessionSpecimenRequest($"ACC-BV-{key}", patient.Id, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(specimen.Succeeded, specimen.Error);
        return (patient.Id, specimen.Value!.Id);
    }

    private static TestResult Posted((long PatientId, long SpecimenId) p, string test, string value, string enteredBy = "tech1") => new()
    {
        PatientId = p.PatientId, SpecimenId = p.SpecimenId, TestCode = test, Value = value, Version = 1,
        Status = ResultStatus.PendingVerification, Source = ResultSource.Instrument, EnteredBy = enteredBy, EnteredUtc = DateTime.UtcNow.AddMinutes(-30)
    };

    [Fact]
    public async Task OnlyEligibleResults_AreVerified_AndEachIsAuditedWithTheBatch()
    {
        await using var c = _factory.Create();
        var typed = await PatientAsync(c, "TYPED");
        var newPatient = await PatientAsync(c, "NEW");
        var withAntibody = await PatientAsync(c, "AB");
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory { PatientId = typed.PatientId, Abo = AboGroup.O, RhD = RhType.Positive, IsCurrent = true, Source = BloodTypeSource.HistoricalImport });
        c.AntibodyHistory.Add(new AntibodyHistory { PatientId = withAntibody.PatientId, AntibodySpecificity = "Anti-K" });
        var matching = Posted(typed, "ABORH", AboRhResultValue.Format(AboGroup.O, RhType.Positive));
        var differs = Posted(typed, "ABORH", AboRhResultValue.Format(AboGroup.A, RhType.Positive));
        var firstType = Posted(newPatient, "ABORH", AboRhResultValue.Format(AboGroup.O, RhType.Positive));
        var negativeScreen = Posted(newPatient, "ABSC", "Negative");
        var positiveScreen = Posted(newPatient, "ABSC", "Positive");
        var antibodyPatientDat = Posted(withAntibody, "DAT", "Negative");
        var ownEntry = Posted(newPatient, "DAT", "Negative", enteredBy: "tech2");
        c.TestResults.AddRange(matching, differs, firstType, negativeScreen, positiveScreen, antibodyPatientDat, ownEntry);
        await c.SaveChangesAsync();
        long[] all = [matching.Id, differs.Id, firstType.Id, negativeScreen.Id, positiveScreen.Id, antibodyPatientDat.Id, ownEntry.Id];

        // Off by default: nothing is verified.
        var off = await Service(c).VerifyAsync(new BatchVerifyRequest(all));
        Assert.False(off.Succeeded);
        Assert.False((await Service(c).ListCandidatesAsync()).Enabled);
        Assert.Equal(ResultStatus.PendingVerification, (await c.TestResults.FindAsync(matching.Id))!.Status);

        c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.AllowBatchVerify, Value = "true", Description = "test" });
        await c.SaveChangesAsync();
        var outcome = await Service(c).VerifyAsync(new BatchVerifyRequest(all));
        Assert.True(outcome.Succeeded, outcome.Error);

        Assert.Equal([matching.Id, negativeScreen.Id], outcome.Value!.Verified.Order());
        var reasons = outcome.Value.Skipped.ToDictionary(s => s.ResultId, s => s.Reason);
        Assert.Contains("differs", reasons[differs.Id]);
        Assert.Contains("first type", reasons[firstType.Id]);
        Assert.Contains("Negative", reasons[positiveScreen.Id]);
        Assert.Contains("antibody history", reasons[antibodyPatientDat.Id]);
        Assert.Contains("another person", reasons[ownEntry.Id]);
        // Each one left unverified is named by test and patient, not only by its internal id.
        Assert.Equal("ABSC for Batch, NEW (MRN-BV-NEW)", outcome.Value.Skipped.Single(s => s.ResultId == positiveScreen.Id).Label);

        await using var check = _factory.Create();
        Assert.Equal(ResultStatus.Verified, (await check.TestResults.FindAsync(matching.Id))!.Status);
        Assert.Equal("tech2", (await check.TestResults.FindAsync(negativeScreen.Id))!.VerifiedBy);
        Assert.Equal(ResultStatus.PendingVerification, (await check.TestResults.FindAsync(differs.Id))!.Status);
        Assert.Equal(ResultStatus.PendingVerification, (await check.TestResults.FindAsync(ownEntry.Id))!.Status);
        var batchRows = await check.AuditEvents
            .Where(e => e.EntityType == nameof(TestResult) && e.Reason == $"Verified in batch {outcome.Value.BatchId}.")
            .Select(e => e.EntityId)
            .ToListAsync();
        Assert.Equal([matching.Id, negativeScreen.Id], batchRows.OfType<long>().Order());
    }
}
