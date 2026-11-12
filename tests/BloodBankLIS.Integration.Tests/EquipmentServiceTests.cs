using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>The equipment register (Cycle 708).</summary>
public class EquipmentServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private EquipmentService Service(BloodBankDbContext c) =>
        new(new EfRepository<Equipment>(c), new EfRepository<EquipmentCheck>(c), new EfRepository<EquipmentCheckRecord>(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    private async Task<(long EquipmentId, long CheckId)> RegisterAsync(BloodBankDbContext c, string code, int interval = 1)
    {
        var svc = Service(c);
        var created = await svc.CreateAsync(new SaveEquipmentRequest(code, "Cell washer", "Cell washer", "Bench 1", "SN-1"));
        Assert.True(created.Succeeded, created.Error);
        var withCheck = await svc.AddCheckAsync(created.Value!.Id, new SaveEquipmentCheckRequest("Daily check", interval, "Per SOP BB-EQ-04"));
        Assert.True(withCheck.Succeeded, withCheck.Error);
        return (created.Value.Id, withCheck.Value!.Checks.Single().CheckId);
    }

    // TEST-BB-970: equipment is registered (code upper-cased, audited) with a check that is due until first recorded;
    // a passing record makes it current until its interval comes round, by date; the same code cannot be registered twice.
    [Fact]
    public async Task Register_Schedule_AndRecord_MakeTheCheckCurrent()
    {
        await using var c = _factory.Create();
        var svc = Service(c);
        var (equipmentId, checkId) = await RegisterAsync(c, "cw-01");

        var due = Assert.Single(await svc.ListDueAsync(), d => d.CheckId == checkId);
        Assert.Equal(("CW-01", nameof(EquipmentCheckRule.State.NeverDone)), (due.EquipmentCode, due.State));

        var recorded = await svc.RecordAsync(checkId, new RecordEquipmentCheckRequest(true, "Saline fill even"));
        Assert.True(recorded.Succeeded, recorded.Error);
        Assert.Equal("tech-test", recorded.Value!.PerformedBy);
        Assert.DoesNotContain(await svc.ListDueAsync(), d => d.CheckId == checkId);
        var check = (await svc.GetAsync(equipmentId))!.Checks.Single();
        Assert.Equal((nameof(EquipmentCheckRule.State.Current), true, "Saline fill even"), (check.State, check.LastPassed!.Value, check.LastNote));

        _factory.Clock.UtcNow = _factory.Clock.UtcNow.Date.AddDays(1).AddHours(1);
        Assert.Equal(nameof(EquipmentCheckRule.State.DueToday), Assert.Single(await svc.ListDueAsync(), d => d.CheckId == checkId).State);

        var again = await svc.CreateAsync(new SaveEquipmentRequest("CW-01", "Another", "Cell washer", null, null));
        Assert.False(again.Succeeded);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(Equipment) && e.EntityId == equipmentId && e.EventType == AuditEventType.Create));
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(EquipmentCheckRecord) && e.Reason!.Contains("Daily check passed on CW-01")));
    }

    // TEST-BB-971: a failed record needs a note and stays failed (on the due list first) until a pass; out of service
    // needs a reason, takes the equipment's checks off the due list, and is undone with a reason; a retired check and
    // a check with a bad interval are refused.
    [Fact]
    public async Task Failure_OutOfService_AndRefusals()
    {
        await using var c = _factory.Create();
        var svc = Service(c);
        var (equipmentId, checkId) = await RegisterAsync(c, "HB-01", interval: 30);

        Assert.False((await svc.RecordAsync(checkId, new RecordEquipmentCheckRequest(false, " "))).Succeeded);
        Assert.True((await svc.RecordAsync(checkId, new RecordEquipmentCheckRequest(false, "Read 33.0 °C"))).Succeeded);
        var failed = Assert.Single(await svc.ListDueAsync(), d => d.CheckId == checkId);
        Assert.Equal(nameof(EquipmentCheckRule.State.Failed), failed.State);

        Assert.False((await svc.SetInServiceAsync(equipmentId, inService: false, reason: "")).Succeeded);
        var outOfService = await svc.SetInServiceAsync(equipmentId, inService: false, reason: "Failed temperature check");
        Assert.True(outOfService.Succeeded, outOfService.Error);
        Assert.Equal(("Failed temperature check", "tech-test"), (outOfService.Value!.OutOfServiceReason, outOfService.Value.OutOfServiceBy));
        Assert.DoesNotContain(await svc.ListDueAsync(), d => d.EquipmentId == equipmentId);
        Assert.False((await svc.SetInServiceAsync(equipmentId, inService: false, reason: "again")).Succeeded);

        // A check can still be recorded while out of service (the check after repair).
        Assert.True((await svc.RecordAsync(checkId, new RecordEquipmentCheckRequest(true, "37.0 °C after repair"))).Succeeded);
        var back = await svc.SetInServiceAsync(equipmentId, inService: true, reason: "Repaired; temperature check passed");
        Assert.True(back.Succeeded, back.Error);
        Assert.True(back.Value!.InService);
        Assert.Equal(0, back.Value.ChecksNeedingAction);

        Assert.False((await svc.AddCheckAsync(equipmentId, new SaveEquipmentCheckRequest("Monthly", 0, null))).Succeeded);
        Assert.False((await svc.AddCheckAsync(equipmentId, new SaveEquipmentCheckRequest("Daily check", 1, null))).Succeeded);
        Assert.False((await svc.RetireCheckAsync(checkId, "")).Succeeded);
        Assert.True((await svc.RetireCheckAsync(checkId, "Replaced by the vendor's monthly check")).Succeeded);
        Assert.False((await svc.RecordAsync(checkId, new RecordEquipmentCheckRequest(true, null))).Succeeded);
        Assert.Equal(2, (await svc.ListRecordsAsync(equipmentId)).Count);
    }

    // TEST-BB-972: the demo equipment shows each state: an overdue centrifuge check, a cell washer check due today, and
    // a heat block out of service after a failed check (not on the due list).
    [Fact]
    public async Task Demo_ShowsEachState()
    {
        await using (var seed = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(seed);
        }

        // The demo is dated from the real clock.
        _factory.Clock.UtcNow = DateTime.UtcNow;
        await using var c = _factory.Create();
        var due = await Service(c).ListDueAsync();
        Assert.Contains(due, d => d.EquipmentCode == "CENT-01" && d.State == nameof(EquipmentCheckRule.State.Overdue));
        Assert.Contains(due, d => d.EquipmentCode == "CW-01");
        Assert.DoesNotContain(due, d => d.EquipmentCode == "HB-01");
        var heatBlock = (await Service(c).ListAsync()).Single(e => e.Code == "HB-01");
        Assert.False(heatBlock.InService);
        Assert.False(heatBlock.Checks.Single().LastPassed);
    }

    // TEST-BB-981: equipment details are corrected with a reason (the code does not change), and a check's interval is
    // changed with a reason, keeping its records: the due date follows from the last pass and the new interval. No
    // reason, a bad interval or a duplicate check name is refused; each change is audited with its reason.
    [Fact]
    public async Task Edit_EquipmentAndCheck_WithAReason()
    {
        await using var c = _factory.Create();
        var svc = Service(c);
        var (equipmentId, checkId) = await RegisterAsync(c, "CENT-EDIT", interval: 30);
        Assert.True((await svc.RecordAsync(checkId, new RecordEquipmentCheckRequest(true, null))).Succeeded);

        Assert.False((await svc.UpdateAsync(equipmentId, new UpdateEquipmentRequest("Centrifuge", "Centrifuge", "Bench 2", null, " "))).Succeeded);
        var moved = await svc.UpdateAsync(equipmentId, new UpdateEquipmentRequest("Serologic centrifuge", "Centrifuge", "Bench 2", "SN-9", "Moved to Bench 2"));
        Assert.True(moved.Succeeded, moved.Error);
        Assert.Equal(("CENT-EDIT", "Serologic centrifuge", "Bench 2", "SN-9"), (moved.Value!.Code, moved.Value.Name, moved.Value.Location, moved.Value.SerialNumber));

        Assert.False((await svc.UpdateCheckAsync(checkId, new UpdateEquipmentCheckRequest("Daily check", 0, null, "x"))).Succeeded);
        Assert.False((await svc.UpdateCheckAsync(checkId, new UpdateEquipmentCheckRequest("Daily check", 7, null, ""))).Succeeded);
        await svc.AddCheckAsync(equipmentId, new SaveEquipmentCheckRequest("Annual calibration", 365, null));
        Assert.False((await svc.UpdateCheckAsync(checkId, new UpdateEquipmentCheckRequest("Annual calibration", 7, null, "rename"))).Succeeded);

        var weekly = await svc.UpdateCheckAsync(checkId, new UpdateEquipmentCheckRequest("Weekly check", 7, "Per SOP rev 3", "SOP revised to weekly"));
        Assert.True(weekly.Succeeded, weekly.Error);
        var check = weekly.Value!.Checks.Single(x => x.CheckId == checkId);
        Assert.Equal(("Weekly check", 7, DateOnly.FromDateTime(_factory.Clock.UtcNow).AddDays(7)), (check.Name, check.IntervalDays, check.DueOn!.Value));
        Assert.Single(await svc.ListRecordsAsync(equipmentId));

        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(Equipment) && e.EntityId == equipmentId && e.EventType == AuditEventType.Update && e.Reason == "Moved to Bench 2"));
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(EquipmentCheck) && e.EntityId == checkId && e.Reason == "SOP revised to weekly"));
    }

    // TEST-BB-989: retired equipment leaves the register and the due list (listed again when retired equipment is
    // asked for) and is restored with a reason; retiring or restoring without a reason, or twice, is refused.
    [Fact]
    public async Task Retire_AndRestore_WithAReason()
    {
        await using var c = _factory.Create();
        var svc = Service(c);
        var (equipmentId, checkId) = await RegisterAsync(c, "TB-RET");

        Assert.False((await svc.DeactivateAsync(equipmentId, " ")).Succeeded);
        Assert.True((await svc.DeactivateAsync(equipmentId, "Replaced by TB-02")).Succeeded);
        Assert.False((await svc.DeactivateAsync(equipmentId, "again")).Succeeded);
        Assert.DoesNotContain(await svc.ListAsync(), e => e.Id == equipmentId);
        Assert.DoesNotContain(await svc.ListDueAsync(), d => d.EquipmentId == equipmentId);
        Assert.False(Assert.Single(await svc.ListAsync(includeInactive: true), e => e.Id == equipmentId).IsActive);

        Assert.False((await svc.RestoreAsync(equipmentId, "")).Succeeded);
        var restored = await svc.RestoreAsync(equipmentId, "Retired in error");
        Assert.True(restored.Succeeded, restored.Error);
        Assert.True(restored.Value!.IsActive);
        Assert.Contains(await svc.ListDueAsync(), d => d.CheckId == checkId);
        Assert.False((await svc.RestoreAsync(equipmentId, "again")).Succeeded);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(Equipment) && e.EntityId == equipmentId && e.EventType == AuditEventType.Activate && e.Reason == "Retired in error"));
    }
}
