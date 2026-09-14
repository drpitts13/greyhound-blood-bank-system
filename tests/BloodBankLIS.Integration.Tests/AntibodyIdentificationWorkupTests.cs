using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
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
    public async Task Complete_AfterLotExpires_RequiresAcknowledgment()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-LOTEXP");
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var lot = await context.AntibodyPanelLots.SingleAsync(l => l.Id == lotId);
            lot.ExpiresOn = DateOnly.FromDateTime(_factory.Clock.UtcNow.Date.AddDays(-1));
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            var blocked = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, new ReviewAntibodyIdWorkupRequest(true, "Agree."));
            Assert.False(blocked.Succeeded);
            Assert.Contains(blocked.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationInterpretationRule.ReviewAckCode);
            Assert.Contains(blocked.Evaluation.Warnings, w =>
                w.Code == AntibodyPanelLotValidityRule.ExpiredCode);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree. Lot expired after reactions."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
        }

        await using var check = _factory.Create();
        var withoutAck = await Svc(check).CompleteAsync(workupId);
        Assert.False(withoutAck.Succeeded);
        Assert.Contains(withoutAck.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.CompleteAckCode);
        Assert.Contains(withoutAck.Evaluation.Warnings, w =>
            w.Code == AntibodyPanelLotValidityRule.ExpiredCode);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());

        var completed = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.True(completed.Succeeded, completed.Error);
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
    }

    [Fact]
    public async Task Review_AfterLotDeactivated_IsHardStop()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-LOTINA");
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var lot = await context.AntibodyPanelLots.SingleAsync(l => l.Id == lotId);
            lot.IsActive = false;
            await context.SaveChangesAsync();
        }

        await using var check = _factory.Create();
        var reviewed = await Svc(check, new TestCurrentUser("supervisor-abid", "WS-2"))
            .ReviewAsync(workupId, AcceptReview("Agree."));
        Assert.False(reviewed.Succeeded);
        Assert.Contains(reviewed.Evaluation!.HardStops, r =>
            r.Code == AntibodyPanelLotValidityRule.InactiveCode);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
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
        Assert.False(openRow.HasInactiveLot);
        Assert.False(openRow.HasExpiredLot);
        Assert.Equal(AntibodyIdWorklistNextAction.RecordReactions, openRow.NextAction);
    }

    [Fact]
    public async Task ListOpenWorkups_ShowsNextActionAndSummary()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var reactionsPatientId = await SeedPatientAsync("MRN-ABID-WL-RXN");
        var interpretPatientId = await SeedPatientAsync("MRN-ABID-WL-INT");
        var reviewPatientId = await SeedPatientAsync("MRN-ABID-WL-REV");
        long reactionsId;
        long interpretId;
        long reviewId;

        await using (var context = _factory.Create())
        {
            var reactions = await Svc(context).CreateWorkupAsync(
                reactionsPatientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(reactions.Succeeded, reactions.Error);
            reactionsId = reactions.Value!.Id;

            var interpret = await Svc(context).CreateWorkupAsync(
                interpretPatientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(interpret.Succeeded, interpret.Error);
            interpretId = interpret.Value!.Id;
            await RecordPanelAhgAsync(context, interpretId, interpret.Value);

            var review = await Svc(context).CreateWorkupAsync(
                reviewPatientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(review.Succeeded, review.Error);
            reviewId = review.Value!.Id;
            await RecordPanelAhgAsync(context, reviewId, review.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(
                reviewId,
                new RecordAntibodyIdInterpretationRequest(
                    "anti-K identified.",
                    [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using var check = _factory.Create();
        var listed = await Svc(check).ListOpenWorkupsAsync();
        Assert.Equal(
            AntibodyIdWorklistNextAction.RecordReactions,
            Assert.Single(listed, w => w.Id == reactionsId).NextAction);
        Assert.Equal(
            AntibodyIdWorklistNextAction.Interpret,
            Assert.Single(listed, w => w.Id == interpretId).NextAction);
        Assert.Equal(
            AntibodyIdWorklistNextAction.Review,
            Assert.Single(listed, w => w.Id == reviewId).NextAction);

        var summary = await Svc(check).SummarizeOpenWorkupsAsync();
        Assert.Equal(listed.Count, summary.OpenCount);
        Assert.Equal(listed.Count(w => w.NextAction == AntibodyIdWorklistNextAction.RecordReactions), summary.RecordReactionsCount);
        Assert.Equal(listed.Count(w => w.NextAction == AntibodyIdWorklistNextAction.Interpret), summary.InterpretCount);
        Assert.Equal(listed.Count(w => w.NextAction == AntibodyIdWorklistNextAction.Review), summary.ReviewCount);
        Assert.Equal(listed.Count(w => w.HasInactiveLot), summary.InactiveLotCount);
        Assert.Equal(listed.Count(w => w.HasExpiredLot), summary.ExpiredLotCount);
        Assert.Equal(listed.Count(w => w.HasUnusableSpecimen), summary.UnusableSpecimenCount);
        Assert.Equal(listed.Count(w => w.HasExpiredSpecimen), summary.ExpiredSpecimenCount);
        Assert.Equal(listed.Count(w => w.HasUnacceptedSpecimen), summary.UnacceptedSpecimenCount);
        Assert.Equal(listed.Count(w => w.HasNotReadySpecimen), summary.NotReadySpecimenCount);
        Assert.Equal(listed.Count(w => w.HasWithdrawnJudgment), summary.WithdrawnJudgmentCount);
        Assert.Equal(listed.Count(w => w.HasPendingTypeCorrection), summary.PendingTypeCorrectionCount);
        Assert.Equal(listed.Count(w => w.HasReservedOrIssuedUnits), summary.ReservedOrIssuedUnitCount);
        Assert.True(summary.RecordReactionsCount >= 1);
        Assert.True(summary.InterpretCount >= 1);
        Assert.True(summary.ReviewCount >= 1);
    }

    [Fact]
    public async Task ListOpenWorkups_FlagsExpiredAndInactiveLots()
    {
        var (kellId, expiredLotId) = await SeedPanelAsync();
        var (_, inactiveLotId) = await SeedPanelAsync();
        var (_, currentLotId) = await SeedPanelAsync();
        var selectedId = await SeedExtraLotAsync(kellId, selected: true);
        var expiredPatientId = await SeedPatientAsync("MRN-ABID-WL-EXP");
        var inactivePatientId = await SeedPatientAsync("MRN-ABID-WL-INACT");
        var selectedPatientId = await SeedPatientAsync("MRN-ABID-WL-SEL");
        long expiredWorkupId;
        long inactiveWorkupId;
        long selectedWorkupId;

        await using (var context = _factory.Create())
        {
            var expired = await Svc(context).CreateWorkupAsync(
                expiredPatientId, new CreateAntibodyIdWorkupRequest(null, expiredLotId));
            Assert.True(expired.Succeeded, expired.Error);
            expiredWorkupId = expired.Value!.Id;

            var inactive = await Svc(context).CreateWorkupAsync(
                inactivePatientId, new CreateAntibodyIdWorkupRequest(null, inactiveLotId));
            Assert.True(inactive.Succeeded, inactive.Error);
            inactiveWorkupId = inactive.Value!.Id;

            var selected = await Svc(context).CreateWorkupAsync(
                selectedPatientId, new CreateAntibodyIdWorkupRequest(null, currentLotId));
            Assert.True(selected.Succeeded, selected.Error);
            selectedWorkupId = selected.Value!.Id;
            var attached = await Svc(context).AttachLotsAsync(
                selectedWorkupId, new AttachAntibodyIdLotsRequest([selectedId]));
            Assert.True(attached.Succeeded, attached.Error);
        }

        await using (var context = _factory.Create())
        {
            var expiredLot = await context.AntibodyPanelLots.SingleAsync(l => l.Id == expiredLotId);
            expiredLot.ExpiresOn = DateOnly.FromDateTime(_factory.Clock.UtcNow.Date.AddDays(-1));
            var inactiveLot = await context.AntibodyPanelLots.SingleAsync(l => l.Id == inactiveLotId);
            inactiveLot.IsActive = false;
            var selectedLot = await context.AntibodyPanelLots.SingleAsync(l => l.Id == selectedId);
            selectedLot.ExpiresOn = DateOnly.FromDateTime(_factory.Clock.UtcNow.Date.AddDays(-1));
            await context.SaveChangesAsync();
        }

        await using var check = _factory.Create();
        var listed = await Svc(check).ListOpenWorkupsAsync();
        var expiredRow = Assert.Single(listed, w => w.Id == expiredWorkupId);
        Assert.True(expiredRow.HasExpiredLot);
        Assert.False(expiredRow.HasInactiveLot);
        Assert.Contains(expiredRow.LotNumber, expiredRow.ExpiredLotNumbers ?? []);

        var inactiveRow = Assert.Single(listed, w => w.Id == inactiveWorkupId);
        Assert.True(inactiveRow.HasInactiveLot);
        Assert.False(inactiveRow.HasExpiredLot);
        Assert.Contains(inactiveRow.LotNumber, inactiveRow.InactiveLotNumbers ?? []);

        var selectedRow = Assert.Single(listed, w => w.Id == selectedWorkupId);
        Assert.True(selectedRow.HasExpiredLot);
        Assert.False(selectedRow.HasInactiveLot);
        var selectedLotNumber = await check.AntibodyPanelLots
            .Where(l => l.Id == selectedId)
            .Select(l => l.LotNumber)
            .SingleAsync();
        Assert.Contains(selectedRow.LotNumber, selectedRow.AttachedLotNumbers ?? []);
        Assert.Contains(selectedLotNumber, selectedRow.AttachedLotNumbers ?? []);
        Assert.Contains(selectedLotNumber, selectedRow.ExpiredLotNumbers ?? []);
        Assert.DoesNotContain(selectedRow.LotNumber, selectedRow.ExpiredLotNumbers ?? []);
        var selectedCellLot = await check.AntibodyPanelLots.SingleAsync(l => l.Id == selectedId);
        var selectedManufacturer = await check.AntibodyPanelManufacturers
            .SingleAsync(m => m.Id == selectedCellLot.ManufacturerId);
        Assert.Contains(selectedManufacturer.Name, selectedRow.AttachedManufacturers ?? []);
        Assert.Contains(selectedManufacturer.Code, selectedRow.AttachedManufacturers ?? []);
        Assert.False(string.Equals(selectedRow.ManufacturerName, selectedManufacturer.Name, StringComparison.OrdinalIgnoreCase));

        var summary = await Svc(check).SummarizeOpenWorkupsAsync();
        Assert.Contains(expiredRow.LotNumber, summary.ExpiredLotNumbers ?? []);
        Assert.Contains(inactiveRow.LotNumber, summary.InactiveLotNumbers ?? []);
        Assert.Contains(selectedLotNumber, summary.ExpiredLotNumbers ?? []);
        Assert.DoesNotContain(selectedRow.LotNumber, summary.ExpiredLotNumbers ?? []);

        var patientRows = await Svc(check).ListWorkupsAsync(expiredPatientId);
        Assert.True(Assert.Single(patientRows).HasExpiredLot);
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree."));
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
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
    public async Task CollectedLinkedSpecimen_BlocksOtherSpecimenVerifyAndNewWorkup()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAbtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-SPEC-COL");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-COL");
        var otherSpecimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-COL-B");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(
                workupId,
                new RecordAntibodyIdInterpretationRequest(
                    "anti-K identified.",
                    [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Technologist identification")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var specimen = await context.Specimens.SingleAsync(s => s.Id == specimenId);
            specimen.Status = SpecimenStatus.Collected;
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            var listed = await Svc(context).ListOpenWorkupsAsync();
            var row = Assert.Single(listed, w => w.Id == workupId);
            Assert.True(row.HasNotReadySpecimen);
            Assert.False(row.HasUnusableSpecimen);
            Assert.False(row.HasUnacceptedSpecimen);
            Assert.False(row.HasExpiredSpecimen);
            var summary = await Svc(context).SummarizeOpenWorkupsAsync();
            Assert.Equal(listed.Count(w => w.HasNotReadySpecimen), summary.NotReadySpecimenCount);
            Assert.True(summary.NotReadySpecimenCount >= 1);

            var blocked = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(otherSpecimenId, lotId));
            Assert.False(blocked.Succeeded);
            Assert.Contains(blocked.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationWorkupScopeRule.OverlappingOpenCode);

            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                otherSpecimenId,
                "ABTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            var verified = await Results(context).VerifyResultAsync(entered.Value!.Id);
            Assert.False(verified.Succeeded);
            Assert.Contains(verified.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationHistoryPostRule.OpenWorkupCode);
        }

        await using var check = _factory.Create();
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.NotNull(workup.InterpretedUtc);
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
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
            var reviewed = await supervisor.ReviewAsync(workupId, AcceptReview("Agree with anti-K."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
        }

        await using (var context = _factory.Create())
        {
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
            Assert.Equal(AntibodyWorkupStatus.Completed, completed.Value!.Status);
        }

        await using var check = _factory.Create();
        var listed = Assert.Single(await Svc(check).ListWorkupsAsync(patientId));
        Assert.Equal(AntibodyWorkupStatus.Completed, listed.Status);
        Assert.Equal(AntibodyIdWorklistNextAction.None, listed.NextAction);
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
        Assert.Equal(attrId, history.BloodAttributeDefinitionId);
        Assert.True(await check.AuditEvents.AnyAsync(e =>
            e.EntityType == nameof(AntibodyHistory) && e.EventType == AuditEventType.Antibody && e.EntityId == history.Id));
    }

    [Fact]
    public async Task Complete_WritesAntibodyWithHistoryId()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var patientId = await SeedPatientAsync($"MRN-ABID-AUD-{suffix}");
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
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree with anti-K."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
        }

        await using var check = _factory.Create();
        var completed = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.True(completed.Succeeded, completed.Error);

        var history = Assert.Single(await check.AntibodyHistory
            .Where(a => a.PatientId == patientId && a.IsActive)
            .ToListAsync());
        var events = check.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Antibody
            && e.EntityType == nameof(AntibodyHistory)
            && e.EntityId == history.Id
            && e.Reason == "Identified on reviewed antibody-identification workup."
            && e.NewValueJson is not null
            && e.NewValueJson.Contains($"\"PatientId\":{patientId}")
            && e.NewValueJson.Contains($"\"WorkupId\":{workupId}")
            && e.NewValueJson.Contains($"\"HistoryId\":{history.Id}"));
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
                .ReviewAsync(workupId, AcceptReview("Agree."));
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
            var finding = Assert.Single(interpreted.Value!.Findings, f => f.Source == AntibodyIdSource.Technologist);
            Assert.Equal(attrId, finding.BloodAttributeDefinitionId);
            Assert.Equal("anti-K", finding.Specificity);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree."));
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
            [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
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
        var finding = Assert.Single(interpreted.Value!.Findings, f => f.Source == AntibodyIdSource.Technologist);
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
            [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
        Assert.True(interpreted.Succeeded, interpreted.Error);

        var voided = await Svc(context).VoidAsync(
            created.Value.Id, new VoidAntibodyIdWorkupRequest("Opened on the wrong specimen."));
        Assert.True(voided.Succeeded, voided.Error);
        Assert.Equal(AntibodyWorkupStatus.Voided, voided.Value!.Status);
        Assert.Equal("Opened on the wrong specimen.", voided.Value.VoidReason);
        Assert.Empty(await context.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task CreateWorkup_WritesAntibody()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-AUD");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);

        var events = await context.AuditEvents.ToListAsync();
        Assert.Contains(events, e =>
            e.EntityType == nameof(AntibodyIdentificationWorkup)
            && e.EventType == AuditEventType.Antibody
            && e.EntityId == created.Value!.Id
            && e.Reason == "Antibody-identification workup opened.");
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree."));
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
        var selectedLot = Assert.Single(attached.Value.Lots, l => l.Id == selectedId && l.IsSelectedCellLot);
        Assert.False(string.IsNullOrWhiteSpace(selectedLot.ManufacturerName));
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
                .ReviewAsync(workupId, AcceptReview("Agree none identified."));
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
            .ReviewAsync(workupId, AcceptReview("Agree none identified."));
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
                .ReviewAsync(workupId, AcceptReview("Agree."));
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
                .ReviewAsync(workupId, AcceptReview("Noted."));
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
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
                .ReviewAsync(workupId, AcceptReview("Should not accept stale panel."));
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(reinterpreted.Succeeded, reinterpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var rereviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree after panel update."));
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
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
    public async Task Interpret_IdentifiedWhenPanelExcludes_WithoutRationale_HardStops()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-EXCLRSN");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
        Assert.True(created.Succeeded, created.Error);
        await RecordPanelAhgAsync(context, created.Value!.Id, created.Value);

        var interpreted = await Svc(context).RecordInterpretationAsync(created.Value.Id, new RecordAntibodyIdInterpretationRequest(
            "anti-K identified.",
            [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, null)]));
        Assert.False(interpreted.Succeeded);
        Assert.Contains(interpreted.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.IdentifiedExcludedRationaleCode);
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
            [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree despite exclusion."));
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
                .ReviewAsync(workupId, AcceptReview("Agree anti-K."));
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
    public async Task Complete_WithReservedUnit_WarnsAndRequiresAcknowledgment()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-PROD");
        await SeedReservedUnitAsync(patientId, "ABID-PROD");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            Assert.True(created.Value.HasReservedOrIssuedUnits);
            var listed = await Svc(context).ListOpenWorkupsAsync();
            var row = Assert.Single(listed, w => w.Id == workupId);
            Assert.True(row.HasReservedOrIssuedUnits);
            var summary = await Svc(context).SummarizeOpenWorkupsAsync();
            Assert.Equal(listed.Count(w => w.HasReservedOrIssuedUnits), summary.ReservedOrIssuedUnitCount);
            Assert.True(summary.ReservedOrIssuedUnitCount >= 1);
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
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
                w.Code == AntibodyIdentificationInterpretationRule.ProductsOpenCode);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree. Reserved units noted."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
        }

        await using var check = _factory.Create();
        var withoutAck = await Svc(check).CompleteAsync(workupId);
        Assert.False(withoutAck.Succeeded);
        Assert.Contains(withoutAck.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.CompleteAckCode);
        Assert.Contains(withoutAck.Evaluation.Warnings, w =>
            w.Code == AntibodyIdentificationInterpretationRule.ProductsOpenCode);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());

        var completed = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.True(completed.Succeeded, completed.Error);
        Assert.Contains(completed.Evaluation!.Warnings, w =>
            w.Code == AntibodyIdentificationInterpretationRule.ProductsOpenCode);
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
    }

    [Fact]
    public async Task Void_WithReservedUnit_WarnsAndVoids()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-VOID-PROD");
        await SeedReservedUnitAsync(patientId, "ABID-VOID-PROD");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            Assert.True(created.Value.HasReservedOrIssuedUnits);
        }

        await using var check = _factory.Create();
        var voided = await Svc(check).VoidAsync(
            workupId, new VoidAntibodyIdWorkupRequest("Abandoned; reserved units already issued path."));
        Assert.True(voided.Succeeded, voided.Error);
        Assert.Contains(
            voided.Evaluation!.Warnings,
            w => w.Code == AntibodyIdentificationInterpretationRule.VoidProductsCode);
        Assert.Equal(AntibodyWorkupStatus.Voided, voided.Value!.Status);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
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
    public async Task AddAntibody_OpenWorkup_IsBlockedUntilVoided()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ADD-OPEN");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;

            var blocked = await Immuno(context).AddAntibodyAsync(
                patientId, null, "anti-K", AntibodyStatus.Identified, "Chart add while open");
            Assert.False(blocked.Succeeded);
            Assert.Contains("identification of record", blocked.Error);
            Assert.Empty(await Immuno(context).GetAntibodyHistoryAsync(patientId));
        }

        await using (var voidCtx = _factory.Create())
        {
            var voided = await Svc(voidCtx).VoidAsync(
                workupId, new VoidAntibodyIdWorkupRequest("Abandoned so history can be added from the chart."));
            Assert.True(voided.Succeeded, voided.Error);
        }

        await using var after = _factory.Create();
        var added = await Immuno(after).AddAntibodyAsync(
            patientId, null, "anti-K", AntibodyStatus.Identified, "Chart add after void");
        Assert.True(added.Succeeded, added.Error);
        Assert.Equal("anti-K", added.Value!.AntibodySpecificity);
    }

    [Fact]
    public async Task AddAntibody_SpecimenScopedOpenWorkup_IsBlocked()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ADD-SCOPED");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-ADD-SCOPED");

        await using var context = _factory.Create();
        var created = await Svc(context).CreateWorkupAsync(
            patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
        Assert.True(created.Succeeded, created.Error);

        var blocked = await Immuno(context).AddAntibodyAsync(
            patientId, null, "anti-E", AntibodyStatus.Identified, "Chart add while scoped workup open");
        Assert.False(blocked.Succeeded);
        Assert.Contains("identification of record", blocked.Error);
        Assert.Empty(await Immuno(context).GetAntibodyHistoryAsync(patientId));
    }

    [Fact]
    public async Task DeactivateAntibody_OpenWorkup_IsBlockedUntilVoided()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-DEACT-OPEN");
        long antibodyId;
        long workupId;

        await using (var seed = _factory.Create())
        {
            var added = await Immuno(seed).AddAntibodyAsync(
                patientId, null, "anti-K", AntibodyStatus.Identified, "Historical anti-K before workup");
            Assert.True(added.Succeeded, added.Error);
            antibodyId = added.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;

            var blocked = await Immuno(context).DeactivateAntibodyAsync(
                antibodyId, "Trying to clear history while identification is open");
            Assert.False(blocked.Succeeded);
            Assert.Contains("identification of record", blocked.Error);
            var stillActive = Assert.Single(await Immuno(context).GetActiveAntibodiesAsync(patientId));
            Assert.Equal("anti-K", stillActive.AntibodySpecificity);
        }

        await using (var voidCtx = _factory.Create())
        {
            var voided = await Svc(voidCtx).VoidAsync(
                workupId, new VoidAntibodyIdWorkupRequest("Abandoned so history can be deactivated."));
            Assert.True(voided.Succeeded, voided.Error);
        }

        await using var after = _factory.Create();
        var deactivated = await Immuno(after).DeactivateAntibodyAsync(
            antibodyId, "Currently undetectable after void");
        Assert.True(deactivated.Succeeded, deactivated.Error);
        Assert.False(deactivated.Value!.IsActive);
        Assert.DoesNotContain(
            deactivated.Warnings,
            w => w.Code == AntibodyIdentificationHistoryPostRule.DeactivatePostedCode);
    }

    [Fact]
    public async Task DeactivateAntibody_PostedByCompletedWorkup_WarnsAndDeactivates()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-DEACT-POST");
        long workupId;
        long antibodyId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree anti-K."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
        }

        await using var after = _factory.Create();
        var history = Assert.Single(await after.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        antibodyId = history.Id;
        var dto = Assert.Single(await Immuno(after).ListAntibodyDtosAsync(patientId, activeOnly: true));
        Assert.True(dto.PostedByCompletedWorkup);

        var deactivated = await Immuno(after).DeactivateAntibodyAsync(
            antibodyId, "Currently undetectable after identification of record");
        Assert.True(deactivated.Succeeded, deactivated.Error);
        Assert.False(deactivated.Value!.IsActive);
        Assert.Contains(
            deactivated.Warnings,
            w => w.Code == AntibodyIdentificationHistoryPostRule.DeactivatePostedCode);
    }

    [Fact]
    public async Task SaveAntigen_OpenWorkup_WarnsAndWithdrawsInterpretation()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-AG-OPEN");
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
            Assert.NotNull(interpreted.Value!.InterpretedUtc);
        }

        await using (var context = _factory.Create())
        {
            var saved = await Immuno(context).SaveAntigenProfileAsync(
                patientId, new SaveAntigenProfileRequest(attrId, AntigenResult.Positive, "Gel"));
            Assert.True(saved.Succeeded, saved.Error);
            Assert.Contains(
                saved.Warnings,
                w => w.Code == AntibodyIdentificationHistoryPostRule.AntigenOpenCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.Null(workup.InterpretedUtc);
        Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, workup.Status);
        var detail = await Svc(check).GetWorkupAsync(workupId);
        Assert.Equal(
            AntibodyIdentificationInterpretationRule.JudgmentWithdrawnAdvisory,
            detail!.JudgmentWithdrawnReason);
        var blocked = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.InterpretationRequiredCode);
    }

    [Fact]
    public async Task RecordBloodType_OpenWorkup_WarnsAndWithdrawsInterpretation()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ABO-OPEN");
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
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
            Assert.NotNull(interpreted.Value!.InterpretedUtc);
        }

        await using (var context = _factory.Create())
        {
            var recorded = await Immuno(context).RecordBloodTypeManualAsync(
                patientId, AboGroup.A, RhType.Negative, "Historical record import");
            Assert.True(recorded.Succeeded, recorded.Error);
            Assert.Contains(
                recorded.Warnings,
                w => w.Code == AntibodyIdentificationHistoryPostRule.BloodTypeOpenCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.Null(workup.InterpretedUtc);
        Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, workup.Status);
        var blocked = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.InterpretationRequiredCode);
    }

    [Fact]
    public async Task RecordBloodType_SameType_DoesNotWithdrawInterpretation()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ABO-SAME");
        long workupId;

        await using (var context = _factory.Create())
        {
            var first = await Immuno(context).RecordBloodTypeManualAsync(
                patientId, AboGroup.O, RhType.Positive, "Historical record import");
            Assert.True(first.Succeeded, first.Error);

            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var recorded = await Immuno(context).RecordBloodTypeManualAsync(
                patientId, AboGroup.O, RhType.Positive, "Re-entered same type");
            Assert.True(recorded.Succeeded, recorded.Error);
            Assert.DoesNotContain(
                recorded.Warnings,
                w => w.Code == AntibodyIdentificationHistoryPostRule.BloodTypeOpenCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.NotNull(workup.InterpretedUtc);
    }

    [Fact]
    public async Task AddSpecialRequirement_OpenWorkup_WarnsAndAdds()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-SR-ADD");

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
        }

        await using var check = _factory.Create();
        var added = await SpecialRequirements(check).AddAsync(
            patientId,
            new AddSpecialRequirementRequest(
                SpecialTransfusionRequirementType.AntigenNegative,
                "History of anti-K",
                AntigenCode: "K"));
        Assert.True(added.Succeeded, added.Error);
        Assert.True(added.Value!.IsActive);
        Assert.Contains(
            added.Warnings,
            w => w.Code == AntibodyIdentificationHistoryPostRule.SpecialRequirementOpenCode);
    }

    [Fact]
    public async Task DeactivateSpecialRequirement_OpenWorkup_WarnsAndDeactivates()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-SR-DEACT");
        long requirementId;

        await using (var context = _factory.Create())
        {
            var added = await SpecialRequirements(context).AddAsync(
                patientId,
                new AddSpecialRequirementRequest(SpecialTransfusionRequirementType.Irradiated, "Needed"));
            Assert.True(added.Succeeded, added.Error);
            requirementId = added.Value!.Id;

            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
        }

        await using var check = _factory.Create();
        var deactivated = await SpecialRequirements(check).DeactivateAsync(requirementId, "No longer indicated");
        Assert.True(deactivated.Succeeded, deactivated.Error);
        Assert.False(deactivated.Value!.IsActive);
        Assert.Contains(
            deactivated.Warnings,
            w => w.Code == AntibodyIdentificationHistoryPostRule.SpecialRequirementOpenCode);
    }

    [Fact]
    public async Task VerifyAboRh_OpenWorkup_WarnsAndWithdrawsInterpretation()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ABO-VERIFY");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-ABO-VERIFY");
        long workupId;
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
            Assert.NotNull(interpreted.Value!.InterpretedUtc);

            var entered = await Results(context).EnterAboRhAsync(
                new EnterAboRhRequest(specimenId, AboGroup.A, RhType.Negative));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context, new TestCurrentUser("tech-verify", "WORKSTATION-2"))
                .VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
            Assert.Contains(
                verified.Evaluation!.Warnings,
                w => w.Code == AntibodyIdentificationHistoryPostRule.BloodTypeOpenCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.Null(workup.InterpretedUtc);
        Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, workup.Status);
        var blocked = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.InterpretationRequiredCode);
    }

    [Fact]
    public async Task VerifyAntigen_OpenWorkup_WarnsAndWithdrawsInterpretation()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAgtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-AG-VERIFY");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-AG-VERIFY");
        long workupId;
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
            Assert.NotNull(interpreted.Value!.InterpretedUtc);

            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                specimenId,
                "AGTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
            Assert.Contains(
                verified.Evaluation!.Warnings,
                w => w.Code == AntibodyIdentificationHistoryPostRule.AntigenOpenCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.Null(workup.InterpretedUtc);
        Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, workup.Status);
        var blocked = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.InterpretationRequiredCode);
    }

    [Fact]
    public async Task InvalidateAntigen_AfterReinterpret_WarnsAndWithdrawsInterpretation()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAgtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-AG-INVAL");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-AG-INVAL");
        long workupId;
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);

            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                specimenId,
                "AGTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
        }

        await using (var context = _factory.Create())
        {
            var reinterpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified after K+.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Repeated after antigen verify.")]));
            Assert.True(reinterpreted.Succeeded, reinterpreted.Error);
            Assert.NotNull(reinterpreted.Value!.InterpretedUtc);
        }

        await using (var context = _factory.Create())
        {
            var invalidated = await Results(context).InvalidateResultAsync(resultId, "Wrong reagent; repeat antigen typing.");
            Assert.True(invalidated.Succeeded, invalidated.Error);
            Assert.Contains(invalidated.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.AntigenInvalidateCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.Null(workup.InterpretedUtc);
        Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, workup.Status);
        var phenotype = Assert.Single(await check.AntigenProfiles.Where(p => p.PatientId == patientId).ToListAsync());
        Assert.Equal(AntigenResult.Positive, phenotype.Result);
        var blocked = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationInterpretationRule.InterpretationRequiredCode);
    }

    [Fact]
    public async Task InvalidateAboRh_AfterReinterpret_WarnsAndWithdrawsInterpretation()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ABO-INVAL");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-ABO-INVAL");
        long workupId;
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);

            var entered = await Results(context).EnterAboRhAsync(
                new EnterAboRhRequest(specimenId, AboGroup.A, RhType.Negative));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context, new TestCurrentUser("tech-verify", "WORKSTATION-2"))
                .VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
        }

        await using (var context = _factory.Create())
        {
            var reinterpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified after ABO.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Repeated after ABO verify.")]));
            Assert.True(reinterpreted.Succeeded, reinterpreted.Error);
            Assert.NotNull(reinterpreted.Value!.InterpretedUtc);
        }

        await using (var context = _factory.Create())
        {
            var invalidated = await Results(context).InvalidateResultAsync(resultId, "Wrong patient specimen.");
            Assert.True(invalidated.Succeeded, invalidated.Error);
            Assert.Contains(invalidated.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.BloodTypeInvalidateCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.Null(workup.InterpretedUtc);
        Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, workup.Status);
        Assert.Contains(await check.PatientBloodTypeHistory.Where(h => h.PatientId == patientId).ToListAsync(),
            h => h.Abo == AboGroup.A && h.RhD == RhType.Negative);
    }

    [Fact]
    public async Task CorrectAntigen_OpenWorkup_WarnsAndDoesNotWithdraw()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAgtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-TYPE-CORR");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-TYPE-CORR");
        long workupId;
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);

            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                specimenId,
                "AGTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
        }

        await using (var context = _factory.Create())
        {
            var reinterpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified after K+.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Repeated after antigen verify.")]));
            Assert.True(reinterpreted.Succeeded, reinterpreted.Error);
            Assert.NotNull(reinterpreted.Value!.InterpretedUtc);
        }

        await using (var context = _factory.Create())
        {
            var corrected = await Results(context).CorrectResultAsync(
                resultId,
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Negative)]),
                "Transcription error; K is negative.");
            Assert.True(corrected.Succeeded, corrected.Error);
            Assert.Contains(corrected.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.TypeCorrectionOpenCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.NotNull(workup.InterpretedUtc);
        Assert.Equal(AntibodyWorkupStatus.PendingSupervisorReview, workup.Status);

        var listed = await Svc(check).ListOpenWorkupsAsync();
        var row = Assert.Single(listed, w => w.Id == workupId);
        Assert.True(row.HasPendingTypeCorrection);
        var summary = await Svc(check).SummarizeOpenWorkupsAsync();
        Assert.Equal(listed.Count(w => w.HasPendingTypeCorrection), summary.PendingTypeCorrectionCount);
        Assert.True(summary.PendingTypeCorrectionCount >= 1);

        var blockedComplete = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.False(blockedComplete.Succeeded);
        Assert.Contains(blockedComplete.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationHistoryPostRule.PendingTypeCorrectionCode);

        var blockedReview = await Svc(check, new TestCurrentUser("supervisor-abid", "WS-2"))
            .ReviewAsync(workupId, AcceptReview("Agree after correction."));
        Assert.False(blockedReview.Succeeded);
        Assert.Contains(blockedReview.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationHistoryPostRule.PendingTypeCorrectionCode);
    }

    [Fact]
    public async Task PendingTypeCorrection_OnOtherSpecimen_DoesNotBlockScopedWorkup()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAgtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-TYPE-OTHER");
        var linkedId = await SeedSpecimenAsync(patientId, "ACC-ABID-TYPE-LINKED");
        var otherId = await SeedSpecimenAsync(patientId, "ACC-ABID-TYPE-OTHER");
        long workupId;
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(linkedId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);

            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                otherId,
                "AGTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
        }

        await using (var context = _factory.Create())
        {
            var interpreted = await Svc(context).RecordInterpretationAsync(workupId, new RecordAntibodyIdInterpretationRequest(
                "anti-K identified.",
                [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var corrected = await Results(context).CorrectResultAsync(
                resultId,
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Negative)]),
                "Other-draw transcription error.");
            Assert.True(corrected.Succeeded, corrected.Error);
            Assert.Contains(corrected.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.TypeCorrectionOpenCode);

            var listed = await Svc(context).ListOpenWorkupsAsync();
            var row = Assert.Single(listed, w => w.Id == workupId);
            Assert.False(row.HasPendingTypeCorrection);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree. Correction is on another specimen."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
        }

        await using var check = _factory.Create();
        var completed = await Svc(check).CompleteAsync(workupId, ReviewedWarnings());
        Assert.True(completed.Succeeded, completed.Error);
        Assert.DoesNotContain(completed.Evaluation!.HardStops, r =>
            r.Code == AntibodyIdentificationHistoryPostRule.PendingTypeCorrectionCode);
    }

    [Fact]
    public async Task VerifyBloodAttributeAntibody_OpenWorkup_HardStopsAndDoesNotPost()
    {
        var (_, lotId) = await SeedPanelAsync();
        await SeedAbtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ABTYPE-OPEN");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-ABTYPE-OPEN");
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);

            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                specimenId,
                "ABTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;

            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.False(verified.Succeeded);
            Assert.Contains(verified.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationHistoryPostRule.OpenWorkupCode);
        }

        await using var check = _factory.Create();
        var result = await check.TestResults.SingleAsync(r => r.Id == resultId);
        Assert.Equal(ResultStatus.Entered, result.Status);
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
    }

    [Fact]
    public async Task VerifyBloodAttributeAntibody_OpenWorkup_HardStopsAndDoesNotDeactivate()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAbtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ABTYPE-DEACT");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-ABTYPE-DEACT");
        long resultId;

        await using (var context = _factory.Create())
        {
            var added = await Immuno(context).AddAntibodyAsync(
                patientId, attrId, null, AntibodyStatus.Identified, "History of anti-K");
            Assert.True(added.Succeeded, added.Error);

            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(null, lotId));
            Assert.True(created.Succeeded, created.Error);

            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                specimenId,
                "ABTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Negative)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;

            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.False(verified.Succeeded);
            Assert.Contains(verified.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationHistoryPostRule.OpenWorkupCode);
        }

        await using var check = _factory.Create();
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        Assert.True(history.IsActive);
        var result = await check.TestResults.SingleAsync(r => r.Id == resultId);
        Assert.Equal(ResultStatus.Entered, result.Status);
    }

    [Fact]
    public async Task VerifyBloodAttributeAntibody_AfterCompletedWorkup_SkipsPostAndWarnsAuthoritative()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAbtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ABTYPE-DONE");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-ABTYPE-DONE");
        await CompleteReviewedIdentificationAsync(patientId, specimenId, lotId, attrId);
        long resultId;

        await using (var context = _factory.Create())
        {
            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                specimenId,
                "ABTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;

            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
            Assert.Contains(verified.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.AuthoritativeCode);
            Assert.DoesNotContain(verified.Evaluation.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.DisagreeCode);
        }

        await using var check = _factory.Create();
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
        Assert.Equal(attrId, history.BloodAttributeDefinitionId);
        Assert.NotEqual(resultId, history.SourceResultId);
        var result = await check.TestResults.SingleAsync(r => r.Id == resultId);
        Assert.Equal(ResultStatus.Verified, result.Status);
    }

    [Fact]
    public async Task VerifyBloodAttributeAntibody_AfterCompletedWorkup_DoesNotDeactivate()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAbtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ABTYPE-SKIPDEACT");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-ABTYPE-SKIPDEACT");
        await CompleteReviewedIdentificationAsync(patientId, specimenId, lotId, attrId);
        long resultId;

        await using (var context = _factory.Create())
        {
            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                specimenId,
                "ABTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Negative)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;

            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
            Assert.Contains(verified.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.AuthoritativeCode);
            Assert.Contains(verified.Evaluation.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.DisagreeCode);
        }

        await using var check = _factory.Create();
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        Assert.True(history.IsActive);
        Assert.Equal("anti-K", history.AntibodySpecificity);
        var result = await check.TestResults.SingleAsync(r => r.Id == resultId);
        Assert.Equal(ResultStatus.Verified, result.Status);
    }

    [Fact]
    public async Task VerifyAntigen_AfterCompletedWorkup_SameSpecimen_WarnsAndDoesNotReopen()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAgtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-AG-DONE");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-AG-DONE");
        await CompleteReviewedIdentificationAsync(patientId, specimenId, lotId, attrId);
        long resultId;

        await using (var context = _factory.Create())
        {
            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                specimenId,
                "AGTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
            Assert.Contains(verified.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.AntigenCompletedCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.PatientId == patientId);
        Assert.Equal(AntibodyWorkupStatus.Completed, workup.Status);
        Assert.NotNull(workup.InterpretedUtc);
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
        var phenotype = Assert.Single(await check.AntigenProfiles.Where(p => p.PatientId == patientId).ToListAsync());
        Assert.Equal(AntigenResult.Positive, phenotype.Result);
    }

    [Fact]
    public async Task VerifyAntigen_AfterCompletedWorkup_OtherSpecimen_WarnsWhenPostedConflicts()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAgtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-AG-DONE-OTHER");
        var linkedId = await SeedSpecimenAsync(patientId, "ACC-ABID-AG-DONE-LINKED");
        var otherId = await SeedSpecimenAsync(patientId, "ACC-ABID-AG-DONE-OTHER");
        await CompleteReviewedIdentificationAsync(patientId, linkedId, lotId, attrId);
        long resultId;

        await using (var context = _factory.Create())
        {
            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                otherId,
                "AGTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
            Assert.Contains(verified.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.AntigenCompletedCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.PatientId == patientId);
        Assert.Equal(AntibodyWorkupStatus.Completed, workup.Status);
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
    }

    [Fact]
    public async Task VerifyAboRh_AfterCompletedWorkup_SameSpecimen_WarnsAndDoesNotReopen()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ABO-DONE");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-ABO-DONE");
        await CompleteReviewedIdentificationAsync(patientId, specimenId, lotId, attrId);
        long resultId;

        await using (var context = _factory.Create())
        {
            var entered = await Results(context).EnterAboRhAsync(
                new EnterAboRhRequest(specimenId, AboGroup.A, RhType.Negative));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var verified = await Results(context, new TestCurrentUser("tech-verify", "WORKSTATION-2"))
                .VerifyResultAsync(resultId);
            Assert.True(verified.Succeeded, verified.Error);
            Assert.Contains(verified.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.BloodTypeCompletedCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.PatientId == patientId);
        Assert.Equal(AntibodyWorkupStatus.Completed, workup.Status);
        Assert.Contains(await check.PatientBloodTypeHistory.Where(h => h.PatientId == patientId).ToListAsync(),
            h => h.Abo == AboGroup.A && h.RhD == RhType.Negative && h.IsCurrent);
    }

    [Fact]
    public async Task SaveAntigen_AfterCompletedUnscopedWorkup_WarnsAndDoesNotReopen()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-AG-DONE-CHART");
        await CompleteReviewedIdentificationAsync(patientId, null, lotId, attrId);

        await using (var context = _factory.Create())
        {
            var saved = await Immuno(context).SaveAntigenProfileAsync(
                patientId, new SaveAntigenProfileRequest(attrId, AntigenResult.Positive, "Gel"));
            Assert.True(saved.Succeeded, saved.Error);
            Assert.Contains(saved.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.AntigenCompletedCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.PatientId == patientId);
        Assert.Equal(AntibodyWorkupStatus.Completed, workup.Status);
        Assert.NotNull(workup.InterpretedUtc);
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
    }

    [Fact]
    public async Task RecordBloodType_AfterCompletedUnscopedWorkup_WarnsAndDoesNotReopen()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-ABO-DONE-CHART");
        await CompleteReviewedIdentificationAsync(patientId, null, lotId, attrId);

        await using (var context = _factory.Create())
        {
            var recorded = await Immuno(context).RecordBloodTypeManualAsync(
                patientId, AboGroup.A, RhType.Negative, "Historical record import");
            Assert.True(recorded.Succeeded, recorded.Error);
            Assert.Contains(recorded.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.BloodTypeCompletedCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.PatientId == patientId);
        Assert.Equal(AntibodyWorkupStatus.Completed, workup.Status);
        Assert.NotNull(workup.InterpretedUtc);
    }

    [Fact]
    public async Task SaveAntigen_AfterCompletedScopedWorkup_WarnsWhenPostedConflicts()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-AG-DONE-SCOPED");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-AG-DONE-SCOPED");
        await CompleteReviewedIdentificationAsync(patientId, specimenId, lotId, attrId);

        await using (var context = _factory.Create())
        {
            var saved = await Immuno(context).SaveAntigenProfileAsync(
                patientId, new SaveAntigenProfileRequest(attrId, AntigenResult.Positive, "Gel"));
            Assert.True(saved.Succeeded, saved.Error);
            Assert.Contains(saved.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.AntigenCompletedCode);
            Assert.DoesNotContain(saved.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.AntigenOpenCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.PatientId == patientId);
        Assert.Equal(AntibodyWorkupStatus.Completed, workup.Status);
        var history = Assert.Single(await check.AntibodyHistory.Where(a => a.PatientId == patientId && a.IsActive).ToListAsync());
        Assert.Equal("anti-K", history.AntibodySpecificity);
    }

    [Fact]
    public async Task SaveAntigen_AfterCompletedScopedWorkup_UnrelatedAntigen_DoesNotWarn()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var fyaId = await SeedAntigenAttributeAsync("FYA", "Fy(a)", "anti-Fya");
        var patientId = await SeedPatientAsync("MRN-ABID-AG-DONE-UNREL");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-AG-DONE-UNREL");
        await CompleteReviewedIdentificationAsync(patientId, specimenId, lotId, attrId);

        await using (var context = _factory.Create())
        {
            var saved = await Immuno(context).SaveAntigenProfileAsync(
                patientId, new SaveAntigenProfileRequest(fyaId, AntigenResult.Positive, "Gel"));
            Assert.True(saved.Succeeded, saved.Error);
            Assert.DoesNotContain(saved.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.AntigenCompletedCode);
        }
    }

    [Fact]
    public async Task RejectSpecimen_OpenWorkup_WarnsWithdrawsAndBlocksOtherSpecimenVerify()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAbtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-SPEC-REJ");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-REJ");
        var otherSpecimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-REJ-B");
        long workupId;
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(
                workupId,
                new RecordAntibodyIdInterpretationRequest(
                    "anti-K identified.",
                    [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Technologist identification")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var rejected = await Specimens(context).RejectAsync(specimenId, "Hemolyzed; redraw.");
            Assert.True(rejected.Succeeded, rejected.Error);
            Assert.Contains(rejected.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.SpecimenOpenCode);
        }

        await using (var context = _factory.Create())
        {
            var workup = await context.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
            Assert.Null(workup.InterpretedUtc);
            Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, workup.Status);

            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                otherSpecimenId,
                "ABTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;

            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.False(verified.Succeeded);
            Assert.Contains(verified.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationHistoryPostRule.OpenWorkupCode);
        }

        await using var check = _factory.Create();
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        var result = await check.TestResults.SingleAsync(r => r.Id == resultId);
        Assert.Equal(ResultStatus.Entered, result.Status);
    }

    [Fact]
    public async Task CreateWorkup_AfterRejectedLinkedSpecimen_IsHardStop_RelinkSucceeds()
    {
        var (_, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-SPEC-REJ-DUP");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-REJ-DUP");
        var otherSpecimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-REJ-DUP-B");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
        }

        await using (var context = _factory.Create())
        {
            var rejected = await Specimens(context).RejectAsync(specimenId, "Clotted; redraw.");
            Assert.True(rejected.Succeeded, rejected.Error);
        }

        await using (var context = _factory.Create())
        {
            var listed = await Svc(context).ListOpenWorkupsAsync();
            var row = Assert.Single(listed, w => w.Id == workupId);
            Assert.True(row.HasUnusableSpecimen);
            var summary = await Svc(context).SummarizeOpenWorkupsAsync();
            Assert.Equal(1, summary.UnusableSpecimenCount);

            var blocked = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(otherSpecimenId, lotId));
            Assert.False(blocked.Succeeded);
            Assert.Contains(blocked.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationWorkupScopeRule.OverlappingOpenCode);

            var relinked = await Svc(context).LinkSpecimenAsync(
                workupId, new LinkAntibodyIdSpecimenRequest(otherSpecimenId));
            Assert.True(relinked.Succeeded, relinked.Error);
        }

        await using var check = _factory.Create();
        var after = Assert.Single(await Svc(check).ListOpenWorkupsAsync(), w => w.Id == workupId);
        Assert.Equal(otherSpecimenId, after.SpecimenId);
        Assert.False(after.HasUnusableSpecimen);
        Assert.Equal(0, (await Svc(check).SummarizeOpenWorkupsAsync()).UnusableSpecimenCount);
    }

    [Fact]
    public async Task ClockExpiredLinkedSpecimen_BlocksOtherSpecimenVerifyAndNewWorkup()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAbtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-SPEC-CLK");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-CLK");
        var otherSpecimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-CLK-B");
        long workupId;
        long resultId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(
                workupId,
                new RecordAntibodyIdInterpretationRequest(
                    "anti-K identified.",
                    [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Technologist identification")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var specimen = await context.Specimens.SingleAsync(s => s.Id == specimenId);
            specimen.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            var listed = await Svc(context).ListOpenWorkupsAsync();
            var row = Assert.Single(listed, w => w.Id == workupId);
            Assert.True(row.HasExpiredSpecimen);
            Assert.False(row.HasUnusableSpecimen);
            Assert.False(row.HasUnacceptedSpecimen);
            Assert.False(row.HasNotReadySpecimen);
            Assert.Equal(1, (await Svc(context).SummarizeOpenWorkupsAsync()).ExpiredSpecimenCount);

            var blocked = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(otherSpecimenId, lotId));
            Assert.False(blocked.Succeeded);
            Assert.Contains(blocked.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationWorkupScopeRule.OverlappingOpenCode);

            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                otherSpecimenId,
                "ABTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            resultId = entered.Value!.Id;
            var verified = await Results(context).VerifyResultAsync(resultId);
            Assert.False(verified.Succeeded);
            Assert.Contains(verified.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationHistoryPostRule.OpenWorkupCode);
        }

        await using var check = _factory.Create();
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.NotNull(workup.InterpretedUtc);
    }

    [Fact]
    public async Task ReceivedLinkedSpecimen_BlocksOtherSpecimenVerifyAndNewWorkup()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        await SeedAbtypeAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-SPEC-RCV");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-RCV", SpecimenStatus.Received);
        var otherSpecimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-RCV-B");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            Assert.Contains(created.Evaluation!.Warnings, w =>
                w.Code == AntibodyIdentificationWorkupScopeRule.SpecimenUnacceptedCode);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(
                workupId,
                new RecordAntibodyIdInterpretationRequest(
                    "anti-K identified.",
                    [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Technologist identification")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var listed = await Svc(context).ListOpenWorkupsAsync();
            var row = Assert.Single(listed, w => w.Id == workupId);
            Assert.True(row.HasUnacceptedSpecimen);
            Assert.False(row.HasUnusableSpecimen);
            Assert.False(row.HasExpiredSpecimen);
            Assert.False(row.HasNotReadySpecimen);
            Assert.Equal(1, (await Svc(context).SummarizeOpenWorkupsAsync()).UnacceptedSpecimenCount);

            var blocked = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(otherSpecimenId, lotId));
            Assert.False(blocked.Succeeded);
            Assert.Contains(blocked.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationWorkupScopeRule.OverlappingOpenCode);

            var entered = await Results(context).EnterResultAsync(new EnterResultRequest(
                otherSpecimenId,
                "ABTYPE",
                BloodAttributeResultValue.Serialize([new BloodAttributeResultRow("K", AntigenResult.Positive)])));
            Assert.True(entered.Succeeded, entered.Error);
            var verified = await Results(context).VerifyResultAsync(entered.Value!.Id);
            Assert.False(verified.Succeeded);
            Assert.Contains(verified.Evaluation!.HardStops, r =>
                r.Code == AntibodyIdentificationHistoryPostRule.OpenWorkupCode);
        }

        await using var check = _factory.Create();
        Assert.Empty(await check.AntibodyHistory.Where(a => a.PatientId == patientId).ToListAsync());
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.NotNull(workup.InterpretedUtc);
    }

    [Fact]
    public async Task UpdateSpecimen_OpenWorkup_WarnsAndWithdrawsInterpretation()
    {
        var (attrId, lotId) = await SeedPanelAsync();
        var patientId = await SeedPatientAsync("MRN-ABID-SPEC-EDIT");
        var specimenId = await SeedSpecimenAsync(patientId, "ACC-ABID-SPEC-EDIT");
        long workupId;

        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(
                workupId,
                new RecordAntibodyIdInterpretationRequest(
                    "anti-K identified.",
                    [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Technologist identification")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var specimen = await context.Specimens.SingleAsync(s => s.Id == specimenId);
            var updated = await Specimens(context).UpdateAsync(
                specimenId,
                new UpdateSpecimenRequest(specimen.CollectedUtc.AddHours(-2), ValidityHours: 72));
            Assert.True(updated.Succeeded, updated.Error);
            Assert.Contains(updated.Warnings, w =>
                w.Code == AntibodyIdentificationHistoryPostRule.SpecimenEditCode);
        }

        await using var check = _factory.Create();
        var workup = await check.AntibodyIdentificationWorkups.SingleAsync(w => w.Id == workupId);
        Assert.Null(workup.InterpretedUtc);
        Assert.Equal(AntibodyWorkupStatus.PendingInterpretation, workup.Status);

        var listed = await Svc(check).ListOpenWorkupsAsync();
        var row = Assert.Single(listed, w => w.Id == workupId);
        Assert.True(row.HasWithdrawnJudgment);
        Assert.Equal(AntibodyIdWorklistNextAction.Interpret, row.NextAction);
        var summary = await Svc(check).SummarizeOpenWorkupsAsync();
        Assert.Equal(listed.Count(w => w.HasWithdrawnJudgment), summary.WithdrawnJudgmentCount);
        Assert.True(summary.WithdrawnJudgmentCount >= 1);
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
            [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Pattern reviewed against assistance.")]));
        Assert.True(interpreted.Succeeded, interpreted.Error);

        var reviewed = await Svc(context).ReviewAsync(created.Value.Id, AcceptReview("self"));
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

    private ResultService Results(BloodBankDbContext c, ICurrentUser? user = null)
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
            testDefinitions: new EfRepository<TestDefinition>(c),
            antibodies: new EfRepository<AntibodyHistory>(c),
            antigenProfiles: new EfRepository<AntigenProfile>(c),
            bloodAttributes: new EfRepository<BloodAttributeDefinition>(c),
            antibodyWorkups: new EfRepository<AntibodyIdentificationWorkup>(c),
            antibodyFindings: new EfRepository<AntibodyIdentificationFinding>(c));
    }

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(
            new EfRepository<Specimen>(c),
            new EfRepository<Patient>(c),
            new EfRepository<SpecimenTypeDefinition>(c),
            c,
            _factory.Clock,
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            currentUser: _factory.CurrentUser,
            antibodyWorkups: new EfRepository<AntibodyIdentificationWorkup>(c),
            antibodyFindings: new EfRepository<AntibodyIdentificationFinding>(c));

    private SpecialRequirementService SpecialRequirements(BloodBankDbContext c) =>
        new(
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<Patient>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            workups: new EfRepository<AntibodyIdentificationWorkup>(c));

    private ImmunohematologyService Immuno(BloodBankDbContext c) =>
        new(
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<Patient>(c),
            new EfRepository<AntibodyIdentificationWorkup>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            findings: new EfRepository<AntibodyIdentificationFinding>(c));

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
            testDefinitions: new EfRepository<TestDefinition>(c),
            allocations: new EfRepository<Allocation>(c),
            issues: new EfRepository<Issue>(c));
    }

    private async Task CompleteReviewedIdentificationAsync(
        long patientId, long? specimenId, long lotId, long attrId)
    {
        long workupId;
        await using (var context = _factory.Create())
        {
            var created = await Svc(context).CreateWorkupAsync(
                patientId, new CreateAntibodyIdWorkupRequest(specimenId, lotId));
            Assert.True(created.Succeeded, created.Error);
            workupId = created.Value!.Id;
            await RecordPanelAhgAsync(context, workupId, created.Value);
            var interpreted = await Svc(context).RecordInterpretationAsync(
                workupId,
                new RecordAntibodyIdInterpretationRequest(
                    "anti-K identified.",
                    [new AntibodyIdInterpretationItem(attrId, "anti-K", AntibodyIdClassification.Identified, "Technologist identification")]));
            Assert.True(interpreted.Succeeded, interpreted.Error);
        }

        await using (var context = _factory.Create())
        {
            var reviewed = await Svc(context, new TestCurrentUser("supervisor-abid", "WS-2"))
                .ReviewAsync(workupId, AcceptReview("Agree with anti-K."));
            Assert.True(reviewed.Succeeded, reviewed.Error);
        }

        await using (var context = _factory.Create())
        {
            var completed = await Svc(context).CompleteAsync(workupId, ReviewedWarnings());
            Assert.True(completed.Succeeded, completed.Error);
        }
    }

    private static CompleteAntibodyIdWorkupRequest ReviewedWarnings() =>
        new("Reviewed leftover CannotExclude, history, DAT, none-identified, and conflicting findings.");

    private static ReviewAntibodyIdWorkupRequest AcceptReview(string comment = "Agree.") =>
        new(true, comment, ReviewedWarnings().WarningAcknowledgment);

    private async Task SeedReservedUnitAsync(long patientId, string key)
    {
        await using var context = _factory.Create();
        var productType = new ProductType
        {
            ProductCode = $"RBC-{key}",
            Name = "Test RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        context.ProductTypes.Add(productType);
        await context.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = $"U-{key}",
            ProductTypeId = productType.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        context.BloodUnits.Add(unit);
        await context.SaveChangesAsync();

        context.Allocations.Add(new Allocation
        {
            BloodProductId = unit.Id,
            PatientId = patientId,
            Status = AllocationStatus.Reserved,
            AssignmentType = AssignmentType.Reservation,
            AllocatedUtc = _factory.Clock.UtcNow,
            AllocatedBy = "tech-abid"
        });
        await context.SaveChangesAsync();
    }

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

    private async Task<long> SeedAntigenAttributeAsync(string code, string name, string antibodyName)
    {
        await using var context = _factory.Create();
        var existing = await context.BloodAttributeDefinitions.FirstOrDefaultAsync(a => a.Code == code);
        if (existing is not null)
        {
            return existing.Id;
        }

        var attr = new BloodAttributeDefinition
        {
            Code = code,
            Name = name,
            AntibodyName = antibodyName,
            IsClinicallySignificant = true,
            SortOrder = 20,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = _factory.Clock.UtcNow,
            Version = 1
        };
        context.BloodAttributeDefinitions.Add(attr);
        await context.SaveChangesAsync();
        return attr.Id;
    }

    private async Task SeedAgtypeAsync()
    {
        await using var context = _factory.Create();
        if (await context.TestDefinitions.AnyAsync(t => t.Code == "AGTYPE"))
        {
            return;
        }

        context.TestDefinitions.Add(new TestDefinition
        {
            Code = "AGTYPE",
            Name = "Antigen Typing Panel",
            Category = TestCategory.AntigenTyping,
            ResultValueType = ResultValueType.BloodAttribute,
            BloodAttributeScopeJson = BloodAttributeScope.Serialize([new BloodAttributeScopeEntry("K")]),
            BloodAttributeScopeKind = BloodAttributeKind.Antigen,
            VerificationRequired = true,
            ContributesToCompatibility = true,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = _factory.Clock.UtcNow,
            Version = 1
        });
        await context.SaveChangesAsync();
    }

    private async Task SeedAbtypeAsync()
    {
        await using var context = _factory.Create();
        if (await context.TestDefinitions.AnyAsync(t => t.Code == "ABTYPE"))
        {
            return;
        }

        context.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABTYPE",
            Name = "Antibody Attribute Panel",
            Category = TestCategory.AntibodyIdentification,
            ResultValueType = ResultValueType.BloodAttribute,
            BloodAttributeScopeJson = BloodAttributeScope.Serialize([new BloodAttributeScopeEntry("K")]),
            BloodAttributeScopeKind = BloodAttributeKind.Antibody,
            VerificationRequired = true,
            ContributesToAntibodyHistory = true,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = _factory.Clock.UtcNow,
            Version = 1
        });
        await context.SaveChangesAsync();
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
