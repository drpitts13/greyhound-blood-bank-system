using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AdminCatalogTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AdminCatalogTests(SqliteContextFactory factory) => _factory = factory;

    private OrderingProviderAdminService CreateProviderService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new OrderingProviderAdminService(
            new EfRepository<OrderingProvider>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private BloodAttributeAdminService CreateBloodAttributeService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new BloodAttributeAdminService(
            new EfRepository<BloodAttributeDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private SpecimenTypeAdminService CreateSpecimenTypeService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new SpecimenTypeAdminService(
            new EfRepository<SpecimenTypeDefinition>(context),
            new EfRepository<TestDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private OrderingLocationAdminService CreateLocationService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new OrderingLocationAdminService(
            new EfRepository<OrderingLocation>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    [Fact]
    public async Task CreateProvider_PersistsAndLists()
    {
        await using var c = _factory.Create();
        var svc = CreateProviderService(c);

        var result = await svc.CreateAsync(new SaveOrderingProviderRequest("PROV-NEW", "Dr. New", null, null));
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal("PROV-NEW", result.Value!.ProviderId);

        var row = await c.OrderingProviders.FirstOrDefaultAsync(p => p.ProviderId == "PROV-NEW");
        Assert.NotNull(row);
        Assert.Equal("Dr. New", row!.Name);
    }

    [Fact]
    public async Task CreateBloodAttribute_DraftActivate_BlocksDuplicateCode()
    {
        await using var c = _factory.Create();
        var svc = CreateBloodAttributeService(c);

        var first = await svc.CreateAsync(new SaveBloodAttributeDefinitionRequest("ZZK", "Test Kell", "anti-ZZK", true, 1, null));
        var second = await svc.CreateAsync(new SaveBloodAttributeDefinitionRequest("ZZK", "Test Kell v2", "anti-ZZK", true, 2, null));
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);

        var activated = await svc.ActivateAsync(first.Value!.Id, "Initial activation");
        Assert.True(activated.Succeeded);

        var dupActivate = await svc.ActivateAsync(second.Value!.Id, "Try duplicate");
        Assert.False(dupActivate.Succeeded);
        Assert.True(dupActivate.Evaluation!.IsHardStopped);
    }

    [Fact]
    public async Task CreateSpecimenType_WithExcludedTest_Activates()
    {
        await using var c = _factory.Create();
        c.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABSC",
            Name = "Antibody Screen",
            ResultValueType = ResultValueType.Coded,
            AllowedResultValues = "Negative\nPositive",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = DateTime.UtcNow,
            Version = 1
        });
        await c.SaveChangesAsync();

        var svc = CreateSpecimenTypeService(c);
        var created = await svc.CreateAsync(new SaveSpecimenTypeDefinitionRequest(
            "PLASMA", "Plasma", ["ABSC"], 3, null));
        Assert.True(created.Succeeded);

        var activated = await svc.ActivateAsync(created.Value!.Id, "Initial");
        Assert.True(activated.Succeeded);
        Assert.Contains("ABSC", activated.Value!.ExcludedTestCodes);
    }

    [Fact]
    public async Task CreateLocation_CodeOnly_UsesCodeAsName()
    {
        await using var c = _factory.Create();
        var svc = CreateLocationService(c);

        var result = await svc.CreateAsync(new SaveOrderingLocationRequest("WARD-9", null, "Med/Surg", null));
        Assert.True(result.Succeeded);

        var row = await c.OrderingLocations.FirstAsync(l => l.Code == "WARD-9");
        Assert.Equal("WARD-9", row.Name);
        Assert.Equal("Med/Surg", row.Department);
    }

    private ChargeCodeAdminService CreateChargeCodeService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new ChargeCodeAdminService(
            new EfRepository<ChargeCode>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    private ChargeRuleAdminService CreateChargeRuleService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env);
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env);
        return new ChargeRuleAdminService(
            new EfRepository<ChargeRule>(context),
            new EfRepository<ChargeCode>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    [Fact]
    public async Task CreateChargeCode_PersistsListsAndWritesHistory()
    {
        long id;
        await using (var c = _factory.Create())
        {
            var svc = CreateChargeCodeService(c);
            var result = await svc.CreateAsync(new SaveChargeCodeRequest("bb-new", "New charge", 12.5m, "86999"));
            Assert.True(result.Succeeded);
            Assert.Equal("BB-NEW", result.Value!.Code);
            Assert.Equal(12.5m, result.Value.DefaultAmount);
            Assert.True(result.Value.IsActive);
            id = result.Value.Id;
        }

        await using (var verify = _factory.Create())
        {
            var row = await verify.ChargeCodes.SingleAsync(c => c.Id == id);
            Assert.Equal("BB-NEW", row.Code);
            Assert.Equal("86999", row.CptCode);

            var history = await verify.ConfigurationChangeHistory
                .SingleAsync(h => h.EntityType == nameof(ChargeCode) && h.EntityId == id);
            Assert.Equal(ConfigChangeAction.Create, history.Action);
            Assert.Contains("BB-NEW", history.NewValueJson!);

            var listed = await CreateChargeCodeService(verify).ListAsync(includeInactive: false);
            Assert.Contains(listed, c => c.Code == "BB-NEW");
        }
    }

    [Fact]
    public async Task CreateChargeCode_BlocksDuplicateAndNegativeAmount()
    {
        await using var c = _factory.Create();
        var svc = CreateChargeCodeService(c);

        var first = await svc.CreateAsync(new SaveChargeCodeRequest("BB-DUP", "One", 1m, null));
        Assert.True(first.Succeeded);

        var dup = await svc.CreateAsync(new SaveChargeCodeRequest("bb-dup", "Two", 2m, null));
        Assert.False(dup.Succeeded);
        Assert.True(dup.Evaluation!.IsHardStopped);
        Assert.Contains(dup.Evaluation.HardStops, r => r.Code == "CHARGE.CODE.DUPLICATE");

        var negative = await svc.CreateAsync(new SaveChargeCodeRequest("BB-NEG", "Neg", -5m, null));
        Assert.False(negative.Succeeded);
        Assert.Contains(negative.Evaluation!.HardStops, r => r.Code == "CHARGE.AMOUNT.NEGATIVE");
    }

    [Fact]
    public async Task ChargeCode_DeactivateAndActivate()
    {
        await using var c = _factory.Create();
        var svc = CreateChargeCodeService(c);
        var created = await svc.CreateAsync(new SaveChargeCodeRequest("BB-OFF", "Off", 1m, null));
        Assert.True(created.Succeeded);

        var off = await svc.SetActiveAsync(created.Value!.Id, false);
        Assert.True(off.Succeeded);
        Assert.False(off.Value!.IsActive);

        var activeOnly = await svc.ListAsync(includeInactive: false);
        Assert.DoesNotContain(activeOnly, x => x.Code == "BB-OFF");

        var on = await svc.SetActiveAsync(created.Value.Id, true);
        Assert.True(on.Succeeded);
        Assert.True(on.Value!.IsActive);
    }

    [Fact]
    public async Task CreateChargeRule_MapsCodeAndBlocksMissingOrDuplicate()
    {
        await using var c = _factory.Create();
        var codes = CreateChargeCodeService(c);
        var rules = CreateChargeRuleService(c);

        var code = await codes.CreateAsync(new SaveChargeCodeRequest("BB-RULE", "Rule charge", 10m, null));
        Assert.True(code.Succeeded);

        var missing = await rules.CreateAsync(new SaveChargeRuleRequest(BillingTriggerType.TestVerified, "ABORH", 99999));
        Assert.False(missing.Succeeded);
        Assert.Contains(missing.Evaluation!.HardStops, r => r.Code == "CHARGE.RULE.CODE.REQUIRED");

        var created = await rules.CreateAsync(new SaveChargeRuleRequest(BillingTriggerType.TestVerified, "ABORH", code.Value!.Id));
        Assert.True(created.Succeeded);
        Assert.Equal("BB-RULE", created.Value!.ChargeCode);
        Assert.Equal("ABORH", created.Value.TriggerKey);

        var dup = await rules.CreateAsync(new SaveChargeRuleRequest(BillingTriggerType.TestVerified, "ABORH", code.Value.Id));
        Assert.False(dup.Succeeded);
        Assert.Contains(dup.Evaluation!.HardStops, r => r.Code == "CHARGE.RULE.DUPLICATE");

        var catchAll = await rules.CreateAsync(new SaveChargeRuleRequest(BillingTriggerType.TestVerified, null, code.Value.Id));
        Assert.True(catchAll.Succeeded);
        Assert.Null(catchAll.Value!.TriggerKey);

        var history = await c.ConfigurationChangeHistory
            .Where(h => h.EntityType == nameof(ChargeRule) && h.EntityId == created.Value.Id)
            .SingleAsync();
        Assert.Equal(ConfigChangeAction.Create, history.Action);
    }
}
