using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ResultLifecycleTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ResultLifecycleTests(SqliteContextFactory factory) => _factory = factory;

    private static ICurrentUser Verifier => new TestCurrentUser("tech-verify", "WORKSTATION-2");

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock, currentUser: _factory.CurrentUser);

    private ResultService Results(
        BloodBankDbContext c,
        ICurrentUser? user = null,
        IPermissionEvaluator? permissions = null)
    {
        var current = user ?? _factory.CurrentUser;
        return new(
            new EfRepository<TestResult>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            c,
            _factory.Clock,
            current,
            new AuditWriter(c, _factory.Clock, current),
            patients: new EfRepository<Patient>(c),
            permissions: permissions ?? new FixedPermissionEvaluator(2));
    }

    private async Task<long> EnsurePatientAsync(string mrn)
    {
        await using var context = _factory.Create();
        var existing = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == mrn);
        if (existing is not null)
        {
            return existing.Id;
        }

        var patient = new Patient
        {
            MedicalRecordNumber = mrn,
            LastName = "Lifecycle",
            FirstName = "Result",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Sex = Sex.Unknown
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        return patient.Id;
    }

    private async Task<long> AccessionAsync(string accession, long patientId)
    {
        await using var context = _factory.Create();
        var result = await Specimens(context).AccessionAsync(
            new AccessionSpecimenRequest(accession, patientId, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(result.Succeeded, result.Error);
        return result.Value!.Id;
    }

    [Fact]
    public async Task ManualEnter_StartsEntered_AndWritesNamedResultAudit()
    {
        var patientId = await EnsurePatientAsync("MRN-LIFECYCLE-MAN");
        var specimenId = await AccessionAsync("ACC-LIFECYCLE-MAN", patientId);

        await using var context = _factory.Create();
        var entered = await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "13.5"));
        Assert.True(entered.Succeeded, entered.Error);
        Assert.Equal(ResultStatus.Entered, entered.Value!.Status);
        Assert.Equal(ResultSource.Manual, entered.Value.Source);

        var audit = await context.AuditEvents
            .Where(a => a.EntityType == nameof(TestResult) && a.EventType == AuditEventType.Result && a.EntityId == entered.Value.Id)
            .SingleAsync();
        Assert.Equal("Result entered from Manual.", audit.Reason);
        Assert.Equal("tech-test", audit.UserName);
        Assert.Equal("WORKSTATION-1", audit.Workstation);
        Assert.Contains("13.5", audit.NewValueJson);
    }

    [Fact]
    public async Task InstrumentEnter_StartsPendingVerification()
    {
        var patientId = await EnsurePatientAsync("MRN-LIFECYCLE-INS");
        var specimenId = await AccessionAsync("ACC-LIFECYCLE-INS", patientId);

        await using var context = _factory.Create();
        var entered = await Results(context).EnterResultAsync(
            new EnterResultRequest(specimenId, "HGB", "12.2", Source: ResultSource.Instrument, SourceReference: "ANALYZER-1"));
        Assert.True(entered.Succeeded, entered.Error);
        Assert.Equal(ResultStatus.PendingVerification, entered.Value!.Status);
        Assert.Equal(ResultSource.Instrument, entered.Value.Source);
        Assert.Equal("ANALYZER-1", entered.Value.SourceReference);
    }

    [Fact]
    public async Task SubmitThenVerify_WritesVerifyAudit_WithOldAndNew()
    {
        var patientId = await EnsurePatientAsync("MRN-LIFECYCLE-SUB");
        var specimenId = await AccessionAsync("ACC-LIFECYCLE-SUB", patientId);
        long resultId;

        await using (var context = _factory.Create())
        {
            resultId = (await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "11.0"))).Value!.Id;
            var submitted = await Results(context).SubmitForVerificationAsync(resultId);
            Assert.True(submitted.Succeeded, submitted.Error);
            Assert.Equal(ResultStatus.PendingVerification, submitted.Value!.Status);
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context, user: Verifier).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
            Assert.Equal(ResultStatus.Verified, verified.Value!.Status);
        }

        await using var verify = _factory.Create();
        var audit = await verify.AuditEvents
            .Where(a => a.EntityType == nameof(TestResult) && a.EntityId == resultId && a.EventType == AuditEventType.Verify)
            .SingleAsync();
        Assert.Contains("PendingVerification", audit.OldValueJson);
        Assert.Contains("Verified", audit.NewValueJson);
        Assert.Equal("Result verified.", audit.Reason);
        Assert.Equal("tech-verify", audit.UserName);
    }

    [Fact]
    public async Task InvalidateVerified_CreatesNewVersion_AndRetainsOriginal()
    {
        var patientId = await EnsurePatientAsync("MRN-LIFECYCLE-INV");
        var specimenId = await AccessionAsync("ACC-LIFECYCLE-INV", patientId);
        long resultId;

        await using (var context = _factory.Create())
        {
            resultId = (await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "9.0"))).Value!.Id;
            await Results(context, user: Verifier).VerifyResultAsync(resultId);
        }

        long invalidatedId;
        await using (var context = _factory.Create())
        {
            var noReason = await Results(context).InvalidateResultAsync(resultId, "  ");
            Assert.False(noReason.Succeeded);

            var invalidated = await Results(context).InvalidateResultAsync(resultId, "Instrument QC failure");
            Assert.True(invalidated.Succeeded, invalidated.Error);
            Assert.Equal(ResultStatus.Invalidated, invalidated.Value!.Status);
            Assert.Equal(2, invalidated.Value.Version);
            Assert.Equal("9.0", invalidated.Value.Value);
            Assert.Equal("Instrument QC failure", invalidated.Value.InvalidationReason);
            invalidatedId = invalidated.Value.Id;
        }

        await using var verify = _factory.Create();
        var original = await verify.TestResults.FindAsync(resultId);
        Assert.Equal(ResultStatus.Verified, original!.Status);
        Assert.Equal("9.0", original.Value);
        Assert.Equal(invalidatedId, original.SupersededByResultId);

        var audit = await verify.AuditEvents
            .Where(a => a.EventType == AuditEventType.Invalidate && a.EntityId == resultId)
            .SingleAsync();
        Assert.Equal("Instrument QC failure", audit.Reason);
        Assert.Contains("Verified", audit.OldValueJson);
    }

    [Fact]
    public async Task Invalidate_WithoutPermission_IsRejected()
    {
        var patientId = await EnsurePatientAsync("MRN-LIFECYCLE-PERM");
        var specimenId = await AccessionAsync("ACC-LIFECYCLE-PERM", patientId);
        long resultId;

        await using (var context = _factory.Create())
        {
            resultId = (await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "8.5"))).Value!.Id;
        }

        await using var act = _factory.Create();
        var denied = await Results(
            act,
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter))
            .InvalidateResultAsync(resultId, "Wrong patient");
        Assert.False(denied.Succeeded);
        Assert.Equal(ResultAuthorizationRule.EvaluateInvalidate(false).Message, denied.Error);
        Assert.Equal(ResultStatus.Entered, (await act.TestResults.FindAsync(resultId))!.Status);
    }

    [Fact]
    public async Task InvalidateUnverifiedCorrection_RestoresPriorVerified()
    {
        var patientId = await EnsurePatientAsync("MRN-LIFECYCLE-REST");
        var specimenId = await AccessionAsync("ACC-LIFECYCLE-REST", patientId);
        long originalId;
        long correctionId;

        await using (var context = _factory.Create())
        {
            originalId = (await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "10.0"))).Value!.Id;
            await Results(context, user: Verifier).VerifyResultAsync(originalId);
            correctionId = (await Results(context).CorrectResultAsync(originalId, "14.0", "Transcription error")).Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var invalidated = await Results(context).InvalidateResultAsync(correctionId, "Correction entered on wrong specimen");
            Assert.True(invalidated.Succeeded, invalidated.Error);
            Assert.Equal(ResultStatus.Invalidated, invalidated.Value!.Status);
        }

        await using var verify = _factory.Create();
        var original = await verify.TestResults.FindAsync(originalId);
        Assert.Equal(ResultStatus.Verified, original!.Status);
        Assert.Null(original.SupersededByResultId);
        Assert.Equal("10.0", original.Value);

        var correction = await verify.TestResults.FindAsync(correctionId);
        Assert.Equal(ResultStatus.Invalidated, correction!.Status);
        Assert.Equal(originalId, correction.SupersededByResultId);
        Assert.True(ResultLifecycleRule.IsCurrentRow(original.SupersededByResultId));
        Assert.False(ResultLifecycleRule.IsCurrentRow(correction.SupersededByResultId));
    }

    [Fact]
    public async Task PatientRecordOpen_WritesPatientAccessAudit()
    {
        var patientId = await EnsurePatientAsync("MRN-LIFECYCLE-ACCESS");
        await using var context = _factory.Create();
        var patients = new PatientService(
            new EfRepository<Patient>(context),
            context,
            _factory.Clock,
            Specimens(context),
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            _factory.CurrentUser);

        await patients.RecordAccessAsync(patientId);

        var audit = await context.AuditEvents
            .Where(a => a.EventType == AuditEventType.PatientAccess && a.EntityId == patientId)
            .SingleAsync();
        Assert.Equal("Patient record opened.", audit.Reason);
        Assert.Equal("tech-test", audit.UserName);
        Assert.Equal("WORKSTATION-1", audit.Workstation);
    }

    [Fact]
    public async Task AboTyped_WithoutSubtests_StaysManual()
    {
        var patientId = await EnsurePatientAsync("MRN-LIFECYCLE-ABO-MAN");
        var specimenId = await AccessionAsync("ACC-LIFECYCLE-ABO-MAN", patientId);

        await using var context = _factory.Create();
        var entered = await Results(context).EnterAboRhAsync(
            new EnterAboRhRequest(specimenId, AboGroup.O, RhType.Positive));
        Assert.True(entered.Succeeded, entered.Error);
        Assert.Equal(ResultSource.Manual, entered.Value!.Source);
        Assert.Equal(ResultStatus.Entered, entered.Value.Status);
    }

    [Fact]
    public async Task AboPanel_StartsCalculated_AndEntered()
    {
        var patientId = await EnsurePatientAsync("MRN-LIFECYCLE-ABO-CALC");
        var specimenId = await AccessionAsync("ACC-LIFECYCLE-ABO-CALC", patientId);

        await using var context = _factory.Create();
        var entered = await Results(context).EnterAboRhAsync(new EnterAboRhRequest(
            specimenId,
            AboGroup.O,
            RhType.Positive,
            new Dictionary<string, string>
            {
                [AboRhPanelSubtestCodes.AntiA] = "0",
                [AboRhPanelSubtestCodes.AntiB] = "0",
                [AboRhPanelSubtestCodes.AntiD] = "3+",
                [AboRhPanelSubtestCodes.ACells] = "4+",
                [AboRhPanelSubtestCodes.BCells] = "4+",
                [AboRhPanelSubtestCodes.Control] = "0"
            }));
        Assert.True(entered.Succeeded, entered.Error);
        Assert.Equal(ResultSource.Calculated, entered.Value!.Source);
        Assert.Equal(ResultStatus.Entered, entered.Value.Status);
        Assert.Contains("Result entered from Calculated.", (await context.AuditEvents
            .Where(a => a.EntityType == nameof(TestResult) && a.EntityId == entered.Value.Id && a.EventType == AuditEventType.Result)
            .SingleAsync()).Reason);
    }

    [Fact]
    public async Task InstrumentAboPanel_KeepsInstrumentSource()
    {
        var patientId = await EnsurePatientAsync("MRN-LIFECYCLE-ABO-INS");
        var specimenId = await AccessionAsync("ACC-LIFECYCLE-ABO-INS", patientId);

        await using var context = _factory.Create();
        var entered = await Results(context).EnterAboRhAsync(new EnterAboRhRequest(
            specimenId,
            AboGroup.O,
            RhType.Positive,
            new Dictionary<string, string>
            {
                [AboRhPanelSubtestCodes.AntiA] = "0",
                [AboRhPanelSubtestCodes.AntiB] = "0",
                [AboRhPanelSubtestCodes.AntiD] = "3+",
                [AboRhPanelSubtestCodes.ACells] = "4+",
                [AboRhPanelSubtestCodes.BCells] = "4+"
            },
            Source: ResultSource.Instrument,
            SourceReference: "ANALYZER-ABO"));
        Assert.True(entered.Succeeded, entered.Error);
        Assert.Equal(ResultSource.Instrument, entered.Value!.Source);
        Assert.Equal(ResultStatus.PendingVerification, entered.Value.Status);
        Assert.Equal("ANALYZER-ABO", entered.Value.SourceReference);
    }
}
