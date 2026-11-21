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

// TEST-BB-391 (OCD-055): an authorizer not in the directory can be added as an outside provider, with a reason,
// by a user holding provider.outside.add; later overrides they authorize then match the directory.
public class OutsideProviderTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private OrderingProviderAdminService Providers(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new OrderingProviderAdminService(
            new EfRepository<OrderingProvider>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static FixedPermissionEvaluator CanAdd() => new(1, PermissionCodes.ProviderOutsideAdd);

    [Fact]
    public async Task Add_WithPermissionAndReason_CreatesAnOutsideProvider_WithAnAssignedId_AndAuditsTheReason()
    {
        await using var c = _factory.Create();
        var added = await Providers(c, CanAdd()).AddOutsideAsync(
            new AddOutsideProviderRequest("  Okafor,  Chidi ", null, "Transferring trauma surgeon, County General"));
        Assert.True(added.Succeeded, added.Error);
        Assert.Equal("OUT-0001", added.Value!.ProviderId);
        Assert.Equal(OutsideProviderRule.SourceSystem, added.Value.SourceSystem);
        Assert.True(added.Value.IsActive);

        var second = await Providers(c, CanAdd()).AddOutsideAsync(
            new AddOutsideProviderRequest("Lindqvist, Maja", "1234567890", "Visiting anesthesiologist on call"));
        Assert.True(second.Succeeded, second.Error);
        Assert.Equal("1234567890", second.Value!.ProviderId);

        await using var verify = _factory.Create();
        var audit = await verify.AuditEvents.SingleAsync(a => a.EntityType == "OrderingProvider" && a.EntityId == added.Value.Id && a.EventType == AuditEventType.Configure);
        Assert.Contains("County General", audit.Reason);
        Assert.True(await verify.ConfigurationChangeHistory.AnyAsync(h => h.EntityType == "OrderingProvider" && h.EntityId == added.Value.Id));
    }

    [Fact]
    public async Task Add_WithoutPermission_IsRefused_AndNothingIsAdded()
    {
        await using var c = _factory.Create();
        var denied = await Providers(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate))
            .AddOutsideAsync(new AddOutsideProviderRequest("Okafor, Chidi", null, "Transferring trauma surgeon"));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, h => h.Code == OutsideProviderRule.PermissionCode);
        Assert.False(await c.OrderingProviders.AnyAsync());
    }

    [Fact]
    public async Task Add_WithoutReason_WithoutName_OrAlreadyListed_IsRefused()
    {
        await using (var seed = _factory.Create())
        {
            seed.OrderingProviders.Add(new OrderingProvider { ProviderId = "P100", Name = "Rivera, Ana", IsActive = true });
            await seed.SaveChangesAsync();
        }

        await using var c = _factory.Create();
        var noReason = await Providers(c, CanAdd()).AddOutsideAsync(new AddOutsideProviderRequest("Okafor, Chidi", null, "short"));
        Assert.Contains(noReason.Evaluation!.HardStops, h => h.Code == OutsideProviderRule.ReasonCode);

        var noName = await Providers(c, CanAdd()).AddOutsideAsync(new AddOutsideProviderRequest(" ", null, "Transferring trauma surgeon"));
        Assert.Contains(noName.Evaluation!.HardStops, h => h.Code == OutsideProviderRule.NameCode);

        var listed = await Providers(c, CanAdd()).AddOutsideAsync(new AddOutsideProviderRequest("rivera,  ana", null, "Transferring trauma surgeon"));
        Assert.Contains(listed.Evaluation!.HardStops, h => h.Code == OutsideProviderRule.AlreadyListedCode);

        var duplicateId = await Providers(c, CanAdd()).AddOutsideAsync(new AddOutsideProviderRequest("Okafor, Chidi", "P100", "Transferring trauma surgeon"));
        Assert.Contains(duplicateId.Evaluation!.HardStops, h => h.Code == "PROVIDER.ID.DUPLICATE");

        Assert.Equal(1, await c.OrderingProviders.CountAsync());
    }

    [Fact]
    public async Task AnOverrideAuthorizedByTheAddedProvider_IsRecordedAsOutsideProvider()
    {
        await using (var c = _factory.Create())
        {
            Assert.True((await Providers(c, CanAdd()).AddOutsideAsync(
                new AddOutsideProviderRequest("Okafor, Chidi", null, "Transferring trauma surgeon"))).Succeeded);
        }

        await using var db = _factory.Create();
        var row = new Override
        {
            Action = OverrideAction.EmergencyRelease,
            ContextType = nameof(Issue),
            ContextId = 1,
            RuleCode = "ISS-TEST",
            Reason = "Trauma bay",
            AuthorizedBy = "Okafor, Chidi (OUT-0001)",
            OverriddenUtc = _factory.Clock.UtcNow
        };
        db.Overrides.Add(row);
        await db.SaveChangesAsync();
        var saved = await db.Overrides.AsNoTracking().SingleAsync(o => o.Id == row.Id);
        Assert.Equal(AuthorizerDirectoryMatch.OutsideProvider, saved.AuthorizerMatch);
        Assert.Equal("OUT-0001", saved.AuthorizerReference);
    }
}
