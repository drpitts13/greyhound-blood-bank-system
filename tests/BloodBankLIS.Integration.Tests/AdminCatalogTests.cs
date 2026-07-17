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
}
