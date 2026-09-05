using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class SubtestAndGrouperAdminTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SubtestAndGrouperAdminTests(SqliteContextFactory factory) => _factory = factory;

    private SubtestDefinitionAdminService CreateSubtestService(BloodBankDbContext context)
    {
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", false));
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", false));
        return new SubtestDefinitionAdminService(new EfRepository<SubtestDefinition>(context), context, _factory.Clock, _factory.CurrentUser, audit, history);
    }

    private TestGrouperAdminService CreateGrouperService(BloodBankDbContext context)
    {
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", false));
        var history = new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", false));
        return new TestGrouperAdminService(
            new EfRepository<TestGrouper>(context),
            new EfRepository<TestDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            history);
    }

    [Fact]
    public async Task Subtest_CreateAndActivate_Succeeds()
    {
        await using var context = _factory.Create();
        var service = CreateSubtestService(context);
        var created = await service.CreateAsync(new SaveSubtestDefinitionRequest(
            "Anti-A",
            "Anti-A",
            SubtestResultType.GradedReaction,
            SubtestChoiceDefinitions.DefaultGradedReaction()
                .Select(c => new SubtestChoiceDto(c.Code, c.Label, c.Polarity))
                .ToList(),
            null));

        Assert.True(created.Succeeded);
        var activated = await service.ActivateAsync(created.Value!.Id, "seed");
        Assert.True(activated.Succeeded);
        Assert.True(activated.Value!.IsActive);
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(SubtestDefinition) && a.EntityId == created.Value.Id && a.EventType == AuditEventType.TestChange));
    }

    [Fact]
    public async Task Grouper_Activate_RequiresMemberTests()
    {
        await using var context = _factory.Create();
        context.TestDefinitions.Add(new TestDefinition
        {
            Code = "ABORH",
            Name = "ABO/Rh",
            ResultValueType = ResultValueType.AboRh,
            IsActive = true,
            IsDraft = false
        });
        await context.SaveChangesAsync();

        var service = CreateGrouperService(context);
        var created = await service.CreateAsync(new SaveTestGrouperRequest(
            "TNS",
            "Type and Screen",
            [new TestGrouperMemberDto("ABORH", 1)],
            null));

        Assert.True(created.Succeeded);
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(TestGrouper) && a.EntityId == created.Value!.Id && a.EventType == AuditEventType.TestChange));
        var activated = await service.ActivateAsync(created.Value!.Id, "seed");
        Assert.True(activated.Succeeded);
        Assert.Equal("ABORH", activated.Value!.Members[0].TestCode);
    }

    [Fact]
    public async Task Grouper_CreateAndUpdate_WriteTestChange()
    {
        await using var context = _factory.Create();
        var testCode = "ABORH-RA12-" + Guid.NewGuid().ToString("N")[..6];
        context.TestDefinitions.Add(new TestDefinition
        {
            Code = testCode,
            Name = "ABO/Rh",
            ResultValueType = ResultValueType.AboRh,
            IsActive = true,
            IsDraft = false
        });
        await context.SaveChangesAsync();

        var service = CreateGrouperService(context);
        var code = "TG-RA12-" + Guid.NewGuid().ToString("N")[..8];
        var created = await service.CreateAsync(new SaveTestGrouperRequest(
            code,
            "Result-audit grouper",
            [new TestGrouperMemberDto(testCode, 1)],
            "draft grouper"));
        Assert.True(created.Succeeded, created.Error ?? created.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var updated = await service.UpdateAsync(created.Value!.Id, new SaveTestGrouperRequest(
            code,
            "Result-audit grouper renamed",
            [new TestGrouperMemberDto(testCode, 1)],
            "rename grouper"));
        Assert.True(updated.Succeeded);

        var events = await context.AuditEvents
            .Where(a => a.EntityType == nameof(TestGrouper) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, events.Count(a => a.EventType == AuditEventType.TestChange));
    }
}
