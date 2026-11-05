using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
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
            mtpActivations: directory ? new EfRepository<MtpActivation>(context) : null);

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
            new MtpActivation { PatientId = patient.Id, RequestedBy = "Dr. A", Reason = "Earlier", ActivatedBy = "tech1", ActivatedUtc = _factory.Clock.UtcNow.AddDays(-1), Status = MtpActivationStatus.Closed });
        await context.SaveChangesAsync();

        var created = await CreateService(context, permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff), directory: true)
            .CreateAsync(new CreateShiftHandoffRequest($"MTP running in trauma bay {Guid.NewGuid():N}"));

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(activeBefore + 1, created.Value!.ActiveMtp);
        Assert.Equal(activeBefore + 1, ShiftHandoffDto.From(created.Value).ActiveMtp);
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
}
