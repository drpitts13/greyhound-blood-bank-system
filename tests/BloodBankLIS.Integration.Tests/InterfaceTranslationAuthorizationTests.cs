using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class InterfaceTranslationAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InterfaceTranslationAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private InterfaceTranslationAdminService Translations(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new InterfaceTranslationAdminService(
            new InterfaceValueTranslationRepository(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveInterfaceTranslationsRequest Request(string external) =>
        new(
            [new InterfaceValueTranslationDto("POS", external, InterfaceTranslationDirection.Inbound)],
            "Catalog.");

    [Fact]
    public async Task Replace_WithoutAdminHl7Manage_IsRejected()
    {
        await using var c = _factory.Create();
        var external = $"POS-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var denied = await Translations(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .ReplaceAsync(InterfaceDataItemKeys.ResultValue, Request(external));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InterfaceTranslationAuthorizationRule.ReplaceCode);
        Assert.False(await c.InterfaceValueTranslations.AnyAsync(t =>
            t.DataItemKey == InterfaceDataItemKeys.ResultValue && t.ExternalValue == external));

        var allowed = await Translations(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminHl7Manage))
            .ReplaceAsync(InterfaceDataItemKeys.ResultValue, Request(external));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Contains(allowed.Value!.Rows, r => r.ExternalValue == external);
    }
}
