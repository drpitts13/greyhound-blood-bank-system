using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ShiftHandoffServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ShiftHandoffServiceTests(SqliteContextFactory factory) => _factory = factory;

    private ShiftHandoffService CreateService(
        BloodBankDbContext context,
        ICurrentUser? user = null,
        IPermissionEvaluator? permissions = null,
        bool directory = false) =>
        new(
            new EfRepository<ShiftHandoff>(context),
            context,
            _factory.Clock,
            user ?? _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, user ?? _factory.CurrentUser),
            permissions: permissions,
            users: directory ? new EfRepository<User>(context) : null,
            mtpActivations: directory ? new EfRepository<MtpActivation>(context) : null,
            issues: directory ? new EfRepository<Issue>(context) : null);

    // TEST-BB-330: a read-only permission cannot accept responsibility for open bench work.
    [Fact]
    public async Task Create_And_Acknowledge_WithAuditReadOnly_AreRefused()
    {
        await using var context = _factory.Create();
        var readOnly = new FixedPermissionEvaluator(0, PermissionCodes.AuditRead, PermissionCodes.ReportRun);
        var note = $"read-only {Guid.NewGuid():N}";
        var denied = await CreateService(context, permissions: readOnly).CreateAsync(new CreateShiftHandoffRequest(note));
        Assert.False(denied.Succeeded);
        Assert.Contains("shift.handoff", denied.Error);

        var created = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff))
            .CreateAsync(new CreateShiftHandoffRequest(note));
        var ack = await CreateService(context, user: new TestCurrentUser("auditor1", "WS-A"), permissions: readOnly)
            .AcknowledgeAsync(created.Value!.Id);
        Assert.False(ack.Succeeded);
        Assert.Null((await context.ShiftHandoffs.FindAsync(created.Value.Id))!.AcknowledgedUtc);
    }

    // TEST-BB-330: a named recipient must be an active user other than the sender.
    [Fact]
    public async Task Create_ToUnknownOrSelf_IsRefused_ToActiveUser_Succeeds()
    {
        await using var context = _factory.Create();
        var name = $"night{Guid.NewGuid():N}"[..14];
        context.Users.Add(new User { UserName = name, DisplayName = "Night Tech", IsActive = true });
        await context.SaveChangesAsync();
        var perms = new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff);
        var before = await context.ShiftHandoffs.CountAsync();

        var typo = await CreateService(context, permissions: perms, directory: true)
            .CreateAsync(new CreateShiftHandoffRequest("Two units in a cooler", name + "x"));
        Assert.False(typo.Succeeded);
        Assert.Contains("not an active user", typo.Error);

        var self = await CreateService(context, permissions: perms, directory: true)
            .CreateAsync(new CreateShiftHandoffRequest("Two units in a cooler", _factory.CurrentUser.UserName));
        Assert.False(self.Succeeded);
        Assert.Equal(before, await context.ShiftHandoffs.CountAsync());

        var ok = await CreateService(context, permissions: perms, directory: true)
            .CreateAsync(new CreateShiftHandoffRequest("Two units in a cooler", name.ToUpperInvariant()));
        Assert.True(ok.Succeeded, ok.Error);
    }

    // TEST-BB-330: the packet counts active massive transfusion protocols.
    [Fact]
    public async Task Create_CountsActiveMassiveTransfusionProtocols()
    {
        await using var context = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = $"MRN-HO-{Guid.NewGuid():N}"[..20], LastName = "Handoff", FirstName = "Mtp", DateOfBirth = new DateOnly(1980, 1, 1) };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var activeBefore = await context.MtpActivations.CountAsync(a => a.Status == MtpActivationStatus.Active);
        context.MtpActivations.AddRange(
            new MtpActivation { PatientId = patient.Id, RequestedBy = "Dr. A", Reason = "Shock", ActivatedBy = "tech1", ActivatedUtc = _factory.Clock.UtcNow },
            new MtpActivation { PatientId = patient.Id, RequestedBy = "Dr. A", Reason = "Earlier", ActivatedBy = "tech1", ActivatedUtc = _factory.Clock.UtcNow.AddDays(-1), Status = MtpActivationStatus.Closed },
            new MtpActivation { PatientId = patient.Id, RequestedBy = "Dr. A", Reason = "Left open", ActivatedBy = "tech1", ActivatedUtc = _factory.Clock.UtcNow.AddHours(-6) });
        await context.SaveChangesAsync();

        var created = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff), directory: true)
            .CreateAsync(new CreateShiftHandoffRequest($"MTP running in trauma bay {Guid.NewGuid():N}"));

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(activeBefore + 2, created.Value!.ActiveMtp);
        var dto = ShiftHandoffDto.From(created.Value);
        Assert.Equal(activeBefore + 2, dto.ActiveMtp);
        // TEST-BB-714: the one called six hours ago with nothing issued is counted as idle (default 240 minutes), kept with
        // the handoff, and named on the item and the summary; the one called now is not.
        Assert.True(created.Value.ActiveMtpIdle >= 1);
        Assert.True(created.Value.ActiveMtpIdle < created.Value.ActiveMtp);
        Assert.Equal(created.Value.ActiveMtpIdle, dto.ActiveMtpIdle);
        Assert.Contains($"({dto.ActiveMtpIdle} idle, to close)", dto.QueueSummary);
        var item = Assert.Single(ShiftHandoffItems.For(dto), i => i.Label == "Massive transfusion active");
        Assert.Equal(($"{dto.ActiveMtpIdle} idle, to close", true), (item.Detail, item.Urgent));
    }

    // TEST-BB-460: the packet counts coolers still in transit and how many were not checked or had differences. The
    // handoff user does not need issue.create for the count.
    [Fact]
    public async Task Create_CountsCoolersInTransit_AndTheirChecks()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var product = new ProductType { ProductCode = $"RBC-HO-{key}", Name = "Handoff RBC", ComponentClass = ComponentClass.RedBloodCells };
        var patient = new Patient { MedicalRecordNumber = $"MRN-HOC-{key}", LastName = "Handoff", FirstName = "Cooler", DateOfBirth = new DateOnly(1980, 1, 1) };
        context.AddRange(product, patient);
        await context.SaveChangesAsync();
        BloodUnit Unit(string suffix) => new()
        {
            UnitNumber = $"U-HOC{suffix}-{key}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10), Status = UnitStatus.Issued
        };
        var (a, b, c, d) = (Unit("A"), Unit("B"), Unit("C"), Unit("D"));
        context.BloodUnits.AddRange(a, b, c, d);
        await context.SaveChangesAsync();
        Issue Issued(BloodUnit unit, string? cooler, DateTime? received = null) => new()
        {
            BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-5), IssuedBy = "tech1",
            Status = IssueStatus.Issued, CoolerId = cooler, WardReceivedUtc = received
        };
        context.Issues.AddRange(Issued(a, $"HO-CHK-{key}"), Issued(b, $"HO-NEW-{key}"), Issued(c, null), Issued(d, $"HO-DONE-{key}", _factory.Clock.UtcNow));
        await context.SaveChangesAsync();

        var coolers = new CoolerCheckService(
            new EfRepository<Issue>(context), new InventoryRepository(context), new EfRepository<BloodUnit>(context),
            new EfRepository<ProductType>(context), new EfRepository<Patient>(context),
            new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate), _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser), context, new AuditQuery(context));
        var before = await coolers.SummarizeOpenAsync();
        Assert.True((await coolers.CheckAsync(new CoolerCheckRequest($"HO-CHK-{key}", [], Finish: true))).Succeeded);

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff),
            coolers: coolers);
        var created = await service.CreateAsync(new CreateShiftHandoffRequest($"Two coolers out {key}"));
        Assert.True(created.Succeeded, created.Error);
        var row = created.Value!;
        Assert.Equal(before.Count, row.CoolersInTransit);
        Assert.Equal(before.Count(o => o.LastCheckedUtc is null) - 1, row.CoolersNotChecked);
        Assert.Equal(before.Count(o => o.LastCheckedUtc is not null && o.LastCheckReady != true) + 1, row.CoolersWithDifferences);
        Assert.Contains(before, o => o.CoolerId == $"HO-NEW-{key}");
        Assert.DoesNotContain(before, o => o.CoolerId == $"HO-DONE-{key}");
        Assert.Contains("coolers out", ShiftHandoffDto.From(row).QueueSummary);
    }

    [Fact]
    public async Task Create_WithoutShiftHandoffPermission_IsHardStopped()
    {
        await using var context = _factory.Create();
        var note = $"deny-perm-{Guid.NewGuid():N}";
        var denied = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.ResultEnter))
            .CreateAsync(new CreateShiftHandoffRequest(note));
        Assert.False(denied.Succeeded);
        Assert.Equal(ShiftHandoffRule.EvaluatePermission(false).Message, denied.Error);
        Assert.DoesNotContain(context.ShiftHandoffs, h => h.Notes == note);
    }

    [Fact]
    public async Task Create_WithoutNotes_IsHardStopped()
    {
        await using var context = _factory.Create();
        var before = await context.ShiftHandoffs.CountAsync();
        var denied = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff))
            .CreateAsync(new CreateShiftHandoffRequest("  "));
        Assert.False(denied.Succeeded);
        Assert.Equal(ShiftHandoffRule.EvaluateNotes("  ").Message, denied.Error);
        Assert.Equal(before, await context.ShiftHandoffs.CountAsync());
    }

    [Fact]
    public async Task Create_WritesSnapshotAndAudit()
    {
        await using var context = _factory.Create();
        var note = $"Two units in transit; fatality follow-up open. {Guid.NewGuid():N}";
        var created = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff))
            .CreateAsync(new CreateShiftHandoffRequest(note, "tech2"));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(note, created.Value!.Notes);
        Assert.Equal("tech2", created.Value.ToUser);
        Assert.Equal(_factory.CurrentUser.UserName, created.Value.FromUser);
        Assert.Null(created.Value.AcknowledgedUtc);
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EventType == AuditEventType.ShiftHandoff
            && a.EntityId == created.Value.Id
            && a.Reason == "Shift handoff recorded."));
        var open = await CreateService(context).GetLatestOpenAsync();
        Assert.NotNull(open);
        Assert.Equal(created.Value.Id, open!.Id);
    }

    [Fact]
    public async Task Acknowledge_SameUser_IsHardStopped()
    {
        await using var context = _factory.Create();
        var created = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff))
            .CreateAsync(new CreateShiftHandoffRequest($"Handoff note {Guid.NewGuid():N}"));
        Assert.True(created.Succeeded, created.Error);

        var denied = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff))
            .AcknowledgeAsync(created.Value!.Id);
        Assert.False(denied.Succeeded);
        Assert.Equal(
            ShiftHandoffRule.EvaluateAcknowledge(
                created.Value.FromUser, _factory.CurrentUser.UserName, null).Message,
            denied.Error);
        Assert.Null((await context.ShiftHandoffs.FindAsync(created.Value.Id))!.AcknowledgedUtc);
    }

    [Fact]
    public async Task Acknowledge_DifferentUser_SucceedsOnce()
    {
        await using var context = _factory.Create();
        var created = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff))
            .CreateAsync(new CreateShiftHandoffRequest($"Handoff note {Guid.NewGuid():N}"));
        Assert.True(created.Succeeded, created.Error);

        var incoming = new TestCurrentUser("tech-night", "WORKSTATION-N");
        var acked = await CreateService(
                context,
                user: incoming,
                permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff))
            .AcknowledgeAsync(created.Value!.Id);
        Assert.True(acked.Succeeded, acked.Error);
        Assert.Equal("tech-night", acked.Value!.AcknowledgedBy);
        Assert.NotNull(acked.Value.AcknowledgedUtc);
        Assert.NotNull((await context.ShiftHandoffs.FindAsync(created.Value.Id))!.AcknowledgedUtc);

        var again = await CreateService(
                context,
                user: incoming,
                permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff))
            .AcknowledgeAsync(created.Value.Id);
        Assert.False(again.Succeeded);
        Assert.Equal(
            ShiftHandoffRule.EvaluateAcknowledge(
                created.Value.FromUser, "tech-night", acked.Value.AcknowledgedUtc).Message,
            again.Error);
    }

    // TEST-BB-493: a handoff counts deviations not yet closed and the critical ones, without deviation.manage, and keeps
    // both in its audit snapshot.
    [Fact]
    public async Task Create_CountsOpenAndCriticalDeviations()
    {
        await using var context = _factory.Create();
        context.Deviations.AddRange(
            new Deviation { Title = "Critical open", Description = "d", Severity = DeviationSeverity.Critical, Status = DeviationStatus.UnderReview },
            new Deviation { Title = "Major open", Description = "d", Severity = DeviationSeverity.Major },
            new Deviation { Title = "Critical closed", Description = "d", Severity = DeviationSeverity.Critical, Status = DeviationStatus.Closed, CorrectiveAction = "done" });
        await context.SaveChangesAsync();
        var expectedOpen = await context.Deviations.CountAsync(d => d.Status != DeviationStatus.Closed);
        var expectedCritical = await context.Deviations.CountAsync(d => d.Status != DeviationStatus.Closed && d.Severity == DeviationSeverity.Critical);

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            deviations: new EfRepository<Deviation>(context));
        var created = await service.CreateAsync(new CreateShiftHandoffRequest("Deviation count check"));
        Assert.True(created.Succeeded, created.Error);

        Assert.Equal(expectedOpen, created.Value!.OpenDeviations);
        Assert.Equal(expectedCritical, created.Value.CriticalDeviations);
        Assert.True(expectedCritical >= 1 && expectedOpen >= 2);
        var audit = await context.AuditEvents.Where(e => e.EntityType == nameof(ShiftHandoff) && e.EntityId == created.Value.Id).ToListAsync();
        Assert.Contains(audit, e => e.NewValueJson != null && e.NewValueJson.Contains("CriticalDeviations", StringComparison.OrdinalIgnoreCase));
    }

    // TEST-BB-553: a handoff counts open instrument failures and the results held among them, and reagent lots whose
    // latest QC failed with the results still to review; the summary leads with failed reagent QC.
    [Fact]
    public async Task Create_CountsInstrumentFailuresAndFailedReagentLots()
    {
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var iface = new InstrumentInterface { Name = $"HO-{Guid.NewGuid():N}"[..12], Connection = new InstrumentConnection { Port = 2999 } };
        context.InstrumentInterfaces.Add(iface);
        await context.SaveChangesAsync();
        InstrumentMessageLog Message(string? error, DateTime? resolved = null) => new()
        {
            InterfaceId = iface.Id, RawMessage = "x", Status = InstrumentMessageStatus.Failed, ReceivedUtc = now, ErrorDetail = error, ResolvedUtc = resolved
        };
        context.InstrumentMessages.AddRange(
            Message("Instrument error Q (Hold): ABORH for ACC held for review, not posted (INST-HOLD)."),
            Message("Instrument code X has no translation to a test (INST-CODE)"),
            Message("held (INST-HOLD)", resolved: now));
        var reagent = new Reagent { Code = $"HO-{Guid.NewGuid():N}"[..10], Name = "Handoff AHG", TestCodes = "HOSC" };
        context.Reagents.Add(reagent);
        await context.SaveChangesAsync();
        var lot = new ReagentLot { ReagentId = reagent.Id, LotNumber = "HO-1", ReceivedUtc = now.AddDays(-3), ExpiresUtc = now.AddDays(30), State = ReagentLotState.Opened };
        context.ReagentLots.Add(lot);
        await context.SaveChangesAsync();
        context.ReagentDailyQcs.AddRange(
            new ReagentDailyQc { ReagentLotId = lot.Id, QcDateUtc = now.AddDays(-1).Date, ExpectedResult = "4+", ObservedResult = "4+", Passed = true, PerformedBy = "a", PerformedUtc = now.AddHours(-24), ReviewedBy = "b", ReviewedUtc = now.AddHours(-23) },
            new ReagentDailyQc { ReagentLotId = lot.Id, QcDateUtc = now.Date, ExpectedResult = "4+", ObservedResult = "0", Passed = false, PerformedBy = "a", PerformedUtc = now.AddHours(-1) });
        context.TestResultReagentLots.AddRange(
            new TestResultReagentLot { TestResultId = 900001, ReagentLotId = lot.Id, ReagentCode = reagent.Code, LotNumber = "HO-1", RecordedUtc = now.AddHours(-3) },
            new TestResultReagentLot { TestResultId = 900002, ReagentLotId = lot.Id, ReagentCode = reagent.Code, LotNumber = "HO-1", RecordedUtc = now.AddHours(-2) },
            new TestResultReagentLot { TestResultId = 900003, ReagentLotId = lot.Id, ReagentCode = reagent.Code, LotNumber = "HO-1", RecordedUtc = now.AddHours(-30) });
        await context.SaveChangesAsync();
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser);
        var reagents = new ReagentQcService(
            new EfRepository<Reagent>(context), new EfRepository<ReagentLot>(context), new EfRepository<ReagentDailyQc>(context),
            context, _factory.Clock, _factory.CurrentUser, audit, new EfRepository<TestResultReagentLot>(context));
        var expectedOpen = await context.InstrumentMessages.CountAsync(m => m.Status == InstrumentMessageStatus.Failed && m.ResolvedUtc == null);

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: audit,
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            instrumentMessages: new EfRepository<InstrumentMessageLog>(context),
            reagentQc: reagents);
        var created = await service.CreateAsync(new CreateShiftHandoffRequest("Instruments and reagents"));

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(expectedOpen, created.Value!.InstrumentFailuresOpen);
        Assert.True(created.Value.InstrumentResultsHeld >= 1 && created.Value.InstrumentFailuresOpen >= 2);
        Assert.True(created.Value.ReagentLotsFailedQc >= 1);
        Assert.True(created.Value.ReagentResultsToReview >= 2);
        var summary = ShiftHandoffDto.From(created.Value).QueueSummary;
        Assert.StartsWith("REAGENT QC FAILED:", summary);
        Assert.Contains("held for review", summary);
    }

    // TEST-BB-577: a handoff counts RhIG evaluations still needing work and, of those, the ones to give.
    [Fact]
    public async Task Create_CountsRhigEvaluationsStillNeedingWork()
    {
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var patient = new Patient { MedicalRecordNumber = $"MRN-HO-RH-{Guid.NewGuid():N}"[..20], LastName = "Handoff", FirstName = "Rh", DateOfBirth = new DateOnly(1990, 1, 1) };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        RhigEvaluation Eval(RhigDecision decision, bool completed, bool given) => new()
        {
            PatientId = patient.Id, EventUtc = now, Decision = decision, RecordedBy = "tech", RecordedUtc = now,
            CompletedUtc = completed ? now : null, CompletedBy = completed ? "lead" : null,
            GivenUtc = given ? now : null, GivenLot = given ? "L1" : null, GivenVials = given ? 1 : null
        };
        context.RhigEvaluations.AddRange(
            Eval(RhigDecision.Undetermined, false, false),
            Eval(RhigDecision.Candidate, true, false),
            Eval(RhigDecision.Candidate, true, true),
            Eval(RhigDecision.NotCandidate, true, false));
        await context.SaveChangesAsync();
        var expectedOpen = await context.RhigEvaluations.CountAsync(e => e.CompletedUtc == null || (e.Decision == RhigDecision.Candidate && e.GivenUtc == null));
        var expectedToGive = await context.RhigEvaluations.CountAsync(e => e.CompletedUtc != null && e.Decision == RhigDecision.Candidate && e.GivenUtc == null);

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            rhig: new EfRepository<RhigEvaluation>(context));
        var created = await service.CreateAsync(new CreateShiftHandoffRequest("RhIG"));

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal((expectedOpen, expectedToGive), (created.Value!.RhigOpen, created.Value.RhigToGive));
        Assert.True(expectedOpen >= 2 && expectedToGive >= 1);
    }

    // TEST-BB-794: a handoff counts antibody workups not completed or voided, and of those the ones interpreted and not
    // yet reviewed; a completed or voided workup is not counted, and the counts are kept on the saved handoff.
    [Fact]
    public async Task Create_CountsOpenAntibodyWorkupsAndThoseToReview()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var patient = new Patient { MedicalRecordNumber = $"ABID-HO-{key}", LastName = "Handoff", FirstName = "Abid", DateOfBirth = new DateOnly(1980, 1, 1) };
        var manufacturer = new BloodBankLIS.Domain.Entities.Configuration.AntibodyPanelManufacturer { Code = $"HO{key}", Name = "Handoff panels", IsActive = true, IsDraft = false, EffectiveUtc = _factory.Clock.UtcNow, Version = 1 };
        context.Patients.Add(patient);
        context.AntibodyPanelManufacturers.Add(manufacturer);
        await context.SaveChangesAsync();
        var lot = new AntibodyPanelLot { ManufacturerId = manufacturer.Id, LotNumber = $"HO-{key}", ExpiresOn = new DateOnly(2030, 1, 1), PanelName = "Panel", IsActive = true };
        context.AntibodyPanelLots.Add(lot);
        await context.SaveChangesAsync();
        var now = _factory.Clock.UtcNow;
        context.AntibodyIdentificationWorkups.AddRange(
            new AntibodyIdentificationWorkup { PatientId = patient.Id, PrimaryLotId = lot.Id, Status = AntibodyWorkupStatus.InProgress },
            new AntibodyIdentificationWorkup { PatientId = patient.Id, PrimaryLotId = lot.Id, Status = AntibodyWorkupStatus.PendingSupervisorReview, TechnologistInterpretation = "anti-K", InterpretedUtc = now },
            new AntibodyIdentificationWorkup { PatientId = patient.Id, PrimaryLotId = lot.Id, Status = AntibodyWorkupStatus.PendingSupervisorReview, TechnologistInterpretation = "anti-E", InterpretedUtc = now, ReviewedUtc = now, SupervisorAccepted = true },
            new AntibodyIdentificationWorkup { PatientId = patient.Id, PrimaryLotId = lot.Id, Status = AntibodyWorkupStatus.Completed, InterpretedUtc = now, ReviewedUtc = now },
            new AntibodyIdentificationWorkup { PatientId = patient.Id, PrimaryLotId = lot.Id, Status = AntibodyWorkupStatus.Voided, VoidReason = "Wrong patient" });
        await context.SaveChangesAsync();
        var open = context.AntibodyIdentificationWorkups.Where(w => w.Status != AntibodyWorkupStatus.Completed && w.Status != AntibodyWorkupStatus.Voided);
        var expectedOpen = await open.CountAsync();
        var expectedReview = await open.CountAsync(w => w.InterpretedUtc != null && w.ReviewedUtc == null);

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            antibodyWorkups: new EfRepository<AntibodyIdentificationWorkup>(context));
        var created = await service.CreateAsync(new CreateShiftHandoffRequest("Antibody workups"));

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal((expectedOpen, expectedReview), (created.Value!.AntibodyWorkupsOpen, created.Value.AntibodyWorkupsToReview));
        Assert.True(expectedOpen >= 3 && expectedReview >= 1);
        var saved = await context.ShiftHandoffs.AsNoTracking().SingleAsync(h => h.Id == created.Value.Id);
        Assert.Equal((expectedOpen, expectedReview), (saved.AntibodyWorkupsOpen, saved.AntibodyWorkupsToReview));
    }

    // TEST-BB-842: a handoff counts outbound messages queued or failed past the stuck window; a fresh one, an
    // acknowledged one, an inbound one, and a charge withheld because it was cancelled are not counted; the saved
    // handoff keeps the count.
    [Fact]
    public async Task Create_CountsOutboundMessagesNotSent()
    {
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var key = Guid.NewGuid().ToString("N")[..6];
        Hl7MessageLog Msg(string control, Hl7Direction direction, Hl7MessageStatus status, TimeSpan age, string? detail = null) => new()
        {
            Direction = direction, MessageType = "DFT", MessageControlId = $"{control}-{key}", RawMessage = "MSH|",
            Status = status, ReceivedUtc = now - age, ErrorDetail = detail
        };
        context.Hl7Messages.AddRange(
            Msg("OLD", Hl7Direction.Outbound, Hl7MessageStatus.Received, TimeSpan.FromDays(2)),
            Msg("FAILED", Hl7Direction.Outbound, Hl7MessageStatus.Errored, TimeSpan.FromHours(1)),
            Msg("FRESH", Hl7Direction.Outbound, Hl7MessageStatus.Received, TimeSpan.FromMinutes(3)),
            Msg("ACKED", Hl7Direction.Outbound, Hl7MessageStatus.Acked, TimeSpan.FromDays(1)),
            Msg("INBOUND", Hl7Direction.Inbound, Hl7MessageStatus.Received, TimeSpan.FromDays(1)),
            Msg("WITHHELD", Hl7Direction.Outbound, Hl7MessageStatus.Received, TimeSpan.FromDays(1), BloodBankLIS.Application.Billing.BillingService.UnsentDftWithheldDetail));
        await context.SaveChangesAsync();
        var expected = (await context.Hl7Messages.ToListAsync()).Count(m => m.Direction == Hl7Direction.Outbound
            && (m.Status == Hl7MessageStatus.Received || m.Status == Hl7MessageStatus.Errored)
            && m.ErrorDetail != BloodBankLIS.Application.Billing.BillingService.UnsentDftWithheldDetail
            && OutboundMessageWaitRule.IsStuck(m.ReceivedUtc, now));

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            hl7Messages: new EfRepository<Hl7MessageLog>(context));
        var created = await service.CreateAsync(new CreateShiftHandoffRequest("Outbound"));

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(expected, created.Value!.Hl7OutboundNotSent);
        Assert.True(expected >= 2);
        Assert.Equal(expected, (await context.ShiftHandoffs.AsNoTracking().SingleAsync(h => h.Id == created.Value.Id)).Hl7OutboundNotSent);
    }

    // TEST-BB-851: a handoff counts recorded outages not yet ended (an ended one is not counted) and keeps the count.
    [Fact]
    public async Task Create_CountsOutagesStillOpen()
    {
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        context.DowntimeEpisodes.AddRange(
            new DowntimeEpisode { StartedUtc = now.AddDays(-3), EndedUtc = now.AddDays(-3).AddHours(2), Description = "Ended", CreatedBy = "tech-test", CreatedUtc = now },
            new DowntimeEpisode { StartedUtc = now.AddHours(-4), Description = "Left open", CreatedBy = "tech-test", CreatedUtc = now });
        await context.SaveChangesAsync();
        var expected = await context.DowntimeEpisodes.CountAsync(e => e.EndedUtc == null);

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            outages: new EfRepository<DowntimeEpisode>(context));
        var created = await service.CreateAsync(new CreateShiftHandoffRequest("Outage"));

        Assert.True(created.Succeeded, created.Error);
        Assert.True(expected >= 1);
        Assert.Equal(expected, created.Value!.OutagesOpen);
        Assert.Equal(expected, (await context.ShiftHandoffs.AsNoTracking().SingleAsync(h => h.Id == created.Value.Id)).OutagesOpen);
    }

    // TEST-BB-890 (Cycle 679): a handoff counts active outside antibody reports not yet confirmed (a confirmed one, an
    // inactive one and a local antibody are not counted) and keeps the count.
    [Fact]
    public async Task Create_CountsOutsideAntibodiesToConfirm()
    {
        await using var context = _factory.Create();
        var patient = new Patient { MedicalRecordNumber = $"MRN-HO-OUT-{Guid.NewGuid():N}"[..20], LastName = "Handoff", FirstName = "Outside", DateOfBirth = new DateOnly(1980, 1, 1) };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        context.AntibodyHistory.AddRange(
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-Jka", IsActive = true, ReportingFacility = "St. Mary's" },
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-S", IsActive = true, ReportingFacility = "St. Mary's", ConfirmedUtc = _factory.Clock.UtcNow },
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-M", IsActive = false, ReportingFacility = "St. Mary's" },
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-K", IsActive = true });
        await context.SaveChangesAsync();
        var expected = await context.AntibodyHistory.CountAsync(a => a.IsActive && a.ReportingFacility != null && a.ConfirmedUtc == null);

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            antibodyHistory: new EfRepository<AntibodyHistory>(context));
        var created = await service.CreateAsync(new CreateShiftHandoffRequest("Outside reports"));

        Assert.True(created.Succeeded, created.Error);
        Assert.True(expected >= 1);
        Assert.Equal(expected, created.Value!.OutsideAntibodiesToConfirm);
        Assert.Equal(expected, (await context.ShiftHandoffs.AsNoTracking().SingleAsync(h => h.Id == created.Value.Id)).OutsideAntibodiesToConfirm);
    }

    // TEST-BB-616: a handoff counts unidentified patients not yet merged (a merged one is not counted).
    [Fact]
    public async Task Create_CountsUnidentifiedPatientsNotYetMerged()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        context.Patients.AddRange(
            new Patient { MedicalRecordNumber = $"UNK-HO-{key}-1", LastName = "UNIDENTIFIED", FirstName = "TRAUMA 1", DateOfBirth = new DateOnly(1990, 1, 1), Unidentified = true },
            new Patient { MedicalRecordNumber = $"UNK-HO-{key}-2", LastName = "UNIDENTIFIED", FirstName = "TRAUMA 2", DateOfBirth = new DateOnly(1990, 1, 1), Unidentified = true, Status = PatientStatus.Merged });
        await context.SaveChangesAsync();
        var expected = await context.Patients.CountAsync(p => p.Unidentified && p.Status != PatientStatus.Merged);

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            patients: new EfRepository<Patient>(context));
        var created = await service.CreateAsync(new CreateShiftHandoffRequest("Unidentified"));

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(expected, created.Value!.UnidentifiedPatients);
        Assert.True(expected >= 1);
        Assert.Equal(0, created.Value.TubesDueForDiscard);
    }

    // TEST-BB-554: a handoff counts open HL7 errors and open unmapped interface values (resolved ones are not counted).
    [Fact]
    public async Task Create_CountsHl7ErrorsAndUnmappedInterfaceValues()
    {
        await using var context = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var log = new Hl7MessageLog { RawMessage = "MSH|^~\\&|", Direction = Hl7Direction.Inbound, ReceivedUtc = now };
        context.Hl7Messages.Add(log);
        await context.SaveChangesAsync();
        context.InterfaceErrorQueue.AddRange(
            new InterfaceErrorQueueItem { Hl7MessageId = log.Id, ErrorType = "Mapping", ErrorDetail = "HL7-ORDER-CODE" },
            new InterfaceErrorQueueItem { Hl7MessageId = log.Id, ErrorType = "Mapping", ErrorDetail = "done", Resolved = true, ResolvedUtc = now });
        context.InterfaceUnmappedValues.AddRange(
            new InterfaceUnmappedValue { DataItemKey = "Order.TestCode", Value = "LAB-HO", Direction = InterfaceTranslationDirection.Inbound, FirstSeenUtc = now, LastSeenUtc = now, Count = 1 },
            new InterfaceUnmappedValue { DataItemKey = "Order.TestCode", Value = "LAB-HO2", Direction = InterfaceTranslationDirection.Inbound, FirstSeenUtc = now, LastSeenUtc = now, Count = 1, ResolvedUtc = now });
        await context.SaveChangesAsync();
        var expectedErrors = await context.InterfaceErrorQueue.CountAsync(e => !e.Resolved);
        var expectedUnmapped = await context.InterfaceUnmappedValues.CountAsync(v => v.ResolvedUtc == null);

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            hl7Errors: new EfRepository<InterfaceErrorQueueItem>(context),
            unmapped: new EfRepository<InterfaceUnmappedValue>(context));
        var created = await service.CreateAsync(new CreateShiftHandoffRequest("Interfaces"));

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal((expectedErrors, expectedUnmapped), (created.Value!.Hl7ErrorsOpen, created.Value.UnmappedInterfaceValues));
        Assert.True(expectedErrors >= 1 && expectedUnmapped >= 1);
        var summary = ShiftHandoffDto.From(created.Value).QueueSummary;
        Assert.Contains($"HL7 errors {expectedErrors}", summary);
        Assert.Contains($"unmapped interface values {expectedUnmapped}", summary);
    }

    // TEST-BB-555: the preview shows what a handoff recorded now would carry, saves nothing, matches what recording then
    // stores, and needs the shift.handoff permission like recording does.
    [Fact]
    public async Task Preview_MatchesTheRecordedCounts_AndSavesNothing()
    {
        await using var context = _factory.Create();
        context.Deviations.Add(new Deviation { Title = "Preview open", Description = "d", Severity = DeviationSeverity.Critical, Status = DeviationStatus.UnderReview });
        await context.SaveChangesAsync();
        ShiftHandoffService Service(IPermissionEvaluator permissions) => new(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: permissions,
            deviations: new EfRepository<Deviation>(context));
        var before = await context.ShiftHandoffs.CountAsync();

        var preview = await Service(new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff)).PreviewAsync();

        Assert.True(preview.Succeeded, preview.Error);
        Assert.Equal(before, await context.ShiftHandoffs.CountAsync());
        Assert.Equal(0, preview.Value!.Id);
        Assert.Contains("CRITICAL DEVIATION OPEN", preview.Value.QueueSummary);
        var recorded = await Service(new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff)).CreateAsync(new CreateShiftHandoffRequest("After preview"));
        Assert.True(recorded.Succeeded, recorded.Error);
        Assert.Equal(preview.Value.QueueSummary, ShiftHandoffDto.From(recorded.Value!).QueueSummary);

        var denied = await Service(new FixedPermissionEvaluator(0, PermissionCodes.ResultEnter)).PreviewAsync();
        Assert.False(denied.Succeeded);
    }

    // TEST-BB-508: a handoff counts products below their minimum stock level, without admin.config.edit, and keeps the
    // count in its audit snapshot.
    [Fact]
    public async Task Create_CountsProductsBelowMinimumStock()
    {
        await using var context = _factory.Create();
        var product = new ProductType { ProductCode = $"RBC-{Guid.NewGuid():N}"[..12], Name = "Red cells", ComponentClass = ComponentClass.RedBloodCells };
        context.ProductTypes.Add(product);
        await context.SaveChangesAsync();
        context.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = $"U-LOW-{Guid.NewGuid():N}"[..14], ProductTypeId = product.Id, Status = UnitStatus.Available,
            Abo = AboGroup.O, RhD = RhType.Negative, ExpiresUtc = _factory.Clock.UtcNow.AddDays(5)
        });
        context.MinimumStockLevels.AddRange(
            new MinimumStockLevel { ProductTypeId = product.Id, Abo = AboGroup.O, MinimumAvailable = 4 },
            new MinimumStockLevel { ProductTypeId = product.Id, Abo = AboGroup.A, MinimumAvailable = 0 });
        await context.SaveChangesAsync();
        var minimumStock = new MinimumStockService(
            new EfRepository<MinimumStockLevel>(context), new EfRepository<ProductType>(context), new InventoryRepository(context), context);
        var expected = (await minimumStock.ListAsync()).Count(r => r.Shortfall > 0);

        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(0, PermissionCodes.ShiftHandoff),
            minimumStock: minimumStock);
        var created = await service.CreateAsync(new CreateShiftHandoffRequest("Stock count check"));
        Assert.True(created.Succeeded, created.Error);

        Assert.Equal(expected, created.Value!.ProductsBelowMinimum);
        Assert.True(expected >= 1);
        Assert.Contains("BELOW MINIMUM STOCK", ShiftHandoffDto.From(created.Value).QueueSummary);
        var audit = await context.AuditEvents.Where(e => e.EntityType == nameof(ShiftHandoff) && e.EntityId == created.Value.Id).ToListAsync();
        Assert.Contains(audit, e => e.NewValueJson != null && e.NewValueJson.Contains("ProductsBelowMinimum", StringComparison.OrdinalIgnoreCase));
    }

    // TEST-BB-979: a handoff records the equipment checks due (failed or overdue counted apart) and the equipment out
    // of service, as the Equipment page lists them; checks on equipment out of service are not counted as due.
    [Fact]
    public async Task Handoff_CountsEquipment()
    {
        await using var context = _factory.Create();
        var equipment = new EquipmentService(new EfRepository<Equipment>(context), new EfRepository<EquipmentCheck>(context),
            new EfRepository<EquipmentCheckRecord>(context), context, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser));
        var key = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var washer = await equipment.CreateAsync(new SaveEquipmentRequest($"HO-CW-{key}", "Cell washer", "Cell washer", null, null));
        var washerCheck = await equipment.AddCheckAsync(washer.Value!.Id, new SaveEquipmentCheckRequest("Daily check", 1, null));
        Assert.True((await equipment.RecordAsync(washerCheck.Value!.Checks.Single().CheckId, new RecordEquipmentCheckRequest(false, "Low saline"))).Succeeded);
        var block = await equipment.CreateAsync(new SaveEquipmentRequest($"HO-HB-{key}", "Heat block", "Heat block", null, null));
        await equipment.AddCheckAsync(block.Value!.Id, new SaveEquipmentCheckRequest("Temperature", 1, null));
        Assert.True((await equipment.SetInServiceAsync(block.Value.Id, false, "Broken")).Succeeded);

        var due = await equipment.ListDueAsync();
        var all = await equipment.ListAsync();
        var service = new ShiftHandoffService(
            new EfRepository<ShiftHandoff>(context), context, _factory.Clock, _factory.CurrentUser,
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff),
            equipment: equipment);
        var created = await service.CreateAsync(new CreateShiftHandoffRequest($"Equipment {key}"));
        Assert.True(created.Succeeded, created.Error);

        Assert.Equal(due.Count, created.Value!.EquipmentChecksToDo);
        Assert.Equal(due.Count(d => d.State is "Failed" or "Overdue"), created.Value.EquipmentChecksFailedOrOverdue);
        Assert.Equal(all.Count(e => e.OutOfServiceUtc is not null), created.Value.EquipmentOutOfService);
        Assert.Contains(due, d => d.EquipmentCode == $"HO-CW-{key}" && d.State == "Failed");
        Assert.DoesNotContain(due, d => d.EquipmentCode == $"HO-HB-{key}");
        Assert.True(created.Value.EquipmentOutOfService >= 1 && created.Value.EquipmentChecksFailedOrOverdue >= 1);
    }
}
