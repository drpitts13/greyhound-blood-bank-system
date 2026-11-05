using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ReactionInvestigationServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ReactionInvestigationServiceTests(SqliteContextFactory factory) => _factory = factory;

    private ReactionInvestigationService CreateService(
        BloodBankDbContext context,
        IPermissionEvaluator? permissions = null) =>
        new(
            new EfRepository<ReactionInvestigation>(context),
            new EfRepository<TransfusionEvent>(context),
            new InventoryRepository(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: permissions,
            patients: new EfRepository<Patient>(context),
            bloodTypes: new EfRepository<PatientBloodTypeHistory>(context),
            antibodies: new EfRepository<AntibodyHistory>(context),
            issues: new EfRepository<Issue>(context),
            productTypes: new EfRepository<ProductType>(context),
            specimens: new EfRepository<Specimen>(context),
            orders: new EfRepository<Order>(context),
            orderLines: new EfRepository<OrderLine>(context),
            testResults: new EfRepository<TestResult>(context));

    [Fact]
    public async Task Update_WithoutReactionInvestigate_IsHardStopped()
    {
        await using var context = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-RXN-PERM",
            LastName = "React",
            FirstName = "Pat",
            DateOfBirth = new DateOnly(1970, 1, 1)
        };
        var product = new ProductType { ProductCode = "RBC-RXN-PERM", Name = "RBC" };
        context.Patients.Add(patient);
        context.ProductTypes.Add(product);
        await context.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = "U-RXN-PERM",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Status = UnitStatus.Issued,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10)
        };
        context.BloodUnits.Add(unit);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            IssuedUtc = _factory.Clock.UtcNow,
            IssuedBy = "tech-test",
            Status = IssueStatus.Issued
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            ReactionSuspected = true,
            FinalDisposition = TransfusionDisposition.Completed,
            DocumentedBy = "tech-test"
        };
        context.TransfusionEvents.Add(transfusion);
        await context.SaveChangesAsync();

        var investigation = new ReactionInvestigation
        {
            TransfusionEventId = transfusion.Id,
            PatientId = patient.Id,
            BloodProductId = unit.Id,
            ReportedUtc = _factory.Clock.UtcNow,
            ReportedBy = "tech-test",
            Status = ReactionInvestigationStatus.Open
        };
        context.ReactionInvestigations.Add(investigation);
        await context.SaveChangesAsync();

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .UpdateAsync(investigation.Id, new UpdateReactionInvestigationRequest(
                "FNHTR", ReactionSeverity.Mild, null, null, null, null, null, null, null));
        Assert.False(denied.Succeeded);
        Assert.Equal(ReactionAuthorizationRule.EvaluateInvestigate(false).Message, denied.Error);
        Assert.Null((await context.ReactionInvestigations.FindAsync(investigation.Id))!.ReactionType);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.ReactionInvestigate))
            .UpdateAsync(investigation.Id, new UpdateReactionInvestigationRequest(
                "FNHTR", ReactionSeverity.Mild, null, null, null, null, null, null, null));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("FNHTR", allowed.Value!.ReactionType);

        var cberDenied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .RecordCberNotificationAsync(investigation.Id);
        Assert.False(cberDenied.Succeeded);
        Assert.Equal(ReactionAuthorizationRule.EvaluateInvestigate(false).Message, cberDenied.Error);
    }

    [Fact]
    public async Task OpenAndUpdate_WriteReactionInvestigation()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(context, key);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);
        Assert.True(opened.Id > 0);

        var updated = await CreateService(context).UpdateAsync(
            opened.Id,
            new UpdateReactionInvestigationRequest(
                "FNHTR", ReactionSeverity.Mild, null, null, null, null, null, null, null,
                RepeatPatientAboRh: "A Positive",
                RepeatUnitAboRh: "O Positive",
                DatResult: DatWorkupResult.Negative));
        Assert.True(updated.Succeeded, updated.Error);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EntityType == nameof(ReactionInvestigation)
            && e.EventType == AuditEventType.ReactionInvestigation
            && e.EntityId == opened.Id
            && e.Reason == "Reaction suspected");
        Assert.Contains(events, e =>
            e.EntityType == nameof(ReactionInvestigation)
            && e.EventType == AuditEventType.ReactionInvestigation
            && e.EntityId == opened.Id
            && e.Reason == "Investigation updated."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains("FNHTR"));
    }

    [Fact]
    public async Task ListDtos_SurfacesPatientUnitTypeAndWorkupIncomplete()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(context, key, includeCurrentType: true);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);

        var list = await CreateService(context).ListDtosAsync();
        var row = Assert.Single(list, r => r.Id == opened.Id);
        Assert.Equal($"MRN-RXN-{key}", row.MedicalRecordNumber);
        Assert.Equal("React, Pat", row.PatientDisplayName);
        Assert.Equal($"U-RXN-{key}", row.UnitNumber);
        Assert.False(string.IsNullOrWhiteSpace(row.CurrentBloodType));
        Assert.Equal("O+", row.UnitBloodType);
        Assert.False(row.HasAntibodyHistory);
        Assert.True(row.WorkupIncomplete);
        Assert.Contains("Clerical check", row.WorkupHoldReason);
        Assert.True(row.RemainderQuarantined);
        Assert.False(row.AboRhIncompatible);
        Assert.Equal(IssueType.Standard, row.IssueType);
    }

    private async Task<long> ClosedInvestigationAsync(BloodBankDbContext context, string conclusions)
    {
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(context, key);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);
        await ReactionWorkupTestData.CompleteAsync(context, opened.Id, _factory.Clock.UtcNow);
        var closed = await CreateService(context).UpdateAsync(
            opened.Id,
            new UpdateReactionInvestigationRequest(
                "Febrile non-hemolytic", ReactionSeverity.Mild, "Fever 38.6 C", conclusions, null, null, false, null,
                ReactionInvestigationStatus.Closed,
                ClosedSignatureId: 7,
                ClericalCheckCompleted: true,
                VisualInspectionCompleted: true,
                DatResult: DatWorkupResult.Negative, ConcludedReactionCode: "NONE", TreatmentCodes: ["NONE"]));
        Assert.True(closed.Succeeded, closed.Error);
        return opened.Id;
    }

    // TEST-BB-327: a closed, signed investigation is locked; resending the same values is not a change.
    [Fact]
    public async Task ClosedInvestigation_RefusesChanges_ButAcceptsAnUnchangedResave()
    {
        await using var context = _factory.Create();
        var id = await ClosedInvestigationAsync(context, "Febrile reaction; no further action");

        var changed = await CreateService(context).UpdateAsync(id, new UpdateReactionInvestigationRequest(
            null, null, null, "Rewritten after signing", null, null, null, null, null));
        Assert.False(changed.Succeeded);
        Assert.Contains(ReactionInvestigationLockRule.LockedCode, changed.Error);

        var backToOpen = await CreateService(context).UpdateAsync(id, new UpdateReactionInvestigationRequest(
            null, null, null, null, null, null, null, null, ReactionInvestigationStatus.Open));
        Assert.False(backToOpen.Succeeded);

        var resave = await CreateService(context).UpdateAsync(id, new UpdateReactionInvestigationRequest(
            "Febrile non-hemolytic", ReactionSeverity.Mild, "Fever 38.6 C", "Febrile reaction; no further action", "", null, false, false, null,
            ClericalCheckCompleted: true, VisualInspectionCompleted: true, DatResult: DatWorkupResult.Negative));
        Assert.True(resave.Succeeded, resave.Error);

        await using var verify = _factory.Create();
        var row = await verify.ReactionInvestigations.FindAsync(id);
        Assert.Equal(ReactionInvestigationStatus.Closed, row!.Status);
        Assert.Equal("Febrile reaction; no further action", row.Conclusions);
        Assert.Equal(7, row.ClosedSignatureId);
    }

    [Fact]
    public async Task Reopen_NeedsReason_KeepsTheCloseInTheAudit_AndNeedsANewSignatureToClose()
    {
        await using var context = _factory.Create();
        var id = await ClosedInvestigationAsync(context, "Allergic; premedicate");

        var noReason = await CreateService(context).ReopenAsync(id, new ReopenReactionInvestigationRequest(" "));
        Assert.False(noReason.Succeeded);
        Assert.Contains(ReactionInvestigationLockRule.ReopenCode, noReason.Error);

        var reopened = await CreateService(context).ReopenAsync(id, new ReopenReactionInvestigationRequest("Culture came back positive"));
        Assert.True(reopened.Succeeded, reopened.Error);
        Assert.Equal(ReactionInvestigationStatus.UnderReview, reopened.Value!.Status);
        Assert.Null(reopened.Value.ClosedSignatureId);
        Assert.Equal("Culture came back positive", reopened.Value.ReopenReason);
        Assert.Equal(_factory.CurrentUser.UserName, reopened.Value.ReopenedBy);

        var reopenAudit = await context.AuditEvents.Where(a => a.EventType == AuditEventType.ReactionInvestigation && a.EntityId == id)
            .OrderByDescending(a => a.Id).FirstAsync();
        Assert.Equal("Culture came back positive", reopenAudit.Reason);
        Assert.Contains("\"ClosedSignatureId\":7", reopenAudit.OldValueJson);

        var twice = await CreateService(context).ReopenAsync(id, new ReopenReactionInvestigationRequest("Again"));
        Assert.False(twice.Succeeded);

        var edited = await CreateService(context).UpdateAsync(id, new UpdateReactionInvestigationRequest(
            null, null, null, "Bacterial contamination suspected", null, null, true, null, null));
        Assert.True(edited.Succeeded, edited.Error);
        var editAudit = await context.AuditEvents.Where(a => a.EventType == AuditEventType.ReactionInvestigation && a.EntityId == id)
            .OrderByDescending(a => a.Id).FirstAsync();
        Assert.Contains("Allergic; premedicate", editAudit.OldValueJson);
        Assert.Contains("Bacterial contamination suspected", editAudit.NewValueJson);

        var unsigned = await CreateService(context).UpdateAsync(id, new UpdateReactionInvestigationRequest(
            null, null, null, null, null, null, null, null, ReactionInvestigationStatus.Closed, ConcludedReactionCode: "NONE", TreatmentCodes: ["NONE"]));
        Assert.False(unsigned.Succeeded);

        var signed = await CreateService(context).UpdateAsync(id, new UpdateReactionInvestigationRequest(
            null, null, null, null, null, null, null, null, ReactionInvestigationStatus.Closed, ClosedSignatureId: 8, ConcludedReactionCode: "NONE", TreatmentCodes: ["NONE"]));
        Assert.True(signed.Succeeded, signed.Error);
        Assert.Equal(8, signed.Value!.ClosedSignatureId);
    }

    [Fact]
    public async Task ClosedFatality_StillTakesItsFollowUpReporting()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(context, key);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);
        await ReactionWorkupTestData.CompleteAsync(context, opened.Id, _factory.Clock.UtcNow);
        Assert.True((await CreateService(context).UpdateAsync(opened.Id, new UpdateReactionInvestigationRequest(
            "Hemolytic", ReactionSeverity.Fatal, null, "Fatal acute hemolytic reaction", null, null, null, true,
            ReactionInvestigationStatus.Closed, ClosedSignatureId: 9,
            ClericalCheckCompleted: true, VisualInspectionCompleted: true, DatResult: DatWorkupResult.Negative, ConcludedReactionCode: "NONE", TreatmentCodes: ["NONE"]))).Succeeded);

        var cber = await CreateService(context).RecordCberNotificationAsync(opened.Id);

        Assert.True(cber.Succeeded, cber.Error);
        Assert.Equal(ReactionInvestigationStatus.Closed, cber.Value!.Status);
    }

    [Fact]
    public async Task Update_IncompleteWorkup_DoesNotClose()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(context, key);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);

        var closed = await CreateService(context).UpdateAsync(
            opened.Id,
            new UpdateReactionInvestigationRequest(
                null, null, null, null, null, null, null, null,
                ReactionInvestigationStatus.Closed,
                ClosedSignatureId: 1, ConcludedReactionCode: "NONE", TreatmentCodes: ["NONE"]));
        Assert.False(closed.Succeeded);
        Assert.Contains(ReactionWorkupCompletenessRule.Code, closed.Error);
        Assert.Equal(ReactionInvestigationStatus.Open, (await context.ReactionInvestigations.FindAsync(opened.Id))!.Status);

        var stillDue = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.True(stillDue.WorkupIncomplete);
        Assert.Contains("Clerical check", stillDue.WorkupHoldReason);

        await ReactionWorkupTestData.CompleteAsync(context, opened.Id, _factory.Clock.UtcNow);
        var completed = await CreateService(context).UpdateAsync(
            opened.Id,
            new UpdateReactionInvestigationRequest(
                null, null, null, null, null, null, null, null,
                ReactionInvestigationStatus.Closed,
                ClosedSignatureId: 1,
                ClericalCheckCompleted: true,
                VisualInspectionCompleted: true,
                DatResult: DatWorkupResult.Negative, ConcludedReactionCode: "NONE", TreatmentCodes: ["NONE"]));
        Assert.True(completed.Succeeded, completed.Error);
        Assert.Equal(ReactionInvestigationStatus.Closed, completed.Value!.Status);
        var ready = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.False(ready.WorkupIncomplete);
    }

    // TEST-BB-446: closing needs every workup test verified; the board shows each specimen with its results.
    [Fact]
    public async Task Close_WaitsForEveryWorkupTestToBeVerified_AndTheBoardShowsTheResults()
    {
        await using var context = _factory.Create();
        var transfusion = await SeedTransfusionAsync(context, Guid.NewGuid().ToString("N")[..12], includeCurrentType: true);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);
        await ReactionWorkupTestData.CompleteAsync(context, opened.Id, _factory.Clock.UtcNow);
        var postDat = await context.TestResults.SingleAsync(r => r.OrderId == opened.PostTransfusionOrderId && r.TestCode == "DAT");
        postDat.Status = ResultStatus.Entered;
        await context.SaveChangesAsync();
        Assert.True((await CreateService(context).UpdateAsync(opened.Id, new UpdateReactionInvestigationRequest(
            null, null, null, null, null, null, null, null, null,
            ClericalCheckCompleted: true, VisualInspectionCompleted: true))).Succeeded);

        UpdateReactionInvestigationRequest Close() => new(
            null, null, null, null, null, null, null, null, ReactionInvestigationStatus.Closed, ClosedSignatureId: 4,
            ClericalCheckCompleted: true, VisualInspectionCompleted: true, ConcludedReactionCode: "NONE", TreatmentCodes: ["NONE"]);
        var waiting = await CreateService(context).UpdateAsync(opened.Id, Close());
        Assert.False(waiting.Succeeded);
        Assert.Contains("1 still to verify", waiting.Error);

        var row = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.Equal(ReactionWorkupSpecimenDto.PreRole, row.PreWorkup!.Role);
        Assert.Equal(0, row.PreWorkup.Pending);
        Assert.Equal(1, row.PostWorkup!.Pending);
        Assert.Equal(new[] { "ABORH", "DAT" }, row.PostWorkup.Lines.Select(l => l.TestCode).ToArray());
        Assert.Contains("1 still to verify", row.WorkupHoldReason);

        postDat.Status = ResultStatus.Verified;
        await context.SaveChangesAsync();
        var closed = await CreateService(context).UpdateAsync(opened.Id, Close());
        Assert.True(closed.Succeeded, closed.Error);
        Assert.Equal(ReactionInvestigationStatus.Closed, closed.Value!.Status);
    }

    // TEST-BB-446: the repeat patient type comes from the verified post-transfusion ABO/Rh, compared with the
    // current type and with the pre-transfusion specimen.
    [Fact]
    public async Task RepeatAboRh_ComesFromTheWorkupResults()
    {
        await using var context = _factory.Create();
        var transfusion = await SeedTransfusionAsync(context, Guid.NewGuid().ToString("N")[..12], includeCurrentType: true);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);
        await ReactionWorkupTestData.CompleteAsync(context, opened.Id, _factory.Clock.UtcNow);

        var agrees = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.False(agrees.RepeatPatientAboRhDiscrepancy);

        var postAbo = await context.TestResults.SingleAsync(r => r.OrderId == opened.PostTransfusionOrderId && r.TestCode == "ABORH");
        postAbo.Value = BloodBankLIS.Domain.ValueObjects.AboRhResultValue.Format(AboGroup.B, RhType.Positive);
        await context.SaveChangesAsync();

        var differs = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.True(differs.RepeatPatientAboRhDiscrepancy);
        Assert.Contains("pre-transfusion specimen", differs.RepeatAboRhDetail);
        Assert.Equal("Review the ABO/Rh discrepancy.", differs.NextStep.Text);
    }

    [Fact]
    public async Task ListDtos_SurfacesAboIncompatibilityAndEmergencyIssueContext()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(
            context, key, includeCurrentType: true, unitAbo: AboGroup.A, emergency: true);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);

        var row = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.True(row.AboRhIncompatible);
        Assert.Contains("not compatible", row.AboRhCompatibilityAlert, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(IssueType.EmergencyRelease, row.IssueType);
        Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency, row.CrossmatchStatus);
        Assert.True(row.TestsIncompleteAtIssue);
    }

    [Fact]
    public async Task ListDtos_SurfacesRepeatAboRhDiscrepancy()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(context, key, includeCurrentType: true);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);

        var updated = await CreateService(context).UpdateAsync(
            opened.Id,
            new UpdateReactionInvestigationRequest(
                null, null, null, null, null, null, null, null, null,
                RepeatPatientAboRh: "A Positive",
                RepeatUnitAboRh: "B Positive"));
        Assert.True(updated.Succeeded, updated.Error);

        var row = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.True(row.RepeatPatientAboRhDiscrepancy);
        Assert.True(row.RepeatUnitAboRhDiscrepancy);
        Assert.Contains("A+", row.RepeatAboRhDetail);
        Assert.Contains("O+", row.RepeatAboRhDetail);
        Assert.Contains("B+", row.RepeatAboRhDetail);
        Assert.False(row.AboRhIncompatible);
    }

    [Fact]
    public async Task Update_DiscardedUnit_SavesWorkupWhenRemainderCannotBeHeld()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(context, key, unitStatus: UnitStatus.Discarded);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);
        Assert.False(opened.RemainderQuarantined);

        var updated = await CreateService(context).UpdateAsync(
            opened.Id,
            new UpdateReactionInvestigationRequest(
                "FNHTR", null, "Fever after start", null, null, null, null, null, null,
                ClericalCheckCompleted: true,
                RemainderQuarantined: true));
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Contains(updated.Warnings, w => w.Code == ReactionInvestigationService.RemainderHoldCode);
        Assert.False(updated.Value!.RemainderQuarantined);
        Assert.True(updated.Value.ClericalCheckCompleted);
        Assert.Equal("FNHTR", updated.Value.ReactionType);

        var row = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.Equal(UnitStatus.Discarded, row.RemainderUnitStatus);
        Assert.Contains("Discarded", row.RemainderHoldNote);
        Assert.False(row.RemainderQuarantined);
    }

    [Fact]
    public async Task Update_FatalSeverity_StartsFatalityFollowUp()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(context, key);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);

        var severe = await CreateService(context).UpdateAsync(
            opened.Id,
            new UpdateReactionInvestigationRequest(
                "Febrile", ReactionSeverity.Severe, null, null, null, null, null, false, null));
        Assert.True(severe.Succeeded, severe.Error);
        Assert.False(severe.Value!.IsFatality);

        var marked = await CreateService(context).UpdateAsync(
            opened.Id,
            new UpdateReactionInvestigationRequest(
                "Hemolytic", ReactionSeverity.Fatal, null, null, null, null, null, false, null));
        Assert.True(marked.Succeeded, marked.Error);
        Assert.True(marked.Value!.IsFatality);
        Assert.Equal(ReactionSeverity.Fatal, marked.Value.Severity);
        Assert.Equal(FatalityNotificationStatus.Pending, marked.Value.FatalityNotificationStatus);
        Assert.Equal(_factory.Clock.UtcNow.AddDays(7), marked.Value.WrittenReportDueUtc);

        var due = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.True(due.FatalityFollowUpDue);

        var again = await CreateService(context).UpdateAsync(
            opened.Id,
            new UpdateReactionInvestigationRequest(
                "Hemolytic", ReactionSeverity.Fatal, null, null, null, null, null, false, null));
        Assert.True(again.Succeeded, again.Error);
        Assert.Equal(marked.Value.WrittenReportDueUtc, again.Value!.WrittenReportDueUtc);
    }

    [Fact]
    public async Task RecordFatalityFollowUp_SecondStamp_DoesNotRewrite()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(context, key);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);

        var notFatal = await CreateService(context).RecordCberNotificationAsync(opened.Id);
        Assert.False(notFatal.Succeeded);
        Assert.Contains("fatality", notFatal.Error, StringComparison.OrdinalIgnoreCase);

        var marked = await CreateService(context).UpdateAsync(
            opened.Id,
            new UpdateReactionInvestigationRequest(
                "Hemolytic", ReactionSeverity.Severe, null, null, null, null, null, true, null));
        Assert.True(marked.Succeeded, marked.Error);
        Assert.True(marked.Value!.IsFatality);

        var writtenFirst = await CreateService(context).RecordWrittenReportAsync(opened.Id);
        Assert.True(writtenFirst.Succeeded, writtenFirst.Error);
        Assert.NotNull(writtenFirst.Value!.WrittenReportSubmittedUtc);
        Assert.Equal(FatalityNotificationStatus.Pending, writtenFirst.Value.FatalityNotificationStatus);
        var writtenAt = writtenFirst.Value.WrittenReportSubmittedUtc;

        var cber = await CreateService(context).RecordCberNotificationAsync(opened.Id);
        Assert.True(cber.Succeeded, cber.Error);
        Assert.Equal(writtenAt, cber.Value!.WrittenReportSubmittedUtc);
        Assert.Equal(FatalityNotificationStatus.WrittenReportSubmitted, cber.Value.FatalityNotificationStatus);
        var cberAt = cber.Value.CberNotifiedUtc;

        var cberAgain = await CreateService(context).RecordCberNotificationAsync(opened.Id);
        Assert.False(cberAgain.Succeeded);
        Assert.Contains("already recorded", cberAgain.Error, StringComparison.OrdinalIgnoreCase);

        var writtenAgain = await CreateService(context).RecordWrittenReportAsync(opened.Id);
        Assert.False(writtenAgain.Succeeded);
        Assert.Contains("already recorded", writtenAgain.Error, StringComparison.OrdinalIgnoreCase);

        var stored = await context.ReactionInvestigations.SingleAsync(r => r.Id == opened.Id);
        Assert.Equal(cberAt, stored.CberNotifiedUtc);
        Assert.Equal(writtenAt, stored.WrittenReportSubmittedUtc);
        Assert.Equal(FatalityNotificationStatus.WrittenReportSubmitted, stored.FatalityNotificationStatus);

        var row = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.False(row.FatalityFollowUpDue);
    }

    [Fact]
    public async Task ListDtos_ClosedFatality_StaysDueUntilCberAndWrittenReport()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..12];
        var transfusion = await SeedTransfusionAsync(context, key);
        var opened = await CreateService(context).OpenForTransfusionAsync(transfusion);
        opened.Status = ReactionInvestigationStatus.Closed;
        opened.IsFatality = true;
        opened.FatalityNotificationStatus = FatalityNotificationStatus.Pending;
        opened.WrittenReportDueUtc = _factory.Clock.UtcNow.AddDays(7);
        await context.SaveChangesAsync();

        var due = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.True(due.FatalityFollowUpDue);
        Assert.Contains("CBER", due.FatalityFollowUpDetail);

        opened.CberNotifiedUtc = _factory.Clock.UtcNow;
        await context.SaveChangesAsync();
        var written = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.True(written.FatalityFollowUpDue);
        Assert.Contains("written report", written.FatalityFollowUpDetail);

        opened.WrittenReportSubmittedUtc = _factory.Clock.UtcNow;
        await context.SaveChangesAsync();
        var done = Assert.Single(await CreateService(context).ListDtosAsync(), r => r.Id == opened.Id);
        Assert.False(done.FatalityFollowUpDue);
    }

    private async Task<TransfusionEvent> SeedTransfusionAsync(
        BloodBankDbContext context,
        string key,
        bool includeCurrentType = false,
        AboGroup unitAbo = AboGroup.O,
        bool emergency = false,
        UnitStatus unitStatus = UnitStatus.Issued)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-RXN-{key}",
            LastName = "React",
            FirstName = "Pat",
            DateOfBirth = new DateOnly(1970, 1, 1)
        };
        var product = new ProductType { ProductCode = $"RBC-{key}", Name = "RBC" };
        context.Patients.Add(patient);
        context.ProductTypes.Add(product);
        await context.SaveChangesAsync();

        if (includeCurrentType)
        {
            context.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
            {
                PatientId = patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Source = BloodTypeSource.TestResult,
                IsCurrent = true
            });
            await context.SaveChangesAsync();
        }

        var unit = new BloodUnit
        {
            UnitNumber = $"U-RXN-{key}",
            ProductTypeId = product.Id,
            Abo = unitAbo,
            RhD = RhType.Positive,
            Status = unitStatus,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10)
        };
        context.BloodUnits.Add(unit);
        await context.SaveChangesAsync();

        var issue = new Issue
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            IssuedUtc = _factory.Clock.UtcNow,
            IssuedBy = "tech-test",
            Status = IssueStatus.Issued,
            IssueType = emergency ? IssueType.EmergencyRelease : IssueType.Standard,
            CrossmatchStatus = emergency
                ? CrossmatchClinicalStatus.NotCrossmatchedEmergency
                : CrossmatchClinicalStatus.NotPerformed,
            TestsIncompleteAtIssue = emergency
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync();

        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            ReactionSuspected = true,
            FinalDisposition = TransfusionDisposition.Stopped,
            DocumentedBy = "tech-test"
        };
        context.TransfusionEvents.Add(transfusion);
        await context.SaveChangesAsync();
        return transfusion;
    }
}
