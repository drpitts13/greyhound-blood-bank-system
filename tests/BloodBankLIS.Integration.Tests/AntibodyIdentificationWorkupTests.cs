using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AntibodyIdentificationWorkupTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AntibodyIdentificationWorkupTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task ExpiredLot_IsHardStop()
    {
        var (_, lotId) = await SeedPanelAsync(expiresOn: new DateOnly(2020, 1, 1));
        var patientId = await SeedPatientAsync("MRN-ABID-EXP");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));

        Assert.False(created.Succeeded);
        Assert.True(created.Evaluation!.IsHardStopped);
        Assert.Contains(created.Evaluation.HardStops, r => r.Code == AntibodyPanelLotValidityRule.ExpiredCode);
    }

    [Fact]
    public async Task Create_WithoutSpecimen_WarnsUnscoped()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-UNSCOPE");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);
        Assert.Contains(created.Evaluation!.Warnings, w =>
            w.Code == AntibodyIdentificationWorkupScopeRule.UnscopedCode);
    }

    [Fact]
    public async Task SecondUnscopedOpenWorkup_IsHardStop()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-DUPOPEN");

        await using (var context = _factory.Create())
        {
            var first = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(first.Succeeded, first.Error);
        }

        await using var check = _factory.Create();
        var second = await Svc(check).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.False(second.Succeeded);
        Assert.Contains(second.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.OverlappingOpenCode);
    }

    [Fact]
    public async Task LinkSpecimen_OnUnscopedWorkup_ScopesIdentification()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-LINK");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-LINK");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var linked = await Svc(context).LinkSpecimenAsync(
            created.Value!.Id, new LinkAntibodyIdSpecimenRequest(specimenId));
        Assert.True(linked.Succeeded, linked.Error);
        Assert.Equal(specimenId, linked.Value!.SpecimenId);
        Assert.DoesNotContain(linked.Evaluation!.Warnings, w =>
            w.Code == AntibodyIdentificationWorkupScopeRule.UnscopedCode);

        var listed = await Svc(context).ListWorkupsAsync(patientId);
        var row = Assert.Single(listed, w => w.Id == created.Value.Id);
        Assert.Equal(specimenId, row.SpecimenId);
        Assert.Equal("ACC-ABID-LINK", row.SpecimenAccession);
    }

    [Fact]
    public async Task ListWorkups_Unscoped_ShowsNoAccession()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-LIST");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var listed = await Svc(context).ListWorkupsAsync(patientId);
        var row = Assert.Single(listed, w => w.Id == created.Value!.Id);
        Assert.Null(row.SpecimenId);
        Assert.Null(row.SpecimenAccession);
    }

    [Fact]
    public async Task ListOpenWorkups_ShowsUnscopedAndOmitsVoided()
    {
        var (_, lotId) = await SeedPanelAsync();
        var openPatientId = await SeedPatientAsync("MRN-ABID-WL-OPEN");
        var donePatientId = await SeedPatientAsync("MRN-ABID-WL-DONE");
        var specimenId = await SeedSpecimenAsync(donePatientId, "ACC-ABID-WL-DONE");
        long openId;
        long doneId;

        await using (var context = _factory.Create())
        {
            var open = await Svc(context).CreateWorkupAsync(
                openPatientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(open.Succeeded, open.Error);
            openId = open.Value!.Id;

            var scoped = await Svc(context).CreateWorkupAsync(
                donePatientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(scoped.Succeeded, scoped.Error);
            doneId = scoped.Value!.Id;
            var voided = await Svc(context).VoidAsync(
                doneId, new VoidAntibodyIdWorkupRequest("Opened for worklist exclusion."));
            Assert.True(voided.Succeeded, voided.Error);
        }

        await using var check = _factory.Create();
        var listed = await Svc(check).ListOpenWorkupsAsync();
        Assert.Contains(listed, w => w.Id == openId && w.PatientMrn == "MRN-ABID-WL-OPEN" && w.SpecimenId is null);
        Assert.DoesNotContain(listed, w => w.Id == doneId);
        var openRow = Assert.Single(listed, w => w.Id == openId);
        Assert.Equal("Panel, Tester", openRow.PatientName);
        Assert.Null(openRow.SpecimenAccession);
    }

    [Fact]
    public async Task LinkSpecimen_WrongPatient_Fails()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-LNKPAT");
        var otherId = await SeedPatientAsync("MRN-ABID-LNKOTH");
        var otherSpecimenId = await SeedSpecimenAsync(otherId, "ACC-ABID-LNKOTH");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var linked = await Svc(context).LinkSpecimenAsync(
            created.Value!.Id, new LinkAntibodyIdSpecimenRequest(otherSpecimenId));
        Assert.False(linked.Succeeded);
        Assert.Contains("not found for this patient", linked.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LinkSpecimen_WhenAnotherOpenHasSameSpecimen_IsHardStop()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-LNKDUP");
        var specimenA = await SeedSpecimenAsync(patientId, "ACC-ABID-LNKDUP-A");
        var specimenB = await SeedSpecimenAsync(patientId, "ACC-ABID-LNKDUP-B");
        long workupA;

        await using (var context = _factory.Create())
        {
            var first = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenA, lotId));
            Assert.True(first.Succeeded, first.Error);
            workupA = first.Value!.Id;
            var second = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenB, lotId));
            Assert.True(second.Succeeded, second.Error);
        }

        await using var check = _factory.Create();
        var linked = await Svc(check).LinkSpecimenAsync(
            workupA, new LinkAntibodyIdSpecimenRequest(specimenB));
        Assert.False(linked.Succeeded);
        Assert.Contains(linked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.OverlappingOpenCode);
    }

    [Fact]
    public async Task LinkSpecimen_AfterInterpretation_WithdrawsJudgment()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-LNKSTALE");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-LNKSTALE");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using var check = _factory.Create();
        var linked = await Svc(check).LinkSpecimenAsync(
            workupId, new LinkAntibodyIdSpecimenRequest(specimenId));
        Assert.True(linked.Succeeded, linked.Error);
        Assert.Equal(specimenId, linked.Value!.SpecimenId);
        Assert.Null(linked.Value.InterpretedUtc);
        Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, linked.Value.Status);
        Assert.DoesNotContain(linked.Value.Findings, f =>
            f.Source == AntibodyIdSource.Technologist
            && f.Classification == AntibodyIdClassification.Identified);
    }

    [Fact]
    public async Task LinkSpecimen_OnCompletedWorkup_IsHardStop()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-LNKDONE");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-LNKDONE");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Agree."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
        }

        await using var check = _factory.Create();
        var linked = await Svc(check).LinkSpecimenAsync(
            workupId, new LinkAntibodyIdSpecimenRequest(specimenId));
        Assert.False(linked.Succeeded);
        Assert.Contains(linked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.SpecimenLinkCode);
    }

    [Fact]
    public async Task LinkSpecimen_OnVoidedWorkup_IsHardStop()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-LNKVOID");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-LNKVOID");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            var voided = await Svc(context).VoidAsync(
                workupId, new VoidAntibodyIdWorkupRequest("Opened without a specimen."));
            Assert.True(voided.Succeeded, voided.Error);
        }

        await using var check = _factory.Create();
        var linked = await Svc(check).LinkSpecimenAsync(
            workupId, new LinkAntibodyIdSpecimenRequest(specimenId));
        Assert.False(linked.Succeeded);
        Assert.Contains(linked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.SpecimenLinkCode);
    }

    [Fact]
    public async Task Create_OnRejectedSpecimen_IsHardStop()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-REJCRT");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-REJCRT", SpecimenStatus.Rejected);

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
        Assert.False(created.Succeeded);
        Assert.Contains(created.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.SpecimenUnusableCode);
    }

    [Fact]
    public async Task LinkSpecimen_Rejected_IsHardStop()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-REJLNK");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-REJLNK", SpecimenStatus.Cancelled);

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var linked = await Svc(context).LinkSpecimenAsync(
            created.Value!.Id, new LinkAntibodyIdSpecimenRequest(specimenId));
        Assert.False(linked.Succeeded);
        Assert.Contains(linked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.SpecimenUnusableCode);
    }

    [Fact]
    public async Task LinkSpecimen_ClockExpired_IsHardStop()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-EXPLNK");
        var specimenId = await SeedSpecimenAsync(
            patientId, "ACC-ABID-EXPLNK", expiresUtc: _factory.Clock.UtcNow.AddHours(-1));

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var linked = await Svc(context).LinkSpecimenAsync(
            created.Value!.Id, new LinkAntibodyIdSpecimenRequest(specimenId));
        Assert.False(linked.Succeeded);
        Assert.Contains(linked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.SpecimenExpiredCode);
    }

    [Fact]
    public async Task Create_OnCollectedSpecimen_IsHardStop()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-COLCRT");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-COLCRT", SpecimenStatus.Collected);

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
        Assert.False(created.Succeeded);
        Assert.Contains(created.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.SpecimenNotReadyCode);
    }

    [Fact]
    public async Task Create_OnReceivedSpecimen_Warns()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-RECCRT");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-RECCRT", SpecimenStatus.Received);

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
        Assert.True(created.Succeeded, created.Error);
        Assert.Contains(created.Evaluation!.Warnings, w =>
            w.Code == AntibodyIdentificationWorkupScopeRule.SpecimenUnacceptedCode);
    }

    [Fact]
    public async Task LinkSpecimen_Collected_IsHardStop()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-COLLNK");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-COLLNK", SpecimenStatus.Collected);

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var linked = await Svc(context).LinkSpecimenAsync(
            created.Value!.Id, new LinkAntibodyIdSpecimenRequest(specimenId));
        Assert.False(linked.Succeeded);
        Assert.Contains(linked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.SpecimenNotReadyCode);
    }

    [Fact]
    public async Task Complete_AfterSpecimenCollected_IsHardStop()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-COLCMP");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-COLCMP");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var specimen = await context.Specimens.SingleAsync(s => s.Id == specimenId);
            specimen.Status = SpecimenStatus.Collected;
            await context.SaveChangesAsync();
        }

        await using var check = _factory.Create();
        var reviewed = await Svc(check, new TestCurrentUser("supervisor-abid", "WS-2"))
            .ReviewAsync(workupId, AcceptReview("Agree."));
        Assert.False(reviewed.Succeeded);
        Assert.Contains(reviewed.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.SpecimenNotReadyCode);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task Complete_OnReceivedSpecimen_RequiresAcknowledgment()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-RECCMP");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-RECCMP", SpecimenStatus.Received);
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var blocked = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, new ReviewAntibodyIdWorkupRequest(true, "Agree."));
            Assert.False(blocked.Succeeded);
            Assert.Contains(blocked.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationInterpretationRule.ReviewAckCode);
            Assert.Contains(blocked.Evaluation.Warnings, w =>
                w.Code == AntibodyIdentificationWorkupScopeRule.SpecimenUnacceptedCode);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree. Specimen received, not yet accepted."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
        }

        await using var check = _factory.Create();
        var withoutAck = await Svc(check).CompleteAsync(workupId);
        Assert.False(withoutAck.Succeeded);
        Assert.Contains(withoutAck.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.CompleteAckCode);
        Assert.Contains(withoutAck.Evaluation.Warnings, w =>
            w.Code == AntibodyIdentificationWorkupScopeRule.SpecimenUnacceptedCode);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());

        var completed = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.True(completed.Succeeded, completed.Error);
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
    }

    [Fact]
    public async Task Complete_AfterSpecimenRejected_IsHardStop()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-REJCMP");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-REJCMP");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var specimen = await context.Specimens.SingleAsync(s => s.Id == specimenId);
            specimen.Status = SpecimenStatus.Rejected;
            await context.SaveChangesAsync();
        }

        await using var check = _factory.Create();
        var reviewed = await Svc(check, new TestCurrentUser("supervisor-abid", "WS-2"))
            .ReviewAsync(workupId, AcceptReview("Agree."));
        Assert.False(reviewed.Succeeded);
        Assert.Contains(reviewed.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationWorkupScopeRule.SpecimenUnusableCode);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task Assist_DoesNotPostHistory_OrIdentify()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ASSIST");
        long workupId;
        long kellPosCell;
        long kellNegCell;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            kellPosCell = created.Value.Cells.First(c =>
                c.Role == PanelCellRole.Panel && c.Antigens.Any(a => a.AntigenCode == "K" && a.Expression != AntigenExpression.Absent)).CellId;
            kellNegCell = created.Value.Cells.First(c =>
                c.Role == PanelCellRole.Panel && c.Antigens.Any(a => a.AntigenCode == "K" && a.Expression == AntigenExpression.Absent)).CellId;
        }

        await using (var context = _factory.Create())
        {
            var recorded = await Svc(context).RecordReactionsAsync(workupId,
            [
                new RecordAntibodyIdReactionRequest(kellPosCell, "AHG", ReactionGrade.ThreePlus),
                new RecordAntibodyIdReactionRequest(kellNegCell, "AHG", ReactionGrade.Negative)
            ]);
            Assert.True(recorded.Succeeded, recorded.Error);

            var assist = await Svc(context).RunAssistAsync(workupId);
            Assert.True(assist.Succeeded, assist.Error);
            Assert.DoesNotContain(assist.Value!.Findings, f => f.Classification == AntibodyIdClassification.Identified);
            Assert.Contains(assist.Evaluation!.Results, r => r.Code == AntibodyIdentificationInterpretationRule.AssistAdvisoryCode);
        }

        await using var check = _factory.Create();
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        Assert.True(attrId > 0);
    }

    [Fact]
    public async Task ReactionChange_RefreshesAssistanceWithoutIdentifying()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ASSTALE");
        long workupId;
        long kellPosCell;
        long kellNegCell;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            kellPosCell = created.Value.Cells.First(c =>
                c.Role == PanelCellRole.Panel && c.Antigens.Any(a => a.AntigenCode == "K" && a.Expression != AntigenExpression.Absent)).CellId;
            kellNegCell = created.Value.Cells.First(c =>
                c.Role == PanelCellRole.Panel && c.Antigens.Any(a => a.AntigenCode == "K" && a.Expression == AntigenExpression.Absent)).CellId;
        }

        await using (var context = _factory.Create())
        {
            var reactive = await Svc(context).RecordReactionsAsync(workupId,
            [
                new RecordAntibodyIdReactionRequest(kellPosCell, "AHG", ReactionGrade.ThreePlus),
                new RecordAntibodyIdReactionRequest(kellNegCell, "AHG", ReactionGrade.Negative)
            ]);
            Assert.True(reactive.Succeeded, reactive.Error);
            var first = Assert.Single(reactive.Value!.Findings, f =>
                f.Source == AntibodyIdSource.Assist && f.Specificity == "anti-K");
            Assert.NotEqual(AntibodyIdClassification.Excluded, first.Classification);
            Assert.NotEqual(AntibodyIdClassification.Identified, first.Classification);
        }

        await using var check = _factory.Create();
        var cleared = await Svc(check).RecordReactionsAsync(workupId,
        [
            new RecordAntibodyIdReactionRequest(kellPosCell, "AHG", ReactionGrade.Negative)
        ]);
        Assert.True(cleared.Succeeded, cleared.Error);
        var current = Assert.Single(cleared.Value!.Findings, f =>
            f.Source == AntibodyIdSource.Assist && f.Specificity == "anti-K");
        Assert.Equal(AntibodyIdClassification.Excluded, current.Classification);
        Assert.DoesNotContain(cleared.Value.Findings, f => f.Classification == AntibodyIdClassification.Identified);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task Complete_WithoutInterpretation_IsHardStop()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-NOINT");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var completed = await Svc(context).CompleteAsync(created.Value!.Id);
        Assert.False(completed.Succeeded);
        Assert.Contains(completed.Evaluation!.HardStops, r => r.Code == AntibodyIdentificationInterpretationRule.InterpretationRequiredCode);
        Assert.Empty(await context.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task ReviewedTechnologistIdentification_PostsHistory()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-OK");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);

            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified by pattern and selected-cell confirmation.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Technologist identification")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var supervisor = Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"));
            var reviewed = await supervisor.ReviewAsync(workupId, AcceptReview( "Agree with anti-K."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
        }

        await using (var context = _factory.Create())
        {
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
            Assert.Equal(AntibodyWorkupStatus.Completed, completed.Value!.Status);
        }

        await using var check = _factory.Create();
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
        Assert.Equal(attrId, history.BloodAttributeDefinitionId);
        Assert.True(await check.AuditEvents.AnyAsync(e =>
            e.EntityType == nameof(AntibodyHistory) && e.EventType == AuditEventType.Antibody && e.EntityId == patientId));
    }

    [Fact]
    public async Task Complete_DoesNotDuplicateCatalogHistoryRow()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-DEDUP");

        await using (var context = _factory.Create())
        {
            context.AntibodyHistory.Add(new AntibodyHistory
            {
                PatientId = patientId,
                BloodAttributeDefinitionId = attrId,
                AntibodySpecificity = "anti-Kell",
                Status = AntibodyStatus.Identified,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        long workupId;
        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);

            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Technologist identification")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Agree."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
        }

        await using (var context = _factory.Create())
        {
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
        }

        await using var check = _factory.Create();
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-Kell", history.AntibodySpecificity);
        Assert.Equal(attrId, history.BloodAttributeDefinitionId);
    }

    [Fact]
    public async Task Complete_FreeTextSpecificity_ResolvesCatalogId()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-CAT");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);

            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified by pattern.",
                [new AntibodyIdInterpretationItem(null, "anti-K", AntibodyIdClassification.Identified, "Technologist identification")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
            var finding = Assert.Single(interpreted.Value!.Findings.Where(f => f.Source == AntibodyIdSource.Technologist));
            Assert.Equal(attrId, finding.BloodAttributeDefinitionId);
            Assert.Equal("anti-K", finding.Specificity);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Agree."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
        }

        await using var check = _factory.Create();
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal(attrId, history.BloodAttributeDefinitionId);
        Assert.Equal("anti-K", history.AntibodySpecificity);
    }

    [Fact]
    public async Task CompatLoader_ResolvesFreeTextHistoryToCatalog()
    {
        var (attrId, _) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-COMPAT");

        await using (var context = _factory.Create())
        {
            context.AntibodyHistory.Add(new AntibodyHistory
            {
                PatientId = patientId,
                AntibodySpecificity = "anti-K",
                Status = AntibodyStatus.Identified,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        await using var check = _factory.Create();
        var loader = new BloodAttributeCompatLoader(
            new EfRepository<AntibodyHistory>(check),
            new EfRepository<AntigenProfile>(check),
            new EfRepository<UnitBloodAttribute>(check),
            new EfRepository<BloodAttributeDefinition>(check));
        var snapshot = await loader.LoadAsync(patientId, unitId: 1);
        Assert.Contains(snapshot.PatientSignificantAntibodies, a => a.Code == "K" && a.AntibodyName == "anti-K");
        Assert.True(attrId > 0);
    }

    [Fact]
    public async Task Interpret_IdentifiedOnPhenotypePositive_Warns()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-PHENO");

        await using (var context = _factory.Create())
        {
            context.AntigenProfiles.Add(new AntigenProfile
            {
                PatientId = patientId,
                BloodAttributeDefinitionId = attrId,
                Result = AntigenResult.Positive,
                Method = "Serologic"
            });
            await context.SaveChangesAsync();
        }

        await using var check = _factory.Create();
        var created = await Svc(check).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var interpreted = await Svc(check).RecordInterpretationAsync(created.Value!.Id, new RecordAntibodyIdInterpretationRequest(
            "anti-K identified.",
            [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
        Assert.True(interpreted.Succeeded, interpreted.Error);
        Assert.Contains(interpreted.Evaluation!.Warnings, w =>
            w.Code == AntibodyIdentificationInterpretationRule.IdentifiedPhenotypeConflictCode);
    }

    [Fact]
    public async Task Assist_MolecularMethod_UsesGenotypeConflict()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-GENO");
        long workupId;
        long kellPosCell;
        long kellNegCell;

        await using (var context = _factory.Create())
        {
            context.AntigenProfiles.Add(new AntigenProfile
            {
                PatientId = patientId,
                BloodAttributeDefinitionId = attrId,
                Result = AntigenResult.Positive,
                Method = "Molecular"
            });
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            kellPosCell = created.Value.Cells.First(c =>
                c.Role == PanelCellRole.Panel && c.Antigens.Any(a => a.AntigenCode == "K" && a.Expression != AntigenExpression.Absent)).CellId;
            kellNegCell = created.Value.Cells.First(c =>
                c.Role == PanelCellRole.Panel && c.Antigens.Any(a => a.AntigenCode == "K" && a.Expression == AntigenExpression.Absent)).CellId;
        }

        await using var check = _factory.Create();
        var recorded = await Svc(check).RecordReactionsAsync(workupId,
        [
            new RecordAntibodyIdReactionRequest(kellPosCell, "AHG", ReactionGrade.ThreePlus),
            new RecordAntibodyIdReactionRequest(kellNegCell, "AHG", ReactionGrade.Negative)
        ]);
        Assert.True(recorded.Succeeded, recorded.Error);
        var assist = await Svc(check).RunAssistAsync(workupId);
        Assert.True(assist.Succeeded, assist.Error);
        Assert.Contains(assist.Evaluation!.Warnings, w =>
            w.Code == AntibodyIdentificationAssistEvaluator.GenotypeConflictCode);
        Assert.True(attrId > 0);
    }

    [Fact]
    public async Task Interpret_UnmatchedIdentified_Warns()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-VEL");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var interpreted = await Svc(context).RecordInterpretationAsync(created.Value!.Id, new RecordAntibodyIdInterpretationRequest(
            "anti-Vel identified.",
            [new AntibodyIdInterpretationItem(null, "anti-Vel", AntibodyIdClassification.Identified, null)]));
        Assert.True(interpreted.Succeeded, interpreted.Error);
        Assert.Contains(interpreted.Evaluation!.Warnings, w =>
            w.Code == AntibodyIdentificationCatalogResolver.UnmatchedIdentifiedCode);
        var finding = Assert.Single(interpreted.Value!.Findings.Where(f => f.Source == AntibodyIdSource.Technologist));
        Assert.Null(finding.BloodAttributeDefinitionId);
        Assert.Equal("anti-Vel", finding.Specificity);
    }

    [Fact]
    public async Task Void_DoesNotPostHistory()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-VOID");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var interpreted = await Svc(context).RecordInterpretationAsync(created.Value!.Id, new RecordAntibodyIdInterpretationRequest(
            "anti-K identified.",
            [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
        Assert.True(interpreted.Succeeded, interpreted.Error);

        var voided = await Svc(context).VoidAsync(
            created.Value.Id, new VoidAntibodyIdWorkupRequest("Opened on the wrong specimen."));
        Assert.True(voided.Succeeded, voided.Error);
        Assert.Equal(AntibodyWorkupStatus.Voided, voided.Value!.Status);
        Assert.Equal("Opened on the wrong specimen.", voided.Value.VoidReason);
        Assert.Empty(await context.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task Completed_CannotBeVoided()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-NOVOID");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Agree."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
        }

        await using var check = _factory.Create();
        var voided = await Svc(check).VoidAsync(workupId, new VoidAntibodyIdWorkupRequest("changed mind"));
        Assert.False(voided.Succeeded);
        Assert.Contains(voided.Evaluation!.HardStops, r => r.Code == AntibodyIdentificationInterpretationRule.VoidCompletedCode);
        Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
    }

    [Fact]
    public async Task AttachSelectedLot_AddsCells()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var selectedId = await SeedExtraLotAsync(attrId, selected: true);
        var patientId = await SeedPatientAsync("MRN-ABID-SEL");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);
        Assert.DoesNotContain(created.Value!.Cells, c => c.Role == PanelCellRole.Selected);

        var attached = await Svc(context).AttachLotsAsync(
            created.Value.Id, new AttachAntibodyIdLotsRequest([selectedId]));
        Assert.True(attached.Succeeded, attached.Error);
        Assert.Contains(attached.Value!.Cells, c => c.Role == PanelCellRole.Selected);
        Assert.Contains(attached.Value.Lots, l => l.Id == selectedId && l.IsSelectedCellLot);
    }

    [Fact]
    public async Task AttachExpiredLot_IsHardStop()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var expiredId = await SeedExtraLotAsync(attrId, selected: true, expiresOn: new DateOnly(2020, 1, 1));
        var patientId = await SeedPatientAsync("MRN-ABID-SELEXP");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var attached = await Svc(context).AttachLotsAsync(
            created.Value!.Id, new AttachAntibodyIdLotsRequest([expiredId]));
        Assert.False(attached.Succeeded);
        Assert.Contains(attached.Evaluation!.HardStops, r => r.Code == AntibodyPanelLotValidityRule.ExpiredCode);
        Assert.DoesNotContain((await Svc(context).GetWorkupAsync(created.Value.Id))!.Cells, c => c.Role == PanelCellRole.Selected);
    }

    [Fact]
    public async Task Complete_NoIdentified_WarnsAndPostsNothing()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-NONE");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "No alloantibody identified.", []));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Agree none identified."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
            Assert.Contains(completed.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationInterpretationRule.CompleteNoneCode);
            Assert.Contains(completed.Evaluation.Warnings, w =>
                w.Code == AntibodyIdentificationAssistEvaluator.IncompleteReactionsCode);
            Assert.Contains(completed.Evaluation.Results, r =>
                r.Code == AntibodyIdentificationInterpretationRule.CompleteAckCode
                && r.Severity == RuleSeverity.Pass);
        }

        await using var check = _factory.Create();
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task Complete_NoIdentified_WithoutAcknowledgment_HardStops()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-NOACK");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "No alloantibody identified.", []));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using var check = _factory.Create();
        var reviewed = await Svc(check, new TestCurrentUser("supervisor-abid", "WS-2"))
            .ReviewAsync(workupId, AcceptReview( "Agree none identified."));
        Assert.True(reviewed.Succeeded, reviewed.Error);
        var blocked = await Svc(check).CompleteAsync(workupId);
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.CompleteAckCode);
        Assert.Contains(blocked.Evaluation.Warnings, w =>
            w.Code == AntibodyIdentificationInterpretationRule.CompleteNoneCode);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task Review_NoIdentified_WithoutAcknowledgment_HardStops()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-REVACK");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "No alloantibody identified.", []));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using var check = _factory.Create();
        var blocked = await Svc(check, new TestCurrentUser("supervisor-abid", "WS-2"))
            .ReviewAsync(workupId, new ReviewAntibodyIdWorkupRequest(true, "Agree none identified."));
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.ReviewAckCode);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task Complete_WithPriorHistory_WarnsHistoryRemains()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-HIST");

        await using (var context = _factory.Create())
        {
            context.AntibodyHistory.Add(new AntibodyHistory
            {
                PatientId = patientId,
                BloodAttributeDefinitionId = attrId,
                AntibodySpecificity = "anti-K",
                Status = AntibodyStatus.Identified,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        long workupId;
        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "Historical anti-K not currently detected.", []));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Agree."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
            Assert.Contains(completed.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationInterpretationRule.HistoryRemainsCode);
            Assert.Contains(completed.Evaluation.Warnings, w =>
                w.Code == AntibodyIdentificationAssistEvaluator.HistoricalUndetectedCode);
        }

        await using var check = _factory.Create();
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
        Assert.Equal(attrId, history.BloodAttributeDefinitionId);
    }

    [Fact]
    public async Task Complete_AutocontrolPositiveWithoutDat_Warns()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ACDAT");
        long workupId;
        long acCell;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            acCell = created.Value.Cells.First(c => c.Role == PanelCellRole.Autocontrol).CellId;
            var recorded = await Svc(context).RecordReactionsAsync(workupId,
            [
                new RecordAntibodyIdReactionRequest(acCell, "AHG", ReactionGrade.TwoPlus)
            ]);
            Assert.True(recorded.Succeeded, recorded.Error);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "Autocontrol reactive; DAT pending.", []));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Noted."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
            Assert.Contains(completed.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationAssistEvaluator.DatIndicatedCode);
        }
    }

    [Fact]
    public async Task ReactionsAfterReview_RequireReinterpretation()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-STALE");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            var detail = await Svc(context).GetWorkupAsync(workupId);
            var recorded = await Svc(context).RecordReactionsAsync(workupId, PanelAhg(detail!));
            Assert.True(recorded.Succeeded, recorded.Error);
            Assert.Null(recorded.Value!.InterpretedUtc);
            Assert.Null(recorded.Value.ReviewedUtc);
            Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, recorded.Value.Status);
            Assert.DoesNotContain(recorded.Value.Findings, f =>
                f.Source == AntibodyIdSource.Technologist
                && f.Classification == AntibodyIdClassification.Identified);
            var reviewBlocked = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Should not accept stale panel."));
            Assert.False(reviewBlocked.Succeeded);
            Assert.Contains(reviewBlocked.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationInterpretationRule.InterpretationStaleCode);
        }

        await using (var context = _factory.Create())
        {
            var blocked = await Svc(context).CompleteAsync(workupId);
            Assert.False(blocked.Succeeded);
            Assert.Contains(blocked.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationInterpretationRule.InterpretationRequiredCode);
        }

        await using (var context = _factory.Create())
        {
            var reinterpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified after panel update.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(reinterpreted.Succeeded, reinterpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var rereviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Agree after panel update."));
            Assert.True(rereviewed.Succeeded, rereviewed.Error);
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
        }

        await using var check = _factory.Create();
        Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
    }

    [Fact]
    public async Task Review_IdentifiedWithoutPanelReactions_HardStops()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-INCOMPLETE");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using var check = _factory.Create();
        var reviewed = await Svc(check, new TestCurrentUser("supervisor-abid", "WS-2"))
            .ReviewAsync(workupId, AcceptReview("Agree."));
        Assert.False(reviewed.Succeeded);
        Assert.Contains(reviewed.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationAssistEvaluator.IncompleteReactionsCode);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task Interpret_IdentifiedWhenPanelExcludes_Warns()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-EXCL");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);
        await RecordPanelAhgAsync(context, created.Value!.Id, created.Value);

        var interpreted = await Svc(context).RecordInterpretationAsync(created.Value.Id, new RecordAntibodyIdInterpretationRequest(
            "anti-K identified.",
            [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
        Assert.True(interpreted.Succeeded, interpreted.Error);
        Assert.Contains(interpreted.Evaluation!.Warnings, w =>
            w.Code == AntibodyIdentificationInterpretationRule.IdentifiedExcludedCode);
    }

    [Fact]
    public async Task Complete_IdentifiedWhenPanelExcludes_WarnsAndPosts()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-EXCLPOST");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Agree despite exclusion."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
            Assert.Contains(completed.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationInterpretationRule.IdentifiedExcludedCode);
        }

        await using var check = _factory.Create();
        Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
    }

    [Fact]
    public async Task Complete_IdentifiedWhileAnotherCannotExclude_Warns()
    {
        var (kellId, eId, lotId) = await SeedKellAndEPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-UNEX");
        long workupId;
        long kellPos;
        long kellNeg;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            kellPos = created.Value.Cells.First(c =>
                c.Role == PanelCellRole.Panel && c.Antigens.Any(a => a.AntigenCode == "K" && a.Expression != AntigenExpression.Absent)).CellId;
            kellNeg = created.Value.Cells.First(c =>
                c.Role == PanelCellRole.Panel && c.Antigens.Any(a => a.AntigenCode == "K" && a.Expression == AntigenExpression.Absent)).CellId;
            var recorded = await Svc(context).RecordReactionsAsync(workupId,
            [
                new RecordAntibodyIdReactionRequest(kellPos, "AHG", ReactionGrade.ThreePlus),
                new RecordAntibodyIdReactionRequest(kellNeg, "AHG", ReactionGrade.Negative)
            ]);
            Assert.True(recorded.Succeeded, recorded.Error);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified; anti-E not excluded.",
                [new AntibodyIdInterpretationItem(kellId, "anti-K", AntibodyIdClassification.Identified, null)]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
            Assert.Contains(interpreted.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationInterpretationRule.UnexcludedCode);
            Assert.Contains(interpreted.Evaluation.Warnings, w =>
                w.Code == AntibodyIdentificationAssistEvaluator.SelectedCellNeededCode);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview( "Agree anti-K."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
            Assert.Contains(completed.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationInterpretationRule.UnexcludedCode);
            Assert.Contains(completed.Evaluation.Warnings, w =>
                w.Code == AntibodyIdentificationAssistEvaluator.SelectedCellNeededCode);
        }

        await using var check = _factory.Create();
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
        Assert.Equal(kellId, history.BloodAttributeDefinitionId);
        Assert.True(eId > 0);
    }

    [Fact]
    public async Task DatChangeAfterInterpretation_RequiresReinterpretation()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-DATSTALE");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "No alloantibody identified.", []));
            Assert.True(interpreted.Succeeded, interpreted.Error);
            var dat = await Svc(context).RecordDatAsync(
                workupId, new RecordAntibodyIdDatRequest(AntibodyIdDatResult.PositiveIgG, "IgG card"));
            Assert.True(dat.Succeeded, dat.Error);
            Assert.Null(dat.Value!.InterpretedUtc);
            Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, dat.Value.Status);
        }
    }

    [Fact]
    public async Task SameUserCannotReview()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-SELF");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var interpreted = await Svc(context).RecordInterpretationAsync(created.Value!.Id, new RecordAntibodyIdInterpretationRequest(
            "anti-K identified.",
            [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
        Assert.True(interpreted.Succeeded, interpreted.Error);

        var reviewed = await Svc(context).ReviewAsync(created.Value.Id, AcceptReview( "self"));
        Assert.False(reviewed.Succeeded);
        Assert.Contains(reviewed.Evaluation!.HardStops, r => r.Code == AntibodyIdentificationInterpretationRule.ReviewSelfCode);
    }

    private static IReadOnlyList<RecordAntibodyIdReactionRequest> PanelAhg(AntibodyIdWorkupDetailDto workup) =>
        workup.Cells
            .Where(c => c.Role != PanelCellRole.Autocontrol)
            .Select(c => new RecordAntibodyIdReactionRequest(c.CellId, "AHG", ReactionGrade.Negative))
            .ToList();

    private async Task RecordPanelAhgAsync(
        BloodBankDbContext context, long workupId, AntibodyIdWorkupDetailDto workup)
    {
        var recorded = await Svc(context).RecordReactionsAsync(workupId, PanelAhg(workup));
        Assert.True(recorded.Succeeded, recorded.Error);
    }

    private AntibodyIdentificationService Svc(BloodBankDbContext c, ICurrentUser? user = null)
    {
        var current = user ?? _factory.CurrentUser;
        return new AntibodyIdentificationService(
            new EfRepository<AntibodyPanelManufacturer>(c),
            new EfRepository<AntibodyPanelLot>(c),
            new EfRepository<AntibodyPanelCell>(c),
            new EfRepository<AntibodyPanelCellAntigen>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            new EfRepository<AntibodyIdentificationWorkupLot>(c),
            new EfRepository<AntibodyIdentificationReaction>(c),
            new EfRepository<AntibodyIdentificationFinding>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            c,
            _factory.Clock,
            current,
            new AuditWriter(c, _factory.Clock, current),
            results: new EfRepository<TestResult>(c),
            testDefinitions: new EfRepository<TestDefinition>(c));
    }

    private static CompleteAntibodyIdWorkupRequest ReviewedWarnings() =>
        new("Reviewed leftover CannotExclude, history, DAT, none-identified, and conflicting findings.");

    private static ReviewAntibodyIdWorkupRequest AcceptReview(string comment = "Agree.") =>
        new(true, comment, ReviewedWarnings().WarningAcknowledgment);

    private async Task<long> SeedPatientAsync(string mrn)
    {
        await using var context = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = mrn,
            LastName = "Panel",
            FirstName = "Tester",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Sex = Sex.Unknown
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        return patient.Id;
    }

    private async Task<long> SeedSpecimenAsync(
        long patientId,
        string accession,
        SpecimenStatus status = SpecimenStatus.Accepted,
        DateTime? expiresUtc = null)
    {
        await using var context = _factory.Create();
        var specimen = new Specimen
        {
            AccessionNumber = accession,
            PatientId = patientId,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = expiresUtc ?? _factory.Clock.UtcNow.AddDays(3),
            Status = status
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync();
        return specimen.Id;
    }

    private async Task<(long KellId, long LotId)> SeedPanelAsync(DateOnly? expiresOn = null)
    {
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var kell = await context.BloodAttributeDefinitions.FirstOrDefaultAsync(a => a.Code == "K");
        if (kell is null)
        {
            kell = new BloodAttributeDefinition
            {
                Code = "K",
                Name = "Kell",
                AntibodyName = "anti-K",
                IsClinicallySignificant = true,
                SortOrder = 1,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now,
                Version = 1
            };
            context.BloodAttributeDefinitions.Add(kell);
            await context.SaveChangesAsync();
        }

        var manufacturer = new AntibodyPanelManufacturer
        {
            Code = $"M-{Guid.NewGuid():N}"[..12],
            Name = "Test Manufacturer",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        };
        context.AntibodyPanelManufacturers.Add(manufacturer);
        await context.SaveChangesAsync();

        var lot = new AntibodyPanelLot
        {
            ManufacturerId = manufacturer.Id,
            LotNumber = $"LOT-{Guid.NewGuid():N}"[..12],
            ExpiresOn = expiresOn ?? new DateOnly(2027, 12, 31),
            PanelName = "Test panel",
            IsActive = true
        };
        context.AntibodyPanelLots.Add(lot);
        await context.SaveChangesAsync();

        var pos = new AntibodyPanelCell { LotId = lot.Id, CellNumber = "1", Role = PanelCellRole.Panel, SortOrder = 1 };
        var neg = new AntibodyPanelCell { LotId = lot.Id, CellNumber = "2", Role = PanelCellRole.Panel, SortOrder = 2 };
        var ac = new AntibodyPanelCell { LotId = lot.Id, CellNumber = "AC", Role = PanelCellRole.Autocontrol, SortOrder = 3 };
        context.AntibodyPanelCells.AddRange(pos, neg, ac);
        await context.SaveChangesAsync();

        context.AntibodyPanelCellAntigens.AddRange(
            new AntibodyPanelCellAntigen { CellId = pos.Id, BloodAttributeDefinitionId = kell.Id, Expression = AntigenExpression.Present },
            new AntibodyPanelCellAntigen { CellId = neg.Id, BloodAttributeDefinitionId = kell.Id, Expression = AntigenExpression.Absent });
        await context.SaveChangesAsync();

        return (kell.Id, lot.Id);
    }

    private async Task<(long KellId, long EId, long LotId)> SeedKellAndEPanelAsync()
    {
        var (kellId, lotId) = await SeedPanelAsync();
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var e = await context.BloodAttributeDefinitions.FirstOrDefaultAsync(a => a.Code == "E");
        if (e is null)
        {
            e = new BloodAttributeDefinition
            {
                Code = "E",
                Name = "Rh E",
                AntibodyName = "anti-E",
                IsClinicallySignificant = true,
                SortOrder = 2,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now,
                Version = 1
            };
            context.BloodAttributeDefinitions.Add(e);
            await context.SaveChangesAsync();
        }

        var cells = await context.AntibodyPanelCells
            .Where(c => c.LotId == lotId && c.Role == PanelCellRole.Panel)
            .ToListAsync();
        foreach (var cell in cells)
        {
            context.AntibodyPanelCellAntigens.Add(new AntibodyPanelCellAntigen
            {
                CellId = cell.Id,
                BloodAttributeDefinitionId = e.Id,
                Expression = AntigenExpression.Heterozygous
            });
        }

        await context.SaveChangesAsync();
        return (kellId, e.Id, lotId);
    }

    private async Task<long> SeedExtraLotAsync(long kellId, bool selected, DateOnly? expiresOn = null)
    {
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var manufacturer = new AntibodyPanelManufacturer
        {
            Code = $"M-{Guid.NewGuid():N}"[..12],
            Name = "Selected Manufacturer",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        };
        context.AntibodyPanelManufacturers.Add(manufacturer);
        await context.SaveChangesAsync();

        var lot = new AntibodyPanelLot
        {
            ManufacturerId = manufacturer.Id,
            LotNumber = $"SEL-{Guid.NewGuid():N}"[..12],
            ExpiresOn = expiresOn ?? new DateOnly(2027, 12, 31),
            PanelName = "Selected cells",
            IsSelectedCellLot = selected,
            IsActive = true
        };
        context.AntibodyPanelLots.Add(lot);
        await context.SaveChangesAsync();

        var cell = new AntibodyPanelCell
        {
            LotId = lot.Id,
            CellNumber = "S1",
            Role = selected ? PanelCellRole.Selected : PanelCellRole.Panel,
            SortOrder = 1
        };
        context.AntibodyPanelCells.Add(cell);
        await context.SaveChangesAsync();
        context.AntibodyPanelCellAntigens.Add(new AntibodyPanelCellAntigen
        {
            CellId = cell.Id,
            BloodAttributeDefinitionId = kellId,
            Expression = AntigenExpression.Homozygous
        });
        await context.SaveChangesAsync();
        return lot.Id;
    }
}
