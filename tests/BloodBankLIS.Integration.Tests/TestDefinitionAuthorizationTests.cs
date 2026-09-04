using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class TestDefinitionAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public TestDefinitionAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private TestDefinitionAdminService Tests(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new TestDefinitionAdminService(
            new EfRepository<TestDefinition>(c),
            new EfRepository<SubtestDefinition>(c),
            new EfRepository<PhaseDefinition>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new EfRepository<SpecimenTypeDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveTestDefinitionRequest Request(string code) =>
        new(code, "Temp test", TestCategory.Other, ResultValueType.FreeText, null, null, null, null, null, null,
            SortOrder: 0, Billable: false, ChargeCodeMapping: null, VerificationRequired: true,
            ContributesToAboRhHistory: false, ContributesToAntibodyHistory: false, ContributesToCompatibility: false,
            BloodAttributeScopeCodes: null, BloodAttributeScopeKind: null, ContributesToUnitBloodAttributes: false,
            ChangeReason: "Catalog.");

    [Fact]
    public async Task Create_WithoutAdminTestsManage_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"TD-{Guid.NewGuid():N}"[..12];

        var denied = await Tests(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == TestCatalogAuthorizationRule.CreateCode);
        Assert.False(await c.TestDefinitions.AnyAsync(t => t.Code == code));

        var allowed = await Tests(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminTestsManage))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.Code, ignoreCase: true);
    }

    [Fact]
    public async Task Update_WithoutAdminTestsManage_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Tests(c).CreateAsync(Request($"TD-{Guid.NewGuid():N}"[..12]));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Tests(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .UpdateAsync(created.Value!.Id, Request(created.Value.Code) with { Name = "Renamed" });
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == TestCatalogAuthorizationRule.UpdateCode);
        Assert.Equal("Temp test", (await c.TestDefinitions.SingleAsync(t => t.Id == created.Value.Id)).Name);
    }
}
