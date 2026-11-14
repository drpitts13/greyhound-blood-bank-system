using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AdminConfigTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AdminConfigTests(SqliteContextFactory factory) => _factory = factory;

    private TestDefinitionAdminService CreateService(BloodBankDbContext context, IEnvironmentInfo? env = null)
    {
        env ??= new StaticEnvironmentInfo("Development", isDevMode: false);
        var repo = new EfRepository<TestDefinition>(context);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var subtestRepo = new EfRepository<SubtestDefinition>(context);
        var phaseRepo = new EfRepository<PhaseDefinition>(context);
        return new TestDefinitionAdminService(
            repo,
            subtestRepo,
            phaseRepo,
            new EfRepository<BloodAttributeDefinition>(context),
            new EfRepository<SpecimenTypeDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private static SaveTestDefinitionRequest NewRequest(string code, string name = "Test", string? reason = null) =>
        new(code, name, TestCategory.Other, ResultValueType.FreeText, null, null, null, null, null, null,
            SortOrder: 0, Billable: false, ChargeCodeMapping: null, VerificationRequired: true,
            ContributesToAboRhHistory: false, ContributesToAntibodyHistory: false, ContributesToCompatibility: false,
            BloodAttributeScopeCodes: null, BloodAttributeScopeKind: null, ContributesToUnitBloodAttributes: false,
            ChangeReason: reason);

    [Fact]
    public async Task Create_WritesAuditAndHistory_AsDraft()
    {
        long id;
        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var result = await service.CreateAsync(NewRequest("TD-CREATE"));

            Assert.True(result.Succeeded);
            Assert.False(result.Value!.IsActive);
            Assert.True(result.Value.IsDraft);
            id = result.Value.Id;
        }

        await using (var verify = _factory.Create())
        {
            var audit = await verify.AuditEvents
                .Where(a => a.EntityType == nameof(TestDefinition) && a.EntityId == id && a.EventType == AuditEventType.TestChange)
                .ToListAsync();
            Assert.NotEmpty(audit);
            Assert.All(audit, a => Assert.Equal("tech-test", a.UserName));

            var history = await verify.ConfigurationChangeHistory
                .Where(h => h.EntityType == nameof(TestDefinition) && h.EntityId == id)
                .SingleAsync();
            Assert.Equal(ConfigChangeAction.Create, history.Action);
            Assert.Equal(1, history.Version);
            Assert.NotNull(history.NewValueJson);
            Assert.Contains("TD-CREATE", history.NewValueJson!);
        }
    }

    [Fact]
    public async Task DuplicateActiveCode_BlocksActivation()
    {
        await using var context = _factory.Create();
        var service = CreateService(context);

        // Both created as drafts (no active duplicate yet), then activate one and try the other.
        var first = await service.CreateAsync(NewRequest("TD-DUP"));
        var second = await service.CreateAsync(NewRequest("TD-DUP"));
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);

        var activateFirst = await service.ActivateAsync(first.Value!.Id, "initial activation");
        Assert.True(activateFirst.Succeeded);

        var activateSecond = await service.ActivateAsync(second.Value!.Id, "should fail");
        Assert.False(activateSecond.Succeeded);
        Assert.NotNull(activateSecond.Evaluation);
        Assert.True(activateSecond.Evaluation!.IsHardStopped);
    }

    [Fact]
    public async Task EditActiveDefinition_WithoutReason_Fails()
    {
        await using var context = _factory.Create();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRequest("TD-EDIT"));
        await service.ActivateAsync(created.Value!.Id, "activate");

        var update = await service.UpdateAsync(created.Value.Id, NewRequest("TD-EDIT", "Renamed", reason: null));

        Assert.False(update.Succeeded);
        Assert.Contains("reason", update.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EditActiveDefinition_WithReason_BumpsVersion()
    {
        await using var context = _factory.Create();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRequest("TD-VER"));
        await service.ActivateAsync(created.Value!.Id, "activate");

        var update = await service.UpdateAsync(created.Value.Id, NewRequest("TD-VER", "Renamed", reason: "Clinical update"));

        Assert.True(update.Succeeded);
        Assert.Equal(2, update.Value!.Version);
        Assert.Equal(2, await context.AuditEvents.CountAsync(a =>
            a.EntityType == nameof(TestDefinition) && a.EntityId == created.Value.Id && a.EventType == AuditEventType.TestChange));
    }

    [Fact]
    public async Task Deactivate_SetsInactive_AndRecordsHistory()
    {
        long id;
        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var created = await service.CreateAsync(NewRequest("TD-DEACT"));
            await service.ActivateAsync(created.Value!.Id, "activate");
            id = created.Value.Id;

            var deactivate = await service.DeactivateAsync(id, "Retire");
            Assert.True(deactivate.Succeeded);
            Assert.False(deactivate.Value!.IsActive);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.ConfigurationChangeHistory
                .Where(h => h.EntityType == nameof(TestDefinition) && h.EntityId == id && h.Action == ConfigChangeAction.Deactivate)
                .SingleAsync();
            Assert.Equal("Retire", history.ChangeReason);
        }
    }

    [Fact]
    public async Task DevMode_StampsHistoryEnvironmentAndFlag()
    {
        var devEnv = new StaticEnvironmentInfo("Development", isDevMode: true);
        long id;
        await using (var context = _factory.Create())
        {
            var service = CreateService(context, devEnv);
            var created = await service.CreateAsync(NewRequest("TD-DEV"));
            id = created.Value!.Id;
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.ConfigurationChangeHistory
                .Where(h => h.EntityType == nameof(TestDefinition) && h.EntityId == id)
                .SingleAsync();
            Assert.True(history.IsDevMode);
            Assert.Equal("Development", history.Environment);
        }
    }

    [Fact]
    public async Task Clone_CreatesDraftCopy()
    {
        await using var context = _factory.Create();
        var service = CreateService(context);

        var created = await service.CreateAsync(NewRequest("TD-SRC"));
        var clone = await service.CloneAsync(created.Value!.Id, "TD-CLONE");

        Assert.True(clone.Succeeded);
        Assert.Equal("TD-CLONE", clone.Value!.Code);
        Assert.True(clone.Value.IsDraft);
        Assert.False(clone.Value.IsActive);
    }

    // TEST-BB-521: numeric and titer settings round-trip through the admin service, inconsistent limits are refused, and
    // only the part matching the result value type is kept.
    [Fact]
    public async Task NumericAndTiterSettings_RoundTrip_AndOnlyTheMatchingPartIsKept()
    {
        await using var context = _factory.Create();
        var service = CreateService(context);
        var code = $"NUM{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var shape = new TestResultShapeDto("%", 2, 0, 100, 0, 0.3m, null, 5, TiterStartDilution: 1, TiterEndDilution: 64, TiterEndpointMinGrade: "1+");
        var created = await service.CreateAsync(NewRequest(code) with { ResultValueType = ResultValueType.Numeric, ResultShape = shape });
        Assert.True(created.Succeeded, created.Error);
        var dto = created.Value!.ResultShape!;
        Assert.Equal(("%", 2, 100m, 0.3m, 5m), (dto.ResultUnits, dto.DecimalPlaces, dto.ReportableHigh, dto.ReferenceHigh, dto.CriticalHigh));
        Assert.Null(dto.TiterStartDilution);

        var bad = await service.UpdateAsync(created.Value.Id, NewRequest(code, reason: "Tighten") with
        {
            ResultValueType = ResultValueType.Numeric, ResultShape = shape with { CriticalHigh = 0.1m }
        });
        Assert.False(bad.Succeeded);

        var titer = await service.UpdateAsync(created.Value.Id, NewRequest(code, reason: "Now a titer") with
        {
            ResultValueType = ResultValueType.Titer, ResultShape = shape with { TiterScored = true }
        });
        Assert.True(titer.Succeeded, titer.Error);
        var saved = await context.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == created.Value.Id);
        Assert.Equal((1, 64, "1+", true), (saved.TiterStartDilution, saved.TiterEndDilution, saved.TiterEndpointMinGrade, saved.TiterScored));
        Assert.Null(saved.ResultUnits);
        Assert.Null(saved.CriticalHigh);
    }
}
