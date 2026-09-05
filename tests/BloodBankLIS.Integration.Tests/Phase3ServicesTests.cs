using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Immunohematology;
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

public class Phase3ServicesTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public Phase3ServicesTests(SqliteContextFactory factory) => _factory = factory;

    private SpecimenService Specimens(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock, audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            currentUser: _factory.CurrentUser, permissions: permissions);

    private static ICurrentUser Verifier => new TestCurrentUser("tech-verify", "WORKSTATION-2");

    private ResultService Results(
        BloodBankDbContext c,
        int securityLevel = 2,
        ICurrentUser? user = null,
        IPermissionEvaluator? permissions = null)
    {
        var current = user ?? _factory.CurrentUser;
        return new(new EfRepository<TestResult>(c), new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            c, _factory.Clock, current, new AuditWriter(c, _factory.Clock, current),
            exceptionDefinitions: new EfRepository<ExceptionDefinition>(c),
            overrides: new EfRepository<Override>(c),
            permissions: permissions ?? new FixedPermissionEvaluator(securityLevel));
    }

    private static async Task EnsureDeltaExceptionAsync(BloodBankDbContext c)
    {
        if (await c.ExceptionDefinitions.AnyAsync(e => e.RuleCode == AboRhDeltaRule.DeltaCode))
        {
            return;
        }

        c.ExceptionDefinitions.Add(new ExceptionDefinition
        {
            RuleCode = AboRhDeltaRule.DeltaCode,
            Name = "ABO/Rh historical discrepancy",
            MinSecurityLevel = 2,
            IsOverridable = true,
            IsActive = true
        });
        await c.SaveChangesAsync();
    }

    private SpecialRequirementService SpecialRequirements(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<Patient>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            permissions);

    private ImmunohematologyService Immuno(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<Patient>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            permissions);

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
            LastName = "Tester",
            FirstName = "Phase3",
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
        Assert.True(result.Succeeded);
        return result.Value!.Id;
    }

    [Fact]
    public async Task Accession_ComputesExpiry_AndAcceptsSpecimen()
    {
        var patientId = await EnsurePatientAsync("MRN-ACC");
        await using var context = _factory.Create();

        var collected = _factory.Clock.UtcNow.AddHours(-2);
        var result = await Specimens(context).AccessionAsync(
            new AccessionSpecimenRequest("ACC-1", patientId, "EDTA", collected));

        Assert.True(result.Succeeded);
        Assert.Equal(SpecimenStatus.Accepted, result.Value!.Status);
        Assert.Equal(collected.AddHours(SpecimenValidityPolicy.DefaultStandardHours), result.Value.ExpiresUtc);
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EventType == AuditEventType.Specimen && a.EntityId == result.Value.Id && a.Reason == "Specimen accessioned."));
    }

    [Fact]
    public async Task Accession_WithRecentPregnancy_UsesThreeDayWindow()
    {
        var patientId = await EnsurePatientAsync("MRN-PREG");
        await using (var setup = _factory.Create())
        {
            var patient = await setup.Patients.FindAsync(patientId);
            patient!.RecentPregnancyUtc = _factory.Clock.UtcNow.AddDays(-10);
            await setup.SaveChangesAsync();
        }

        await using var context = _factory.Create();
        var collected = _factory.Clock.UtcNow.AddHours(-2);
        var result = await Specimens(context).AccessionAsync(
            new AccessionSpecimenRequest("ACC-PREG", patientId, "EDTA", collected));

        Assert.True(result.Succeeded);
        Assert.Equal(collected.AddHours(SpecimenValidityPolicy.DefaultAlloimmunizationRiskHours), result.Value!.ExpiresUtc);
    }

    [Fact]
    public async Task Accession_FutureCollection_Fails()
    {
        var patientId = await EnsurePatientAsync("MRN-FUT");
        await using var context = _factory.Create();

        var result = await Specimens(context).AccessionAsync(
            new AccessionSpecimenRequest("ACC-FUT", patientId, "EDTA", _factory.Clock.UtcNow.AddHours(1)));

        Assert.False(result.Succeeded);
        Assert.Contains("future", result.Error);
    }

    [Fact]
    public async Task Accession_DuplicateAccessionNumber_Fails()
    {
        var patientId = await EnsurePatientAsync("MRN-DUP");
        await AccessionAsync("ACC-DUP", patientId);

        await using var context = _factory.Create();
        var second = await Specimens(context).AccessionAsync(
            new AccessionSpecimenRequest("ACC-DUP", patientId, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));

        Assert.False(second.Succeeded);
        Assert.Contains("already exists", second.Error);
    }

    [Fact]
    public async Task Accession_WithoutSpecimenAccession_IsHardStopped()
    {
        var patientId = await EnsurePatientAsync("MRN-ACC-PERM");
        await using var context = _factory.Create();
        var denied = await Specimens(context, new FixedPermissionEvaluator(1, PermissionCodes.SpecimenEdit))
            .AccessionAsync(new AccessionSpecimenRequest("ACC-PERM", patientId, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.False(denied.Succeeded);
        Assert.Equal(SpecimenAuthorizationRule.EvaluateAccession(false).Message, denied.Error);
        Assert.False(await context.Specimens.AnyAsync(s => s.AccessionNumber == "ACC-PERM"));

        var allowed = await Specimens(context, new FixedPermissionEvaluator(1, PermissionCodes.SpecimenAccession))
            .AccessionAsync(new AccessionSpecimenRequest("ACC-PERM", patientId, "EDTA", _factory.Clock.UtcNow.AddHours(-1)));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(SpecimenStatus.Accepted, allowed.Value!.Status);
    }

    [Fact]
    public async Task Reject_RequiresReason_AndSetsStatus()
    {
        var patientId = await EnsurePatientAsync("MRN-REJ");
        var specimenId = await AccessionAsync("ACC-REJ", patientId);

        await using (var context = _factory.Create())
        {
            var noReason = await Specimens(context).RejectAsync(specimenId, "  ");
            Assert.False(noReason.Succeeded);
        }

        await using (var context = _factory.Create())
        {
            var rejected = await Specimens(context).RejectAsync(specimenId, "Hemolyzed");
            Assert.True(rejected.Succeeded);
            Assert.Equal(SpecimenStatus.Rejected, rejected.Value!.Status);
            Assert.Equal("Hemolyzed", rejected.Value.RejectionReason);
            Assert.True(await context.AuditEvents.AnyAsync(a =>
                a.EventType == AuditEventType.Specimen && a.EntityId == specimenId && a.Reason == "Hemolyzed"));
        }
    }

    [Fact]
    public async Task Reject_WithoutSpecimenReject_IsHardStopped()
    {
        var patientId = await EnsurePatientAsync("MRN-REJ-PERM");
        var specimenId = await AccessionAsync("ACC-REJ-PERM", patientId);

        await using var context = _factory.Create();
        var denied = await Specimens(context, new FixedPermissionEvaluator(1, PermissionCodes.SpecimenAccession))
            .RejectAsync(specimenId, "Hemolyzed");
        Assert.False(denied.Succeeded);
        Assert.Equal(SpecimenAuthorizationRule.EvaluateReject(false).Message, denied.Error);
        Assert.Equal(SpecimenStatus.Accepted, (await context.Specimens.FindAsync(specimenId))!.Status);

        var allowed = await Specimens(context, new FixedPermissionEvaluator(1, PermissionCodes.SpecimenReject))
            .RejectAsync(specimenId, "Hemolyzed");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(SpecimenStatus.Rejected, allowed.Value!.Status);
    }

    [Fact]
    public async Task Update_WritesMetadata_AndRecomputesExpiry()
    {
        var patientId = await EnsurePatientAsync("MRN-EDIT");
        var specimenId = await AccessionAsync("ACC-EDIT", patientId);
        var collected = _factory.Clock.UtcNow.AddHours(-6);

        await using var context = _factory.Create();
        var updated = await Specimens(context).UpdateAsync(specimenId, new UpdateSpecimenRequest(
            collected, Barcode: "BC-1", DrawLocation: "4W", Collector: "J. Tech"));

        Assert.True(updated.Succeeded);
        Assert.Equal(collected, updated.Value!.CollectedUtc);
        Assert.Equal("BC-1", updated.Value.Barcode);
        Assert.Equal("4W", updated.Value.DrawLocation);
        Assert.Equal("J. Tech", updated.Value.Collector);
        Assert.Equal(collected.AddHours(SpecimenValidityPolicy.DefaultStandardHours), updated.Value.ExpiresUtc);
        Assert.Equal("ACC-EDIT", updated.Value.AccessionNumber);
        Assert.Equal("EDTA", updated.Value.SpecimenType);
        Assert.Equal(SpecimenStatus.Accepted, updated.Value.Status);
    }

    [Fact]
    public async Task Update_WithoutSpecimenEdit_IsHardStopped()
    {
        var patientId = await EnsurePatientAsync("MRN-EDIT-PERM");
        var specimenId = await AccessionAsync("ACC-EDIT-PERM", patientId);
        var collected = _factory.Clock.UtcNow.AddHours(-6);

        await using var context = _factory.Create();
        var denied = await Specimens(context, new FixedPermissionEvaluator(1, PermissionCodes.SpecimenAccession))
            .UpdateAsync(specimenId, new UpdateSpecimenRequest(collected, Barcode: "BC-X"));
        Assert.False(denied.Succeeded);
        Assert.Equal(SpecimenAuthorizationRule.EvaluateEdit(false).Message, denied.Error);
        Assert.Null((await context.Specimens.FindAsync(specimenId))!.Barcode);

        var allowed = await Specimens(context, new FixedPermissionEvaluator(1, PermissionCodes.SpecimenEdit))
            .UpdateAsync(specimenId, new UpdateSpecimenRequest(collected, Barcode: "BC-X"));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("BC-X", allowed.Value!.Barcode);
    }

    [Fact]
    public async Task Update_FutureCollection_Fails()
    {
        var patientId = await EnsurePatientAsync("MRN-EDIT-FUT");
        var specimenId = await AccessionAsync("ACC-EDIT-FUT", patientId);

        await using var context = _factory.Create();
        var result = await Specimens(context).UpdateAsync(specimenId, new UpdateSpecimenRequest(
            _factory.Clock.UtcNow.AddHours(1)));

        Assert.False(result.Succeeded);
        Assert.Contains("future", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_RejectedSpecimen_Fails()
    {
        var patientId = await EnsurePatientAsync("MRN-EDIT-REJ");
        var specimenId = await AccessionAsync("ACC-EDIT-REJ", patientId);

        await using (var context = _factory.Create())
        {
            Assert.True((await Specimens(context).RejectAsync(specimenId, "Clotted")).Succeeded);
        }

        await using (var context = _factory.Create())
        {
            var result = await Specimens(context).UpdateAsync(specimenId, new UpdateSpecimenRequest(
                _factory.Clock.UtcNow.AddHours(-2), Collector: "Someone"));
            Assert.False(result.Succeeded);
            Assert.Contains("Rejected", result.Error);
        }
    }

    [Fact]
    public async Task Update_ValidityHoursOverride_SetsExpiry()
    {
        var patientId = await EnsurePatientAsync("MRN-EDIT-HRS");
        var specimenId = await AccessionAsync("ACC-EDIT-HRS", patientId);
        var collected = _factory.Clock.UtcNow.AddHours(-3);

        await using var context = _factory.Create();
        var updated = await Specimens(context).UpdateAsync(specimenId, new UpdateSpecimenRequest(
            collected, ValidityHours: 48));

        Assert.True(updated.Succeeded);
        Assert.Equal(collected.AddHours(48), updated.Value!.ExpiresUtc);
    }

    [Fact]
    public async Task EnterThenVerify_TransitionsStatus()
    {
        var patientId = await EnsurePatientAsync("MRN-RES");
        var specimenId = await AccessionAsync("ACC-RES", patientId);
        long resultId;

        await using (var context = _factory.Create())
        {
            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "13.5", Units: "g/dL"));
            Assert.True(entered.Succeeded);
            Assert.Equal(ResultStatus.Entered, entered.Value!.Status);
            resultId = entered.Value.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded);
            Assert.Equal(ResultStatus.Verified, verified.Value!.Status);
            Assert.Equal(_factory.CurrentUser.UserName, verified.Value.VerifiedBy);
        }
    }

    [Fact]
    public async Task Verify_AlreadyVerified_Fails()
    {
        var patientId = await EnsurePatientAsync("MRN-REVERIFY");
        var specimenId = await AccessionAsync("ACC-REVERIFY", patientId);
        long resultId;

        await using (var context = _factory.Create())
        {
            resultId = (await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "10"))).Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            await Results(context).VerifyResultAsync(resultId);
        }

        await using (var context = _factory.Create())
        {
            var second = await Results(context).VerifyResultAsync(resultId);
            Assert.False(second.Succeeded);
        }
    }

    [Fact]
    public async Task VerifyAboRh_AppendsCurrentBloodType()
    {
        var patientId = await EnsurePatientAsync("MRN-ABO");
        var specimenId = await AccessionAsync("ACC-ABO", patientId);
        long resultId;

        await using (var context = _factory.Create())
        {
            var entered = await Results(context).EnterAboRhAsync(new EnterAboRhRequest(specimenId, AboGroup.O, RhType.Positive));
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var self = await Results(context).VerifyResultAsync(resultId);
            Assert.False(self.Succeeded);
            Assert.Contains(self.Evaluation!.HardStops, r => r.Code == SelfVerifyRule.Code);

            var verified = await Results(context, user: Verifier).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded);
            Assert.True(verified.Evaluation is null || verified.Evaluation.Warnings.Count == 0);
        }

        await using (var verify = _factory.Create())
        {
            var current = await Immuno(verify).GetCurrentBloodTypeAsync(patientId);
            Assert.NotNull(current);
            Assert.Equal(AboGroup.O, current!.Abo);
            Assert.Equal(RhType.Positive, current.RhD);
            Assert.Equal(resultId, current.SourceResultId);
        }
    }

    [Fact]
    public async Task VerifyResult_WithoutResultVerify_IsHardStopped()
    {
        var patientId = await EnsurePatientAsync("MRN-ABO-PERM");
        var specimenId = await AccessionAsync("ACC-ABO-PERM", patientId);
        long resultId;

        await using (var context = _factory.Create())
        {
            var entered = await Results(context).EnterAboRhAsync(new EnterAboRhRequest(specimenId, AboGroup.O, RhType.Positive));
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var denied = await Results(
                context,
                user: Verifier,
                permissions: new FixedPermissionEvaluator(2, PermissionCodes.ResultEnter)).VerifyResultAsync(resultId);
            Assert.False(denied.Succeeded);
            Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ResultAuthorizationRule.VerifyCode);
            Assert.Equal(ResultStatus.Entered, (await context.TestResults.FindAsync(resultId))!.Status);

            var verified = await Results(
                context,
                user: Verifier,
                permissions: new FixedPermissionEvaluator(2, PermissionCodes.ResultVerify)).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
            Assert.Equal(ResultStatus.Verified, verified.Value!.Status);
        }
    }

    [Fact]
    public async Task EnterAboRh_WithoutResultEnter_IsHardStopped()
    {
        var patientId = await EnsurePatientAsync("MRN-ABO-ENT");
        var specimenId = await AccessionAsync("ACC-ABO-ENT", patientId);

        await using var context = _factory.Create();
        var denied = await Results(
            context,
            permissions: new FixedPermissionEvaluator(2, PermissionCodes.ResultVerify))
            .EnterAboRhAsync(new EnterAboRhRequest(specimenId, AboGroup.O, RhType.Positive));
        Assert.False(denied.Succeeded);
        Assert.Equal(ResultAuthorizationRule.EvaluateEnter(false).Message, denied.Error);
        Assert.False(await context.TestResults.AnyAsync(r => r.SpecimenId == specimenId));

        var allowed = await Results(
            context,
            permissions: new FixedPermissionEvaluator(2, PermissionCodes.ResultEnter))
            .EnterAboRhAsync(new EnterAboRhRequest(specimenId, AboGroup.O, RhType.Positive));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(ResultStatus.Entered, allowed.Value!.Status);
    }

    [Fact]
    public async Task SaveAboRh_MarkComplete_LeavesEntered_DoesNotSetCurrentType()
    {
        var patientId = await EnsurePatientAsync("MRN-ABO-MC");
        var specimenId = await AccessionAsync("ACC-ABO-MC", patientId);

        await using var context = _factory.Create();
        var saved = await Results(context).SaveTestResultAsync(new SaveTestResultRequest(
            specimenId, 0, 0, ResultService.AboRhTestCode, null, null, null,
            AboGroup.A, RhType.Negative, null,
            MarkComplete: true, CorrectionReason: null, UnitNumber: null, CrossmatchMethod: null,
            CrossmatchResult: null, AntibodyScreenNegative: null));

        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal(ResultStatus.Entered, saved.Value!.Status);
        Assert.Null(saved.Value.VerifiedBy);
        Assert.Null(await Immuno(context).GetCurrentBloodTypeAsync(patientId));
    }

    [Fact]
    public async Task VerifyAboRh_Discrepancy_BlocksUntilOverride_ReplaceFlipsCurrent()
    {
        var patientId = await EnsurePatientAsync("MRN-DELTA");
        var firstSpecimen = await AccessionAsync("ACC-DELTA-1", patientId);
        var secondSpecimen = await AccessionAsync("ACC-DELTA-2", patientId);

        await using (var context = _factory.Create())
        {
            await EnsureDeltaExceptionAsync(context);
            var r1 = (await Results(context).EnterAboRhAsync(new EnterAboRhRequest(firstSpecimen, AboGroup.O, RhType.Positive))).Value!.Id;
            await Results(context, user: Verifier).VerifyResultAsync(r1);
        }

        long secondResultId;
        await using (var context = _factory.Create())
        {
            secondResultId = (await Results(context).EnterAboRhAsync(new EnterAboRhRequest(secondSpecimen, AboGroup.A, RhType.Positive))).Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var blocked = await Results(context, user: Verifier).VerifyResultAsync(secondResultId);
            Assert.False(blocked.Succeeded);
            Assert.True(blocked.RequiresOverride);
            Assert.Contains(blocked.Evaluation!.Warnings, w => w.Code == AboRhDeltaRule.DeltaCode);
        }

        await using (var verify = _factory.Create())
        {
            var history = await Immuno(verify).GetBloodTypeHistoryAsync(patientId);
            Assert.Single(history, h => h.IsCurrent);
            Assert.Equal(AboGroup.O, history.Single(h => h.IsCurrent).Abo);
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context, user: Verifier).VerifyResultAsync(secondResultId, new VerifyResultRequest(
                "Confirmed retype", "supervisor", AboRhHistoryResolution.Replace, SignatureId: 1));
            Assert.True(verified.Succeeded);
            Assert.Contains(verified.Evaluation!.Warnings, w => w.Code == AboRhDeltaRule.DeltaCode);
            Assert.True(await context.Overrides.AnyAsync(o =>
                o.ContextType == nameof(TestResult) && o.RuleCode == AboRhDeltaRule.DeltaCode && o.Resolution == "Replace"));
        }

        await using (var verify = _factory.Create())
        {
            var history = await Immuno(verify).GetBloodTypeHistoryAsync(patientId);
            Assert.Equal(2, history.Count);
            Assert.Single(history, h => h.IsCurrent);
            Assert.Equal(AboGroup.A, history.Single(h => h.IsCurrent).Abo);
        }
    }

    [Fact]
    public async Task VerifyAboRh_Discrepancy_RetainKeepsHistoricalCurrent()
    {
        var patientId = await EnsurePatientAsync("MRN-DELTA-RET");
        var firstSpecimen = await AccessionAsync("ACC-DELTA-RET-1", patientId);
        var secondSpecimen = await AccessionAsync("ACC-DELTA-RET-2", patientId);

        await using (var context = _factory.Create())
        {
            await EnsureDeltaExceptionAsync(context);
            var r1 = (await Results(context).EnterAboRhAsync(new EnterAboRhRequest(firstSpecimen, AboGroup.O, RhType.Positive))).Value!.Id;
            await Results(context, user: Verifier).VerifyResultAsync(r1);
        }

        long secondResultId;
        await using (var context = _factory.Create())
        {
            secondResultId = (await Results(context).EnterAboRhAsync(new EnterAboRhRequest(secondSpecimen, AboGroup.B, RhType.Negative))).Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context, user: Verifier).VerifyResultAsync(secondResultId, new VerifyResultRequest(
                "Keep historical pending investigation", "supervisor", AboRhHistoryResolution.Retain, SignatureId: 2));
            Assert.True(verified.Succeeded);
            Assert.Equal(ResultStatus.Verified, verified.Value!.Status);
        }

        await using (var verify = _factory.Create())
        {
            var history = await Immuno(verify).GetBloodTypeHistoryAsync(patientId);
            Assert.Single(history);
            Assert.True(history[0].IsCurrent);
            Assert.Equal(AboGroup.O, history[0].Abo);
            Assert.Equal(RhType.Positive, history[0].RhD);
        }
    }

    [Fact]
    public async Task VerifyAboRh_Discrepancy_LowSecurityLevel_HardStops()
    {
        var patientId = await EnsurePatientAsync("MRN-DELTA-LOW");
        var firstSpecimen = await AccessionAsync("ACC-DELTA-LOW-1", patientId);
        var secondSpecimen = await AccessionAsync("ACC-DELTA-LOW-2", patientId);

        await using (var context = _factory.Create())
        {
            await EnsureDeltaExceptionAsync(context);
            var r1 = (await Results(context).EnterAboRhAsync(new EnterAboRhRequest(firstSpecimen, AboGroup.O, RhType.Positive))).Value!.Id;
            await Results(context, user: Verifier).VerifyResultAsync(r1);
        }

        long secondResultId;
        await using (var context = _factory.Create())
        {
            secondResultId = (await Results(context).EnterAboRhAsync(new EnterAboRhRequest(secondSpecimen, AboGroup.A, RhType.Positive))).Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var blocked = await Results(context, securityLevel: 1, user: Verifier).VerifyResultAsync(secondResultId, new VerifyResultRequest(
                "Attempt", "tech1", AboRhHistoryResolution.Replace, SignatureId: 3));
            Assert.False(blocked.Succeeded);
            Assert.True(blocked.Evaluation!.IsHardStopped);
            Assert.Contains(blocked.Evaluation.HardStops, h => h.Code == "EXC-SECURITY-LEVEL");
        }
    }

    [Fact]
    public async Task Correct_RequiresReason_AndSupersedesOriginal()
    {
        var patientId = await EnsurePatientAsync("MRN-CORR");
        var specimenId = await AccessionAsync("ACC-CORR", patientId);
        long resultId;

        await using (var context = _factory.Create())
        {
            resultId = (await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "9.0"))).Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            await Results(context).VerifyResultAsync(resultId);
        }

        await using (var context = _factory.Create())
        {
            var noReason = await Results(context).CorrectResultAsync(resultId, "10.0", "  ");
            Assert.False(noReason.Succeeded);
        }

        long correctionId;
        await using (var context = _factory.Create())
        {
            var corrected = await Results(context).CorrectResultAsync(resultId, "10.0", "Transcription error");
            Assert.True(corrected.Succeeded);
            Assert.Equal(2, corrected.Value!.Version);
            Assert.Equal(ResultStatus.Corrected, corrected.Value.Status);
            correctionId = corrected.Value.Id;
        }

        await using (var verify = _factory.Create())
        {
            var original = await verify.TestResults.FindAsync(resultId);
            Assert.Equal(correctionId, original!.SupersededByResultId);
            Assert.Equal(ResultStatus.Verified, original.Status); // original clinical value preserved

            var correctAudit = await verify.AuditEvents
                .Where(a => a.EntityType == nameof(TestResult) && a.EntityId == resultId && a.EventType == AuditEventType.Correct)
                .SingleAsync();
            Assert.Equal("Transcription error", correctAudit.Reason);
        }
    }

    [Fact]
    public async Task Correct_WithoutResultCorrect_IsHardStopped()
    {
        var patientId = await EnsurePatientAsync("MRN-CORR-PERM");
        var specimenId = await AccessionAsync("ACC-CORR-PERM", patientId);
        long resultId;

        await using (var context = _factory.Create())
        {
            resultId = (await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "9.0"))).Value!.Id;
            await Results(context, user: Verifier).VerifyResultAsync(resultId);
        }

        await using var act = _factory.Create();
        var denied = await Results(
            act,
            permissions: new FixedPermissionEvaluator(2, PermissionCodes.ResultEnter))
            .CorrectResultAsync(resultId, "10.0", "Transcription error");
        Assert.False(denied.Succeeded);
        Assert.Equal(ResultAuthorizationRule.EvaluateCorrect(false).Message, denied.Error);
        Assert.Null((await act.TestResults.FindAsync(resultId))!.SupersededByResultId);

        var allowed = await Results(
            act,
            permissions: new FixedPermissionEvaluator(2, PermissionCodes.ResultCorrect))
            .CorrectResultAsync(resultId, "10.0", "Transcription error");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(ResultStatus.Corrected, allowed.Value!.Status);
    }

    [Fact]
    public async Task Correct_UnverifiedResult_Fails()
    {
        var patientId = await EnsurePatientAsync("MRN-CORR-UNV");
        var specimenId = await AccessionAsync("ACC-CORR-UNV", patientId);
        long resultId;

        await using (var context = _factory.Create())
        {
            resultId = (await Results(context).EnterResultAsync(new EnterResultRequest(specimenId, "HGB", "9.0"))).Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await Results(context).CorrectResultAsync(resultId, "10.0", "Too early");
            Assert.False(result.Succeeded);
        }
    }

    [Fact]
    public async Task ManualBloodType_RequiresReason_AndWritesAudit()
    {
        var patientId = await EnsurePatientAsync("MRN-MANUAL");

        await using (var context = _factory.Create())
        {
            var noReason = await Immuno(context).RecordBloodTypeManualAsync(patientId, AboGroup.B, RhType.Negative, "  ");
            Assert.False(noReason.Succeeded);
        }

        await using (var context = _factory.Create())
        {
            var recorded = await Immuno(context).RecordBloodTypeManualAsync(patientId, AboGroup.B, RhType.Negative, "Historical record import");
            Assert.True(recorded.Succeeded);
            Assert.Equal(BloodTypeSource.ManualEntry, recorded.Value!.Source);
            Assert.True(recorded.Value.IsCurrent);
        }

        await using (var verify = _factory.Create())
        {
            Assert.Contains(
                await verify.AuditEvents
                    .Where(a => a.EntityType == nameof(PatientBloodTypeHistory) && a.EntityId == patientId)
                    .ToListAsync(),
                a => a.EventType == AuditEventType.Override && a.Reason == "Historical record import");
        }
    }

    [Fact]
    public async Task ManualBloodType_WithoutImmunoOverride_IsRejected()
    {
        var patientId = await EnsurePatientAsync("MRN-IH-PERM");
        await using var context = _factory.Create();
        var denied = await Immuno(context, new FixedPermissionEvaluator(1, PermissionCodes.ImmunoRecord))
            .RecordBloodTypeManualAsync(patientId, AboGroup.O, RhType.Positive, "Historical record import");
        Assert.False(denied.Succeeded);
        Assert.Contains("immuno.override", denied.Error, StringComparison.OrdinalIgnoreCase);

        var allowed = await Immuno(context, new FixedPermissionEvaluator(2, PermissionCodes.ImmunoOverride))
            .RecordBloodTypeManualAsync(patientId, AboGroup.O, RhType.Positive, "Historical record import");
        Assert.True(allowed.Succeeded, allowed.Error);
    }

    [Fact]
    public async Task AntibodyWrites_RequireImmunoPermissions()
    {
        var patientId = await EnsurePatientAsync("MRN-AB-PERM");
        await using var context = _factory.Create();
        var noRecord = await Immuno(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .AddAntibodyAsync(patientId, null, "anti-K", AntibodyStatus.Identified, "Detected on screen");
        Assert.False(noRecord.Succeeded);
        Assert.Contains("immuno.record", noRecord.Error, StringComparison.OrdinalIgnoreCase);

        var added = await Immuno(context, new FixedPermissionEvaluator(1, PermissionCodes.ImmunoRecord))
            .AddAntibodyAsync(patientId, null, "anti-K", AntibodyStatus.Identified, "Detected on screen");
        Assert.True(added.Succeeded, added.Error);

        var noOverride = await Immuno(context, new FixedPermissionEvaluator(1, PermissionCodes.ImmunoRecord))
            .DeactivateAntibodyAsync(added.Value!.Id, "Reclassified as historical");
        Assert.False(noOverride.Succeeded);
        Assert.Contains("immuno.override", noOverride.Error, StringComparison.OrdinalIgnoreCase);

        var deactivated = await Immuno(context, new FixedPermissionEvaluator(2, PermissionCodes.ImmunoOverride))
            .DeactivateAntibodyAsync(added.Value.Id, "Reclassified as historical");
        Assert.True(deactivated.Succeeded, deactivated.Error);
    }

    [Fact]
    public async Task SpecialRequirementWrites_RequireImmunoPermissions()
    {
        var patientId = await EnsurePatientAsync("MRN-SR-PERM");
        await using var context = _factory.Create();
        var noRecord = await SpecialRequirements(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .AddAsync(patientId, new AddSpecialRequirementRequest(SpecialTransfusionRequirementType.Irradiated, "Needed"));
        Assert.False(noRecord.Succeeded);
        Assert.Contains("immuno.record", noRecord.Error, StringComparison.OrdinalIgnoreCase);

        var added = await SpecialRequirements(context, new FixedPermissionEvaluator(1, PermissionCodes.ImmunoRecord))
            .AddAsync(patientId, new AddSpecialRequirementRequest(SpecialTransfusionRequirementType.Irradiated, "Needed"));
        Assert.True(added.Succeeded, added.Error);

        var noOverride = await SpecialRequirements(context, new FixedPermissionEvaluator(1, PermissionCodes.ImmunoRecord))
            .DeactivateAsync(added.Value!.Id, "No longer indicated");
        Assert.False(noOverride.Succeeded);
        Assert.Contains("immuno.override", noOverride.Error, StringComparison.OrdinalIgnoreCase);

        var deactivated = await SpecialRequirements(context, new FixedPermissionEvaluator(2, PermissionCodes.ImmunoOverride))
            .DeactivateAsync(added.Value.Id, "No longer indicated");
        Assert.True(deactivated.Succeeded, deactivated.Error);
    }

    [Fact]
    public async Task SpecialRequirement_AddAndDeactivate_WriteNamedAudits()
    {
        var patientId = await EnsurePatientAsync("MRN-SR-RA16-" + Guid.NewGuid().ToString("N")[..8]);
        await using var context = _factory.Create();
        var svc = SpecialRequirements(context);

        var added = await svc.AddAsync(patientId, new AddSpecialRequirementRequest(
            SpecialTransfusionRequirementType.AntigenNegative,
            "History of anti-K",
            AntigenCode: "K"));
        Assert.True(added.Succeeded, added.Error);
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(SpecialTransfusionRequirement)
            && a.EntityId == added.Value!.Id
            && a.EventType == AuditEventType.Antibody
            && a.Reason == "History of anti-K"));

        var deactivated = await svc.DeactivateAsync(added.Value!.Id, "No longer indicated");
        Assert.True(deactivated.Succeeded, deactivated.Error);
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(SpecialTransfusionRequirement)
            && a.EntityId == added.Value.Id
            && a.EventType == AuditEventType.Deactivate
            && a.OldValueJson != null
            && a.NewValueJson != null));
    }

    [Fact]
    public async Task Antibody_AddThenDeactivate_RequiresReason()
    {
        var patientId = await EnsurePatientAsync("MRN-AB");
        long antibodyId;

        await using (var context = _factory.Create())
        {
            var added = await Immuno(context).AddAntibodyAsync(patientId, null, "anti-K", AntibodyStatus.Identified, "Detected on screen");
            Assert.True(added.Succeeded);
            antibodyId = added.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var active = await Immuno(context).GetActiveAntibodiesAsync(patientId);
            Assert.Single(active);
        }

        await using (var context = _factory.Create())
        {
            var noReason = await Immuno(context).DeactivateAntibodyAsync(antibodyId, "  ");
            Assert.False(noReason.Succeeded);
        }

        await using (var context = _factory.Create())
        {
            var deactivated = await Immuno(context).DeactivateAntibodyAsync(antibodyId, "Reclassified as historical");
            Assert.True(deactivated.Succeeded);
            Assert.False(deactivated.Value!.IsActive);
        }

        await using (var verify = _factory.Create())
        {
            Assert.Empty(await Immuno(verify).GetActiveAntibodiesAsync(patientId));
            Assert.Single(await Immuno(verify).GetAntibodyHistoryAsync(patientId));

            var antibodyAudits = await verify.AuditEvents
                .Where(a => a.EntityType == nameof(AntibodyHistory) && a.EntityId == antibodyId && a.EventType == AuditEventType.Antibody)
                .ToListAsync();
            Assert.Equal(2, antibodyAudits.Count);
            Assert.Contains(antibodyAudits, a => a.Reason == "Detected on screen");
            Assert.Contains(antibodyAudits, a => a.Reason == "Reclassified as historical");
        }
    }

    [Fact]
    public async Task AntigenProfile_SaveThenUpdate_WritesAntibodyAudit_WithOldAndNew()
    {
        var patientId = await EnsurePatientAsync("MRN-AG-AUDIT");
        long attrId;
        long profileId;

        await using (var context = _factory.Create())
        {
            var attr = new BloodAttributeDefinition
            {
                Code = "AG-AUDIT-K",
                Name = "Kell audit",
                AntibodyName = "anti-K",
                IsClinicallySignificant = true,
                SortOrder = 90,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = _factory.Clock.UtcNow,
                Version = 1
            };
            context.BloodAttributeDefinitions.Add(attr);
            await context.SaveChangesAsync();
            attrId = attr.Id;
        }

        await using (var context = _factory.Create())
        {
            var first = await Immuno(context).SaveAntigenProfileAsync(
                patientId, new SaveAntigenProfileRequest(attrId, AntigenResult.Negative, "Tube"));
            Assert.True(first.Succeeded, first.Error);
            profileId = first.Value!.Id;

            var second = await Immuno(context).SaveAntigenProfileAsync(
                patientId, new SaveAntigenProfileRequest(attrId, AntigenResult.Positive, "Gel"));
            Assert.True(second.Succeeded, second.Error);
            Assert.Equal(profileId, second.Value!.Id);
            Assert.Equal(AntigenResult.Positive, second.Value.Result);
        }

        await using var verify = _factory.Create();
        var audits = await verify.AuditEvents
            .Where(a => a.EntityType == nameof(AntigenProfile) && a.EntityId == profileId && a.EventType == AuditEventType.Antibody)
            .OrderBy(a => a.Id)
            .ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.Equal("Antigen phenotype recorded.", audits[0].Reason);
        Assert.Contains("Negative", audits[0].NewValueJson);
        Assert.Equal("Antigen phenotype updated in place (OCD-022).", audits[1].Reason);
        Assert.Contains("Negative", audits[1].OldValueJson);
        Assert.Contains("Positive", audits[1].NewValueJson);
    }
}
