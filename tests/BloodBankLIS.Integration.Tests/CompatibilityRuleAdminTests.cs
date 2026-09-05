using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class CompatibilityRuleAdminTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public CompatibilityRuleAdminTests(SqliteContextFactory factory) => _factory = factory;

    private CompatibilityRuleAdminService Service(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new CompatibilityRuleAdminService(
            new EfRepository<CompatibilityRuleVersion>(context),
            new EfRepository<CompatibilityRule>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    [Fact]
    public async Task CreateVersion_AndRule_PersistsWithHistory()
    {
        await using var c = _factory.Create();
        var svc = Service(c);

        var created = await svc.CreateVersionAsync(new SaveCompatibilityRuleVersionRequest(
            "TEST-1", "POLICY-1", new DateOnly(2026, 9, 1), "Medical director review of test table."));
        Assert.True(created.Succeeded);
        Assert.False(created.Value!.IsActive);

        var rule = await svc.CreateRuleAsync(created.Value.Id, new SaveCompatibilityRuleRequest(
            AboCompatibilityRule.AboCode,
            ComponentClass.RedBloodCells,
            "ABO",
            "{}",
            "HardStop",
            "Recipient/donor ABO must be compatible."));
        Assert.True(rule.Succeeded);
        Assert.Equal(AboCompatibilityRule.AboCode, rule.Value!.RuleCode);

        var listed = await svc.ListVersionsAsync(true);
        Assert.Contains(listed, v => v.Version == "TEST-1" && v.RuleCount == 1);

        var history = await c.ConfigurationChangeHistory
            .Where(h => h.EntityType == nameof(CompatibilityRuleVersion) && h.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Contains(history, h => h.Action == ConfigChangeAction.Create);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(CompatibilityRuleVersion)
            && a.EntityId == created.Value.Id
            && a.EventType == AuditEventType.Configure));
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(CompatibilityRule)
            && a.EntityId == rule.Value.Id
            && a.EventType == AuditEventType.Configure));
    }

    [Fact]
    public async Task VersionAndRule_CreateAndUpdate_WriteConfigure()
    {
        await using var c = _factory.Create();
        var svc = Service(c);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var created = await svc.CreateVersionAsync(new SaveCompatibilityRuleVersionRequest(
            "RA15-" + suffix, "POLICY-RA15", new DateOnly(2026, 9, 4), "Result-audit compatibility table."));
        Assert.True(created.Succeeded, created.Error ?? created.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var updated = await svc.UpdateVersionAsync(created.Value!.Id, new SaveCompatibilityRuleVersionRequest(
            "RA15-" + suffix, "POLICY-RA15", new DateOnly(2026, 9, 4), "Notes after medical director review.",
            "Director accepted the draft table."));
        Assert.True(updated.Succeeded, updated.Error ?? updated.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var rule = await svc.CreateRuleAsync(created.Value.Id, new SaveCompatibilityRuleRequest(
            AboCompatibilityRule.AboCode,
            ComponentClass.RedBloodCells,
            "ABO",
            "{}",
            "HardStop",
            "Recipient/donor ABO must be compatible."));
        Assert.True(rule.Succeeded, rule.Error ?? rule.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var ruleUpdated = await svc.UpdateRuleAsync(rule.Value!.Id, new SaveCompatibilityRuleRequest(
            AboCompatibilityRule.AboCode,
            ComponentClass.RedBloodCells,
            "ABO",
            "{}",
            "HardStop",
            "ABO must be compatible after type verify."));
        Assert.True(ruleUpdated.Succeeded);

        var versionEvents = await c.AuditEvents
            .Where(a => a.EntityType == nameof(CompatibilityRuleVersion) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, versionEvents.Count(a => a.EventType == AuditEventType.Configure));

        var ruleEvents = await c.AuditEvents
            .Where(a => a.EntityType == nameof(CompatibilityRule) && a.EntityId == rule.Value.Id)
            .ToListAsync();
        Assert.Equal(2, ruleEvents.Count(a => a.EventType == AuditEventType.Configure));
    }

    [Fact]
    public async Task ActivateVersion_RequiresReason_AndDeactivatesPrior()
    {
        await using var c = _factory.Create();
        var svc = Service(c);

        var first = await svc.CreateVersionAsync(new SaveCompatibilityRuleVersionRequest(
            "ACT-1", "P", new DateOnly(2026, 1, 1), "Initial table."));
        var second = await svc.CreateVersionAsync(new SaveCompatibilityRuleVersionRequest(
            "ACT-2", "P", new DateOnly(2026, 9, 1), "Replacement table."));
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);

        var missing = await svc.ActivateVersionAsync(first.Value!.Id, "no");
        Assert.False(missing.Succeeded);
        Assert.Contains(missing.Evaluation!.HardStops, r => r.Code == CompatibilityRuleValidator.ReasonCode);

        var activated = await svc.ActivateVersionAsync(first.Value.Id, "Activate initial compatibility table.");
        Assert.True(activated.Succeeded);
        Assert.True(activated.Value!.IsActive);

        var switched = await svc.ActivateVersionAsync(second.Value!.Id, "Switch to September compatibility table.");
        Assert.True(switched.Succeeded);
        Assert.True(switched.Value!.IsActive);

        var prior = await svc.GetVersionAsync(first.Value.Id);
        Assert.False(prior!.IsActive);
        Assert.NotNull(prior.RetiredDate);
    }

    [Fact]
    public async Task DuplicateRuleCode_IsBlocked()
    {
        await using var c = _factory.Create();
        var svc = Service(c);
        var version = await svc.CreateVersionAsync(new SaveCompatibilityRuleVersionRequest(
            "DUP-1", "P", new DateOnly(2026, 9, 1), "Duplicate rule test."));
        Assert.True(version.Succeeded);

        var first = await svc.CreateRuleAsync(version.Value!.Id, new SaveCompatibilityRuleRequest(
            "ISS-ABO-COMPAT", ComponentClass.RedBloodCells, "ABO", "{}", "HardStop", "ABO"));
        Assert.True(first.Succeeded);

        var dup = await svc.CreateRuleAsync(version.Value.Id, new SaveCompatibilityRuleRequest(
            "iss-abo-compat", ComponentClass.Plasma, "ABO", "{}", "HardStop", "Plasma ABO"));
        Assert.False(dup.Succeeded);
        Assert.Contains(dup.Evaluation!.HardStops, r => r.Code == CompatibilityRuleValidator.RuleCodeDuplicate);
    }

    [Fact]
    public async Task InvalidExpressionJson_IsBlocked()
    {
        await using var c = _factory.Create();
        var svc = Service(c);
        var version = await svc.CreateVersionAsync(new SaveCompatibilityRuleVersionRequest(
            "JSON-1", "P", new DateOnly(2026, 9, 1), "JSON validation test."));
        var bad = await svc.CreateRuleAsync(version.Value!.Id, new SaveCompatibilityRuleRequest(
            "CUSTOM-1", ComponentClass.RedBloodCells, "ABO", "[", "HardStop", "Broken JSON"));
        Assert.False(bad.Succeeded);
        Assert.Contains(bad.Evaluation!.HardStops, r => r.Code == CompatibilityRuleValidator.ExpressionCode);
    }
}
