using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class InterfaceTransfusionAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InterfaceTransfusionAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private InterfaceTransfusionService Service(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        return new InterfaceTransfusionService(
            new EfRepository<Patient>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<Issue>(c),
            new EfRepository<TransfusionEvent>(c),
            new InventoryRepository(c),
            new ReactionInvestigationService(
                new EfRepository<ReactionInvestigation>(c),
                new EfRepository<TransfusionEvent>(c),
                new InventoryRepository(c),
                c,
                _factory.Clock,
                _factory.CurrentUser,
                audit),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            permissions: permissions);
    }

    private static InterfaceTransfusionRequest DummyRequest() =>
        new("MRN-IFACE-PERM", "UNIT-IFACE", null, null, null, null, null, null, false);

    [Fact]
    public async Task DocumentAsync_WithoutTransfusionDocument_IsRejected()
    {
        await using var c = _factory.Create();
        var request = DummyRequest();

        var denied = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate))
            .DocumentAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains("transfusion.document", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await c.TransfusionEvents.AnyAsync());

        var allowed = await Service(c, new FixedPermissionEvaluator(1, PermissionCodes.TransfusionDocument))
            .DocumentAsync(request);
        Assert.False(allowed.Succeeded);
        Assert.DoesNotContain("transfusion.document", allowed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No patient found", allowed.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DocumentFromHl7Async_DoesNotRequireTransfusionDocument()
    {
        await using var c = _factory.Create();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate))
                .DocumentFromHl7Async(DummyRequest()));
        Assert.Contains("No patient found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
