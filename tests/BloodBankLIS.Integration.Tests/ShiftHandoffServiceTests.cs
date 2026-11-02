using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
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
