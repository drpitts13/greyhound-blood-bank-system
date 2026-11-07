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
        Assert.Contains("Manual", audit.OldValueJson);
        Assert.Contains("Manual", audit.NewValueJson);
        Assert.Equal("Result verified.", audit.Reason);
        Assert.Equal("tech-verify", audit.UserName);

        var submittedAudit = await verify.AuditEvents
            .Where(a => a.EntityType == nameof(TestResult) && a.EntityId == resultId && a.EventType == AuditEventType.Result)
            .ToListAsync();
        Assert.Contains(submittedAudit, a =>
            a.Reason == "Submitted for verification."
            && a.OldValueJson is not null
            && a.NewValueJson is not null
            && a.OldValueJson.Contains("Manual")
            && a.NewValueJson.Contains("Manual"));
    }

    private SpecimenService GuardedSpecimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock, currentUser: _factory.CurrentUser,
            results: new EfRepository<TestResult>(c), allocations: new EfRepository<Allocation>(c), units: new EfRepository<BloodUnit>(c));

    private async Task<long> VerifiedAboRhAsync(long specimenId, AboGroup abo, RhType rh)
    {
        await using var context = _factory.Create();
        var entered = await Results(context).EnterAboRhAsync(new EnterAboRhRequest(specimenId, abo, rh));
        Assert.True(entered.Succeeded, entered.Error);
        var verified = await Results(context, user: Verifier).VerifyResultAsync(entered.Value!.Id);
        Assert.True(verified.Succeeded, verified.Error);
        return entered.Value.Id;
    }

    // TEST-BB-333: invalidating the result a blood type came from retires that type; nothing earlier is reinstated.
    [Fact]
    public async Task InvalidateVerifiedAboRh_RetiresTheDerivedType_AndWarns()
    {
        var patientId = await EnsurePatientAsync("MRN-WBIT-INV");
        var specimenId = await AccessionAsync("ACC-WBIT-INV", patientId);
        var resultId = await VerifiedAboRhAsync(specimenId, AboGroup.A, RhType.Positive);

        await using (var context = _factory.Create())
        {
            Assert.True(await context.PatientBloodTypeHistory.AnyAsync(h => h.PatientId == patientId && h.IsCurrent && h.SourceResultId == resultId));
            var invalidated = await Results(context).InvalidateResultAsync(resultId, "Wrong blood in tube");
            Assert.True(invalidated.Succeeded, invalidated.Error);
            Assert.Contains(invalidated.Warnings, w => w.Code == DerivedFromSourceRule.TypeRetiredCode);
        }

        await using var verify = _factory.Create();
        Assert.False(await verify.PatientBloodTypeHistory.AnyAsync(h => h.PatientId == patientId && h.IsCurrent));
        var retired = await verify.PatientBloodTypeHistory.SingleAsync(h => h.SourceResultId == resultId);
        Assert.NotNull(retired.InvalidatedUtc);
        Assert.Contains("Wrong blood in tube", retired.Reason);
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EventType == AuditEventType.Invalidate
            && a.EntityType == nameof(PatientBloodTypeHistory) && a.EntityId == retired.Id));
    }

    // TEST-BB-352: correcting a verified ABO/Rh retires the type recorded from it at once, not at verification.
    [Fact]
    public async Task CorrectVerifiedAboRh_RetiresTheDerivedTypeImmediately_AndWarns()
    {
        var patientId = await EnsurePatientAsync("MRN-ABO-CORR");
        var specimenId = await AccessionAsync("ACC-ABO-CORR", patientId);
        var resultId = await VerifiedAboRhAsync(specimenId, AboGroup.A, RhType.Positive);

        await using (var context = _factory.Create())
        {
            var corrected = await Results(context).CorrectResultAsync(resultId, "O Positive", "Transcription error: tube read as O.");
            Assert.True(corrected.Succeeded, corrected.Error);
            var warning = Assert.Single(corrected.Warnings, w => w.Code == DerivedFromSourceRule.TypeRetiredCode);
            Assert.Contains("A Positive", warning.Message);
            Assert.Contains("until the correction is verified", warning.Message);
        }

        await using var verify = _factory.Create();
        Assert.False(await verify.PatientBloodTypeHistory.AnyAsync(h => h.PatientId == patientId && h.IsCurrent));
        var retired = await verify.PatientBloodTypeHistory.SingleAsync(h => h.SourceResultId == resultId);
        Assert.NotNull(retired.InvalidatedUtc);
        Assert.Contains("Source result corrected", retired.Reason);
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EventType == AuditEventType.Correct
            && a.EntityType == nameof(PatientBloodTypeHistory) && a.EntityId == retired.Id));
        Assert.False(SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(
            await verify.PatientBloodTypeHistory.Where(h => h.PatientId == patientId).ToListAsync())));
    }

    // TEST-BB-353: a second ABO/Rh determination must come from a separate specimen.
    [Fact]
    public async Task SameSpecimenRetest_IsNotASecondDetermination_ButASecondTubeIs()
    {
        var patientId = await EnsurePatientAsync("MRN-2ND-TUBE");
        var first = await AccessionAsync("ACC-2ND-TUBE-A", patientId);
        await VerifiedAboRhAsync(first, AboGroup.B, RhType.Positive);
        await VerifiedAboRhAsync(first, AboGroup.B, RhType.Positive);

        await using (var verify = _factory.Create())
        {
            var history = await verify.PatientBloodTypeHistory.Where(h => h.PatientId == patientId).ToListAsync();
            Assert.Equal(2, history.Count);
            Assert.All(history, h => Assert.Equal(first, h.SpecimenId));
            Assert.False(SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(history)));
        }

        var second = await AccessionAsync("ACC-2ND-TUBE-B", patientId);
        await VerifiedAboRhAsync(second, AboGroup.B, RhType.Positive);
        await using var again = _factory.Create();
        var all = await again.PatientBloodTypeHistory.Where(h => h.PatientId == patientId).ToListAsync();
        Assert.True(SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(all)));
    }

    [Fact]
    public async Task Backfill_FillsTheSpecimenFromTheSourceResult()
    {
        var patientId = await EnsurePatientAsync("MRN-2ND-FILL");
        var specimenId = await AccessionAsync("ACC-2ND-FILL", patientId);
        var resultId = await VerifiedAboRhAsync(specimenId, AboGroup.A, RhType.Negative);
        await using var c = _factory.Create();
        await c.PatientBloodTypeHistory.Where(h => h.SourceResultId == resultId)
            .ExecuteUpdateAsync(u => u.SetProperty(h => h.SpecimenId, (long?)null));

        await c.Database.ExecuteSqlRawAsync(BloodTypeSpecimenBackfill.Sql);
        Assert.Equal(specimenId, (await c.PatientBloodTypeHistory.AsNoTracking().SingleAsync(h => h.SourceResultId == resultId)).SpecimenId);
    }

    // TEST-BB-357: a verified result later invalidated can be sent to the EHR as wrong; unverified results cannot be sent.
    [Fact]
    public async Task Outbound_InvalidatedVerifiedResult_IsSentAsWrong_UnverifiedIsRefused()
    {
        var patientId = await EnsurePatientAsync("MRN-ORU-W");
        var specimenId = await AccessionAsync("ACC-ORU-W", patientId);
        var resultId = await VerifiedAboRhAsync(specimenId, AboGroup.A, RhType.Positive);

        long retractionId;
        await using (var context = _factory.Create())
        {
            Assert.True((await Results(context).InvalidateResultAsync(resultId, "Wrong blood in tube")).Succeeded);
            retractionId = (await context.TestResults.AsNoTracking().SingleAsync(r => r.Id == resultId)).SupersededByResultId!.Value;
        }

        await using (var context = _factory.Create())
        {
            var queued = await Outbound(context).QueueResultMessageAsync(retractionId);
            Assert.True(queued.Succeeded, queued.Error);
            var obx = queued.Value!.RawMessage.Split('\r', StringSplitOptions.RemoveEmptyEntries).Single(x => x.StartsWith("OBX|", StringComparison.Ordinal));
            Assert.Equal("W", obx.Split('|')[11]);
        }

        var otherSpecimen = await AccessionAsync("ACC-ORU-UNV", patientId);
        await using var c2 = _factory.Create();
        var entered = await Results(c2).EnterAboRhAsync(new EnterAboRhRequest(otherSpecimen, AboGroup.A, RhType.Positive));
        Assert.True(entered.Succeeded, entered.Error);
        var refused = await Outbound(c2).QueueResultMessageAsync(entered.Value!.Id);
        Assert.False(refused.Succeeded);
    }

    private BloodBankLIS.HL7.Messaging.Hl7OutboundService Outbound(BloodBankDbContext c) =>
        new(new EfRepository<TestResult>(c), new EfRepository<Patient>(c), new EfRepository<Hl7MessageLog>(c), c, _factory.Clock);

    [Fact]
    public async Task InvalidatedType_IsNotASecondConcordantDetermination()
    {
        var patientId = await EnsurePatientAsync("MRN-WBIT-2ND");
        var first = await AccessionAsync("ACC-WBIT-2ND-A", patientId);
        var firstResult = await VerifiedAboRhAsync(first, AboGroup.O, RhType.Positive);
        await using (var context = _factory.Create())
        {
            Assert.True((await Results(context).InvalidateResultAsync(firstResult, "Wrong blood in tube")).Succeeded);
        }

        var second = await AccessionAsync("ACC-WBIT-2ND-B", patientId);
        await VerifiedAboRhAsync(second, AboGroup.O, RhType.Positive);

        await using var verify = _factory.Create();
        var history = await verify.PatientBloodTypeHistory.Where(h => h.PatientId == patientId).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.False(SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(history)));
        Assert.False(IssueGate.HasUnresolvedAboRhDiscrepancy(history.Where(h => h.InvalidatedUtc is null).Select(h => (h.BloodType, h.IsCurrent))));
    }

    // TEST-BB-333: a specimen with active results cannot be rejected until they are invalidated.
    [Fact]
    public async Task RejectSpecimen_WithActiveResults_IsRefused_ThenAllowedAfterInvalidation()
    {
        var patientId = await EnsurePatientAsync("MRN-WBIT-REJ");
        var specimenId = await AccessionAsync("ACC-WBIT-REJ", patientId);
        var resultId = await VerifiedAboRhAsync(specimenId, AboGroup.B, RhType.Negative);

        await using (var context = _factory.Create())
        {
            var refused = await GuardedSpecimens(context).RejectAsync(specimenId, "Mislabeled");
            Assert.False(refused.Succeeded);
            Assert.Contains(DerivedFromSourceRule.RejectWithResultsCode, refused.Error);
            Assert.Contains("ABORH", refused.Error, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(SpecimenStatus.Rejected, (await context.Specimens.FindAsync(specimenId))!.Status);
        }

        await using (var context = _factory.Create())
        {
            Assert.True((await Results(context).InvalidateResultAsync(resultId, "Mislabeled specimen")).Succeeded);
            var rejected = await GuardedSpecimens(context).RejectAsync(specimenId, "Mislabeled");
            Assert.True(rejected.Succeeded, rejected.Error);
            Assert.Equal(SpecimenStatus.Rejected, rejected.Value!.Status);
        }
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
        Assert.Contains("Manual", audit.OldValueJson);
        Assert.Contains("Manual", audit.NewValueJson);
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

        var restoreAudit = await verify.AuditEvents
            .Where(a => a.EventType == AuditEventType.Invalidate && a.EntityId == correctionId)
            .ToListAsync();
        Assert.Contains(restoreAudit, a =>
            a.OldValueJson is not null
            && a.NewValueJson is not null
            && a.OldValueJson.Contains("Manual")
            && a.NewValueJson.Contains("Manual"));
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

    // TEST-BB-365: an instrument re-sending a pending result is refused once the specimen has expired, as manual entry is.
    [Fact]
    public async Task InterfaceResend_OnAnExpiredSpecimen_IsRefused_AndThePendingValueStands()
    {
        var key = Guid.NewGuid().ToString("N")[..8];
        var patientId = await EnsurePatientAsync($"MRN-LIFECYCLE-EXP-{key}");
        var specimenId = await AccessionAsync($"ACC-LIFECYCLE-EXP-{key}", patientId);
        long orderId;
        await using (var context = _factory.Create())
        {
            var location = new OrderingLocation { Code = $"LOC-EXP-{key}", Name = "Lab", IsActive = true };
            context.OrderingLocations.Add(location);
            var encounter = new Encounter
            {
                PatientId = patientId, VisitNumber = $"VIS-EXP-{key}", EncounterType = EncounterType.Inpatient,
                Status = EncounterStatus.Active, AdmitUtc = _factory.Clock.UtcNow.AddDays(-1)
            };
            context.Encounters.Add(encounter);
            await context.SaveChangesAsync();
            var order = new Order
            {
                OrderNumber = $"ORD-EXP-{key}", PatientId = patientId, EncounterId = encounter.Id, OrderingLocationId = location.Id,
                OrderCategory = OrderCategory.Test, Priority = OrderPriority.Routine,
                OrderedUtc = _factory.Clock.UtcNow, Status = OrderStatus.InProcess
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;

            var first = await Results(context).EnterFromInstrumentAsync(specimenId, orderId, "HGB", "9.0", "g/dL", null, "ANALYZER-1");
            Assert.True(first.Succeeded, first.Error);
            Assert.Equal(ResultStatus.PendingVerification, first.Value!.Status);

            var specimen = await context.Specimens.SingleAsync(s => s.Id == specimenId);
            specimen.ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            var resent = await Results(context).EnterFromInstrumentAsync(specimenId, orderId, "HGB", "12.5", "g/dL", null, "ANALYZER-1");
            Assert.False(resent.Succeeded);
            Assert.Contains("has expired", resent.Error);
        }

        await using var verify = _factory.Create();
        var stored = await verify.TestResults.SingleAsync(r => r.SpecimenId == specimenId && r.TestCode == "HGB");
        Assert.Equal("9.0", stored.Value);
    }

    [Fact]
    public async Task ReenterAfterInvalidate_WritesSourceOnOldAndNew()
    {
        var key = Guid.NewGuid().ToString("N")[..8];
        var patientId = await EnsurePatientAsync($"MRN-LIFECYCLE-REENT-{key}");
        var specimenId = await AccessionAsync($"ACC-LIFECYCLE-REENT-{key}", patientId);
        long invalidatedId;

        await using (var context = _factory.Create())
        {
            var location = new OrderingLocation { Code = $"LOC-REENT-{key}", Name = "Lab", IsActive = true };
            context.OrderingLocations.Add(location);
            await context.SaveChangesAsync();
            var encounter = new Encounter
            {
                PatientId = patientId,
                VisitNumber = $"VIS-REENT-{key}",
                EncounterType = EncounterType.Inpatient,
                Status = EncounterStatus.Active,
                AdmitUtc = _factory.Clock.UtcNow.AddDays(-1)
            };
            context.Encounters.Add(encounter);
            await context.SaveChangesAsync();
            var order = new Order
            {
                OrderNumber = $"ORD-REENT-{key}",
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
                new EnterResultRequest(specimenId, "HGB", "9.0", order.Id, Source: ResultSource.Manual));
            Assert.True(entered.Succeeded, entered.Error);
            var invalidated = await Results(context).InvalidateResultAsync(entered.Value!.Id, "Wrong specimen");
            Assert.True(invalidated.Succeeded, invalidated.Error);
            invalidatedId = invalidated.Value!.Id;

            var reentered = await Results(context).EnterFromInterfaceAsync(
                specimenId, order.Id, "HGB", "9.4", "g/dL", null, "ANALYZER-REENT");
            Assert.True(reentered.Succeeded, reentered.Error);
            Assert.Equal(ResultSource.Interface, reentered.Value!.Source);
        }

        await using var verify = _factory.Create();
        var events = verify.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Result
            && e.EntityType == nameof(TestResult)
            && e.EntityId == invalidatedId
            && e.Reason == "Re-entry after invalidation."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.OldValueJson.Contains("Manual")
            && e.NewValueJson.Contains("Interface"));
    }
}
