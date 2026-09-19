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

public class CodedCommentAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public CodedCommentAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private CodedCommentAdminService Comments(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new CodedCommentAdminService(
            new EfRepository<CodedCommentDefinition>(c),
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            permissionEvaluator: permissions);
    }

    private static SaveCodedCommentDefinitionRequest Request(string code, CommentPage page = CommentPage.Specimen) =>
        new(code, "Specimen hemolyzed.", page, 10);

    [Fact]
    public async Task Create_WithoutAdminConfigEdit_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"H{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        var denied = await Comments(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigView))
            .CreateAsync(Request(code));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == CodedCommentAuthorizationRule.CreateCode);
        Assert.False(await c.CodedCommentDefinitions.AnyAsync(e => e.Code == code));

        var allowed = await Comments(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .CreateAsync(Request(code));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(code, allowed.Value!.Code);
        Assert.Equal(CommentPage.Specimen, allowed.Value.Page);
    }

    [Fact]
    public async Task Create_DuplicatePageCode_IsRejected()
    {
        await using var c = _factory.Create();
        var code = $"D{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var first = await Comments(c).CreateAsync(Request(code, CommentPage.Specimen));
        Assert.True(first.Succeeded, first.Error);

        var duplicate = await Comments(c).CreateAsync(Request(code, CommentPage.Specimen));
        Assert.False(duplicate.Succeeded);
        Assert.Contains(duplicate.Evaluation!.HardStops, r => r.Code == "CMT.CODE.DUPLICATE");

        var otherPage = await Comments(c).CreateAsync(Request(code, CommentPage.Patient));
        Assert.True(otherPage.Succeeded, otherPage.Error);
    }

    [Fact]
    public async Task Deactivate_WithoutAdminConfigActivate_IsRejected()
    {
        await using var c = _factory.Create();
        var created = await Comments(c).CreateAsync(Request($"X{Guid.NewGuid():N}"[..8].ToUpperInvariant()));
        Assert.True(created.Succeeded, created.Error);
        Assert.True(created.Value!.IsActive);

        var denied = await Comments(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigEdit))
            .SetActiveAsync(created.Value.Id, false);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == CodedCommentAuthorizationRule.DeactivateCode);
        Assert.True((await c.CodedCommentDefinitions.SingleAsync(e => e.Id == created.Value.Id)).IsActive);

        var allowed = await Comments(c, new FixedPermissionEvaluator(1, PermissionCodes.AdminConfigActivate))
            .SetActiveAsync(created.Value.Id, false);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.False(allowed.Value!.IsActive);
    }
}
