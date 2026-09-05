using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class Hl7EndpointAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public Hl7EndpointAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private Hl7ConfigAdminService Endpoints(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new Hl7ConfigAdminService(
            new EfRepository<InterfaceEndpoint>(c),
            new InterfaceFieldMappingRepository(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveHl7EndpointRequest Request(string name) =>
        new(
            name,
            InterfaceType.Results,
            Hl7Direction.Inbound,
            InterfaceTransport.File,
            null,
            null,
            $@"C:\hl7\{name}",
            "ORU",
            null,
            null,
            InterfaceMappingMode.Custom,
            "Test",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            [],
            "Catalog.");

    [Fact]
    public async Task Create_WithoutAdminHl7Manage_IsRejected()
    {
        await using var c = _factory.Create();
        var name = $"ORU-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var denied = await Endpoints(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(name));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == Hl7EndpointAuthorizationRule.CreateCode);
        Assert.False(await c.InterfaceEndpoints.AnyAsync(e => e.Name == name));

        var allowed = await Endpoints(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminHl7Manage))
            .CreateAsync(Request(name));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(name, allowed.Value!.Name);
        Assert.Equal(InterfaceType.Results, allowed.Value.InterfaceType);
    }

    [Fact]
    public async Task Enable_WithoutAdminHl7Manage_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Endpoints(c).CreateAsync(Request($"ORU-{Guid.NewGuid():N}"[..12].ToUpperInvariant()));
        Assert.True(created.Succeeded, created.Error);

        var denied = await Endpoints(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .SetEnabledAsync(created.Value!.Id, true, "Go live.");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == Hl7EndpointAuthorizationRule.EnableCode);
        Assert.False((await c.InterfaceEndpoints.SingleAsync(e => e.Id == created.Value.Id)).IsEnabled);

        var allowed = await Endpoints(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminHl7Manage))
            .SetEnabledAsync(created.Value.Id, true, "Go live.");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.Value!.IsEnabled);
    }
}
