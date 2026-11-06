using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class PatientTestHistoryTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public PatientTestHistoryTests(SqliteContextFactory factory) => _factory = factory;

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c), c, _factory.Clock);

    private ResultService Results(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            exceptionDefinitions: new EfRepository<ExceptionDefinition>(c),
            overrides: new EfRepository<Override>(c),
            permissions: new FixedPermissionEvaluator(2));

    private static PatientTestHistoryService History(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c),
            new EfRepository<Order>(c), new EfRepository<TestDefinition>(c));

    private async Task<(long PatientId, long SpecimenId, string Accession)> SeedPatientWithSpecimenAsync(string suffix)
    {
        await using var context = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-TH-{suffix}",
            LastName = "History",
            FirstName = "Test",
            DateOfBirth = new DateOnly(1985, 2, 2),
            Sex = Sex.Unknown
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var accession = $"ACC-TH-{suffix}";
        var result = await Specimens(context).AccessionAsync(
            new AccessionSpecimenRequest(accession, patient.Id, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(result.Succeeded);
        return (patient.Id, result.Value!.Id, accession);
    }

    [Fact]
    public async Task List_IncludesVerifiedCurrentResult_WithAccessionAndOrder()
    {
        var (patientId, specimenId, accession) = await SeedPatientWithSpecimenAsync("VER");
        const string orderNumber = "ORD-TH-VER";

        await using (var context = _factory.Create())
        {
            context.TestDefinitions.Add(new TestDefinition
            {
                Code = "HGB",
                Name = "Hemoglobin",
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = _factory.Clock.UtcNow
            });
            var location = new OrderingLocation { Code = $"LOC-TH-{Guid.NewGuid():N}", Name = "Lab", IsActive = true };
            context.OrderingLocations.Add(location);
            await context.SaveChangesAsync();

            var encounter = new Encounter
            {
                PatientId = patientId,
                VisitNumber = $"VIS-TH-{Guid.NewGuid():N}",
                EncounterType = EncounterType.Inpatient,
                Status = EncounterStatus.Active,
                AdmitUtc = _factory.Clock.UtcNow.AddDays(-1)
            };
            context.Encounters.Add(encounter);
            await context.SaveChangesAsync();

            var order = new Order
            {
                OrderNumber = orderNumber,
                PatientId = patientId,
                EncounterId = encounter.Id,
                OrderingLocationId = location.Id,
                OrderCategory = OrderCategory.Test,
                Priority = OrderPriority.Routine,
                OrderedUtc = _factory.Clock.UtcNow,
                Status = OrderStatus.InProcess
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var entered = await Results(context).EnterResultAsync(
                new EnterResultRequest(specimenId, "HGB", "13.5", order.Id, "g/dL"));
            Assert.True(entered.Succeeded);
            var verified = await Results(context).VerifyResultAsync(entered.Value!.Id);
            Assert.True(verified.Succeeded);
        }

        await using (var context = _factory.Create())
        {
            var rows = await History(context).ListByPatientAsync(patientId);
            var row = Assert.Single(rows);
            Assert.Equal("HGB", row.TestCode);
            Assert.Equal("Hemoglobin", row.TestName);
            Assert.Equal("13.5", row.Value);
            Assert.Equal(accession, row.AccessionNumber);
            Assert.Equal(orderNumber, row.OrderNumber);
            Assert.Equal(_factory.CurrentUser.UserName, row.VerifiedBy);
            Assert.True(row.IsCurrent);
            Assert.Equal(ResultStatus.Verified, row.Status);
        }
    }

    [Fact]
    public async Task List_OmitsNeverVerified_AndIncludesSupersededVerified()
    {
        var (patientId, specimenId, _) = await SeedPatientWithSpecimenAsync("HID");
        long verifiedId;

        await using (var context = _factory.Create())
        {
            var enteredOnly = await Results(context).EnterResultAsync(
                new EnterResultRequest(specimenId, "HCT", "38"));
            Assert.True(enteredOnly.Succeeded);

            var toVerify = await Results(context).EnterResultAsync(
                new EnterResultRequest(specimenId, "HGB", "9.0"));
            Assert.True(toVerify.Succeeded);
            verifiedId = toVerify.Value!.Id;
            Assert.True((await Results(context).VerifyResultAsync(verifiedId)).Succeeded);
        }

        await using (var context = _factory.Create())
        {
            Assert.True((await Results(context).CorrectResultAsync(verifiedId, "10.0", "Transcription error")).Succeeded);
        }

        await using (var context = _factory.Create())
        {
            var rows = await History(context).ListByPatientAsync(patientId);
            var row = Assert.Single(rows);
            Assert.Equal("HGB", row.TestCode);
            Assert.Equal("9.0", row.Value);
            Assert.False(row.IsCurrent);
            Assert.Equal(ResultStatus.Verified, row.Status);
            Assert.Equal("Transcription error", row.Reason);
        }
    }

    // TEST-BB-485: rows say who entered the result and how; the revised view keeps every version of a corrected result
    // together, newest first, and leaves unchanged results out.
    [Fact]
    public async Task Rows_NameTheEnterer_AndRevisedOnlyGroupsTheCorrectionChain()
    {
        var (patientId, specimenId, _) = await SeedPatientWithSpecimenAsync("REV");
        long hgbId;
        await using (var context = _factory.Create())
        {
            var hgb = await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "9.0"));
            var hct = await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HCT", "38"));
            Assert.True(hgb.Succeeded && hct.Succeeded);
            hgbId = hgb.Value!.Id;
            Assert.True((await Results(context).VerifyResultAsync(hgbId)).Succeeded);
            Assert.True((await Results(context).VerifyResultAsync(hct.Value!.Id)).Succeeded);
        }

        await using (var context = _factory.Create())
        {
            var corrected = await Results(context).CorrectResultAsync(hgbId, "10.0", "Transcription error");
            Assert.True(corrected.Succeeded, corrected.Error);
            var reverified = await Results(context).VerifyResultAsync(corrected.Value!.Id);
            Assert.True(reverified.Succeeded, reverified.Error);
        }

        await using (var context = _factory.Create())
        {
            var rows = await History(context).ListByPatientAsync(patientId);
            Assert.All(rows, r => Assert.Equal(_factory.CurrentUser.UserName, r.EnteredBy));
            Assert.All(rows, r => Assert.Equal("Manual", PatientTestHistoryView.SourceLabel(r.Source)));
            Assert.False(Assert.Single(rows, r => r.TestCode == "HCT").HasRevision);

            var revised = PatientTestHistoryView.RevisedOnly(rows);
            Assert.Equal(2, revised.Count);
            Assert.All(revised, r => Assert.Equal("HGB", r.TestCode));
            Assert.Equal(["10.0", "9.0"], revised.Select(r => r.Value));
            Assert.True(revised[0].IsCurrent);
            Assert.False(revised[1].IsCurrent);
        }
    }
}
