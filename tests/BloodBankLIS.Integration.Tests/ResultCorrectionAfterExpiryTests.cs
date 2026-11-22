using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-366: a verified result can still be corrected, and the correction verified, after the specimen expires.
public class ResultCorrectionAfterExpiryTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static ICurrentUser Verifier => new TestCurrentUser("tech-verify", "WORKSTATION-2");

    private ResultService Results(BloodBankDbContext c, ICurrentUser? user = null)
    {
        var current = user ?? _factory.CurrentUser;
        return new(
            new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, current, new AuditWriter(c, _factory.Clock, current),
            patients: new EfRepository<Patient>(c), permissions: new FixedPermissionEvaluator(2));
    }

    private async Task<(long SpecimenId, long ResultId)> AboRhAsync(string key, bool verify)
    {
        await using var c = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = "MRN-" + key, LastName = "Expiry", FirstName = "Correction", DateOfBirth = new DateOnly(1979, 5, 5), Sex = Sex.Unknown };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        var specimen = await new SpecimenService(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
                c, _factory.Clock, currentUser: _factory.CurrentUser)
            .AccessionAsync(new AccessionSpecimenRequest("ACC-" + key, patient.Id, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(specimen.Succeeded, specimen.Error);
        var entered = await Results(c).EnterAboRhAsync(new EnterAboRhRequest(specimen.Value!.Id, AboGroup.A, RhType.Positive));
        Assert.True(entered.Succeeded, entered.Error);
        if (verify)
        {
            Assert.True((await Results(c, Verifier).VerifyResultAsync(entered.Value!.Id)).Succeeded);
        }

        return (specimen.Value.Id, entered.Value!.Id);
    }

    private async Task SetSpecimenAsync(long specimenId, SpecimenStatus status)
    {
        await using var c = _factory.Create();
        var specimen = await c.Specimens.SingleAsync(s => s.Id == specimenId);
        specimen.ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-10);
        specimen.Status = status;
        await c.SaveChangesAsync();
    }

    [Fact]
    public async Task CorrectionOfAVerifiedResult_CanBeVerified_AfterTheSpecimenExpires()
    {
        var (specimenId, resultId) = await AboRhAsync("CORR-EXP", verify: true);
        await SetSpecimenAsync(specimenId, SpecimenStatus.Expired);

        long correctionId;
        await using (var c = _factory.Create())
        {
            var corrected = await Results(c).CorrectResultAsync(resultId, "O Positive", "Transcription error");
            Assert.True(corrected.Succeeded, corrected.Error);
            correctionId = corrected.Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var verified = await Results(c, Verifier).VerifyResultAsync(correctionId);
            Assert.True(verified.Succeeded, verified.Error ?? string.Join("; ", verified.Evaluation?.HardStops.Select(h => h.Message) ?? []));
        }

        await using var v = _factory.Create();
        Assert.Equal(ResultStatus.Verified, (await v.TestResults.SingleAsync(r => r.Id == correctionId)).Status);
    }

    // TEST-BB-367: a verified result can still be invalidated after its specimen expires.
    [Fact]
    public async Task VerifiedResult_CanBeInvalidated_AfterTheSpecimenExpires()
    {
        var (specimenId, resultId) = await AboRhAsync("INV-EXP", verify: true);
        await SetSpecimenAsync(specimenId, SpecimenStatus.Expired);

        await using var c = _factory.Create();
        var invalidated = await Results(c).InvalidateResultAsync(resultId, "Wrong blood in tube");
        Assert.True(invalidated.Succeeded, invalidated.Error);
        Assert.Equal(ResultStatus.Invalidated, invalidated.Value!.Status);
    }

    [Fact]
    public async Task APendingFirstResult_IsStillNotVerified_OnAnExpiredSpecimen()
    {
        var (specimenId, resultId) = await AboRhAsync("FIRST-EXP", verify: false);
        await SetSpecimenAsync(specimenId, SpecimenStatus.Accepted);

        await using var c = _factory.Create();
        var verified = await Results(c, Verifier).VerifyResultAsync(resultId);
        Assert.False(verified.Succeeded);
        Assert.Contains("has expired", verified.Error);
    }

    [Fact]
    public async Task CorrectionOnARejectedSpecimen_IsStillRefusedAtVerification()
    {
        var (specimenId, resultId) = await AboRhAsync("CORR-REJ", verify: true);
        long correctionId;
        await using (var c = _factory.Create())
        {
            var corrected = await Results(c).CorrectResultAsync(resultId, "O Positive", "Transcription error");
            Assert.True(corrected.Succeeded, corrected.Error);
            correctionId = corrected.Value!.Id;
        }

        await SetSpecimenAsync(specimenId, SpecimenStatus.Rejected);
        await using var v = _factory.Create();
        var verified = await Results(v, Verifier).VerifyResultAsync(correctionId);
        Assert.False(verified.Succeeded);
        Assert.Contains("Rejected", verified.Error);
    }
}
