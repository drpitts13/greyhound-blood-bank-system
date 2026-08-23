using BloodBankLIS.Application.Modifications;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class BloodProductModificationServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public BloodProductModificationServiceTests(SqliteContextFactory factory) => _factory = factory;

    private BloodProductModificationService CreateService(BloodBankDbContext context)
    {
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser);
        return new BloodProductModificationService(
            new InventoryRepository(context),
            new EfRepository<ModificationRule>(context),
            new EfRepository<ProductType>(context),
            new EfRepository<ExpirationModificationCode>(context),
            new EfRepository<UnitModification>(context),
            new EfRepository<UnitModificationUnit>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit);
    }

    private async Task<(long SourceProductId, long TargetProductId)> EnsureProductTypesAsync(string suffix)
    {
        await using var context = _factory.Create();
        var source = new ProductType { ProductCode = $"MODSRC-{suffix}", Name = "Source Product" };
        var target = new ProductType { ProductCode = $"MODTGT-{suffix}", Name = "Target Product" };
        context.ProductTypes.AddRange(source, target);
        await context.SaveChangesAsync();
        return (source.Id, target.Id);
    }

    private async Task<long> CreateExpirationCodeAsync(
        string code,
        ExpirationRelativeTo relativeTo = ExpirationRelativeTo.ModificationDateTime)
    {
        if (!ExpirationOffsetCode.TryParse(code, out var offset))
        {
            throw new ArgumentException($"Invalid offset code '{code}'.", nameof(code));
        }

        var storedCode = relativeTo == ExpirationRelativeTo.CollectionDateTime ? $"{code}-COL" : code;
        await using var context = _factory.Create();
        var existing = context.ExpirationModificationCodes.FirstOrDefault(c => c.Code == storedCode);
        if (existing is not null)
        {
            return existing.Id;
        }

        var entity = new ExpirationModificationCode
        {
            Code = storedCode,
            OffsetAmount = offset.Amount,
            OffsetUnit = offset.Unit,
            RelativeTo = relativeTo,
            IsActive = true,
            Version = 1
        };
        context.ExpirationModificationCodes.Add(entity);
        await context.SaveChangesAsync();
        return entity.Id;
    }

    private async Task<long> CreateRuleAsync(
        long sourceProductId,
        long targetProductId,
        ModificationType type,
        string offset = "24H",
        ExpirationRelativeTo relativeTo = ExpirationRelativeTo.ModificationDateTime)
    {
        var codeId = await CreateExpirationCodeAsync(offset, relativeTo);
        await using var context = _factory.Create();
        var rule = new ModificationRule
        {
            ModificationCode = $"T{Guid.NewGuid():N}"[..20],
            SourceProductTypeId = sourceProductId,
            TargetProductTypeId = targetProductId,
            ModificationType = type,
            ExpirationModificationCodeId = codeId,
            IsActive = true,
            Version = 1
        };
        context.ModificationRules.Add(rule);
        await context.SaveChangesAsync();
        return rule.Id;
    }

    private async Task<long> CreateUnitAsync(
        string unitNumber,
        long productTypeId,
        UnitStatus status = UnitStatus.Available,
        AboGroup abo = AboGroup.O,
        RhType rh = RhType.Positive,
        DateTime? expiresUtc = null,
        decimal? volume = null,
        DateTime? collectedUtc = null)
    {
        await using var context = _factory.Create();
        var unit = new BloodUnit
        {
            UnitNumber = unitNumber,
            ProductTypeId = productTypeId,
            Abo = abo,
            RhD = rh,
            ExpiresUtc = expiresUtc ?? _factory.Clock.UtcNow.AddDays(30),
            Status = status,
            Volume = volume,
            CollectedUtc = collectedUtc
        };
        context.BloodUnits.Add(unit);
        await context.SaveChangesAsync();
        return unit.Id;
    }

    [Theory]
    [InlineData(ModificationType.Irradiate)]
    [InlineData(ModificationType.Thaw)]
    [InlineData(ModificationType.VolumeReduction)]
    [InlineData(ModificationType.Leukoreduction)]
    public async Task ApplySingle_HappyPath_RetiresSourceAndCreatesResult(ModificationType type)
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync($"SINGLE-{type}");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, type, "24H");
        var sourceId = await CreateUnitAsync($"U-{type}-SRC", sourceProductId, volume: 300m);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.ApplySingleAsync(sourceId, new PerformSingleModificationRequest(ruleId, null, "Clinical need"));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Modification);
        Assert.Equal(type, result.Modification!.ModificationType);
        var resultUnit = Assert.Single(result.ResultUnits!);
        Assert.Equal(UnitStatus.Quarantine, resultUnit.Status);
        Assert.Equal(targetProductId, resultUnit.ProductTypeId);
        Assert.Equal(_factory.Clock.UtcNow.AddHours(24), resultUnit.ExpiresUtc);

        await using var verify = _factory.Create();
        var source = await verify.BloodUnits.FindAsync(sourceId);
        Assert.Equal(UnitStatus.Modified, source!.Status);

        var auditEvent = await verify.AuditEvents
            .Where(a => a.EntityType == nameof(UnitModification) && a.EntityId == result.Modification.Id && a.EventType == AuditEventType.Modify)
            .SingleAsync();
        Assert.Equal("Clinical need", auditEvent.Reason);
    }

    [Fact]
    public async Task ApplySingle_ExpirationCappedAtSourceExpiry()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("CAP");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Irradiate, "5D");
        var nearExpiry = _factory.Clock.UtcNow.AddHours(6);
        var sourceId = await CreateUnitAsync("U-CAP-SRC", sourceProductId, expiresUtc: nearExpiry);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.ApplySingleAsync(sourceId, new PerformSingleModificationRequest(ruleId, null, "Cap test"));

        Assert.True(result.Succeeded);
        Assert.Equal(nearExpiry, result.ResultUnits!.Single().ExpiresUtc);
    }

    [Fact]
    public async Task ApplySingle_SourceNotAvailable_IsBlocked()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("BADSTATUS");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Irradiate);
        var sourceId = await CreateUnitAsync("U-BADSTATUS-SRC", sourceProductId, status: UnitStatus.Quarantine);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.ApplySingleAsync(sourceId, new PerformSingleModificationRequest(ruleId, null, "attempt"));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Evaluation);
        Assert.True(result.Evaluation!.IsHardStopped);
        Assert.Contains(result.Evaluation.HardStops, r => r.Code == "MOD-STATUS-INVALID");
    }

    [Fact]
    public async Task ApplySingle_ExpiredSource_IsBlocked()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("EXPIRED");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Thaw);
        var sourceId = await CreateUnitAsync("U-EXPIRED-SRC", sourceProductId, expiresUtc: _factory.Clock.UtcNow.AddHours(-1));

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.ApplySingleAsync(sourceId, new PerformSingleModificationRequest(ruleId, null, "attempt"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "MOD-EXPIRED");
    }

    [Fact]
    public async Task ApplySingle_InactiveRule_Fails()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("INACTIVERULE");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Irradiate);
        await using (var context = _factory.Create())
        {
            var rule = await context.ModificationRules.FindAsync(ruleId);
            rule!.IsActive = false;
            await context.SaveChangesAsync();
        }
        var sourceId = await CreateUnitAsync("U-INACTIVERULE-SRC", sourceProductId);

        await using var svcContext = _factory.Create();
        var service = CreateService(svcContext);

        var result = await service.ApplySingleAsync(sourceId, new PerformSingleModificationRequest(ruleId, null, "attempt"));

        Assert.False(result.Succeeded);
        Assert.Contains("inactive", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplySingle_WithoutReason_Fails()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("NOREASON");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Irradiate);
        var sourceId = await CreateUnitAsync("U-NOREASON-SRC", sourceProductId);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.ApplySingleAsync(sourceId, new PerformSingleModificationRequest(ruleId, null, "  "));

        Assert.False(result.Succeeded);
        Assert.Contains("reason", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Divide_HappyPath_CreatesMultipleResultsAndRetiresSource()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("DIVIDE");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Divide, "5D");
        var sourceId = await CreateUnitAsync("U-DIVIDE-SRC", sourceProductId, volume: 300m);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var children = new[]
        {
            new DivideChildSpec("A", 150m),
            new DivideChildSpec("B", 150m)
        };
        var result = await service.DivideAsync(sourceId, new PerformDivideRequest(ruleId, children, "Split for pediatric use"));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.ResultUnits!.Count);
        Assert.Contains(result.ResultUnits, u => u.UnitNumber == "U-DIVIDE-SRC-A");
        Assert.Contains(result.ResultUnits, u => u.UnitNumber == "U-DIVIDE-SRC-B");
        Assert.All(result.ResultUnits, u => Assert.Equal(UnitStatus.Quarantine, u.Status));

        await using var verify = _factory.Create();
        var source = await verify.BloodUnits.FindAsync(sourceId);
        Assert.Equal(UnitStatus.Modified, source!.Status);
    }

    [Fact]
    public async Task Divide_SingleChild_IsBlocked()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("DIVIDE1");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Divide);
        var sourceId = await CreateUnitAsync("U-DIVIDE1-SRC", sourceProductId, volume: 300m);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var children = new[] { new DivideChildSpec("A", 150m) };
        var result = await service.DivideAsync(sourceId, new PerformDivideRequest(ruleId, children, "attempt"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "MOD-DIVIDE-MIN-TARGETS");
    }

    [Fact]
    public async Task Divide_VolumesExceedSource_IsBlocked()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("DIVIDEVOL");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Divide);
        var sourceId = await CreateUnitAsync("U-DIVIDEVOL-SRC", sourceProductId, volume: 200m);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var children = new[] { new DivideChildSpec("A", 150m), new DivideChildSpec("B", 150m) };
        var result = await service.DivideAsync(sourceId, new PerformDivideRequest(ruleId, children, "attempt"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "MOD-VOLUME-EXCEEDS-SOURCE");
    }

    [Fact]
    public async Task Pool_HappyPath_CreatesSingleResultAndRetiresAllSources()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("POOL");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Pool, "24H");
        var source1 = await CreateUnitAsync("U-POOL-1", sourceProductId, volume: 200m);
        var source2 = await CreateUnitAsync("U-POOL-2", sourceProductId, volume: 200m);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.PoolAsync(new PerformPoolRequest(new[] { source1, source2 }, ruleId, "Pooling platelets"));

        Assert.True(result.Succeeded);
        var resultUnit = Assert.Single(result.ResultUnits!);
        Assert.Equal(400m, resultUnit.Volume);
        Assert.Equal(UnitStatus.Quarantine, resultUnit.Status);

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Modified, (await verify.BloodUnits.FindAsync(source1))!.Status);
        Assert.Equal(UnitStatus.Modified, (await verify.BloodUnits.FindAsync(source2))!.Status);

        var links = await verify.UnitModificationUnits.Where(l => l.UnitModificationId == result.Modification!.Id).ToListAsync();
        Assert.Equal(3, links.Count); // 2 sources + 1 result
        Assert.Equal(2, links.Count(l => l.Role == ModificationUnitRole.Source));
        Assert.Single(links.Where(l => l.Role == ModificationUnitRole.Result));
    }

    [Fact]
    public async Task Pool_SingleSource_IsBlocked()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("POOL1");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Pool);
        var source1 = await CreateUnitAsync("U-POOL1-1", sourceProductId);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.PoolAsync(new PerformPoolRequest(new[] { source1 }, ruleId, "attempt"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "MOD-POOL-MIN-SOURCES");
    }

    [Fact]
    public async Task Pool_AboMismatch_IsBlocked()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("POOLABO");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Pool);
        var source1 = await CreateUnitAsync("U-POOLABO-1", sourceProductId, abo: AboGroup.O);
        var source2 = await CreateUnitAsync("U-POOLABO-2", sourceProductId, abo: AboGroup.A);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.PoolAsync(new PerformPoolRequest(new[] { source1, source2 }, ruleId, "attempt"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "MOD-POOL-ABO-MISMATCH");
    }

    [Fact]
    public async Task Pool_RhMismatch_IsBlocked()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("POOLRH");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Pool);
        var source1 = await CreateUnitAsync("U-POOLRH-1", sourceProductId, rh: RhType.Positive);
        var source2 = await CreateUnitAsync("U-POOLRH-2", sourceProductId, rh: RhType.Negative);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.PoolAsync(new PerformPoolRequest(new[] { source1, source2 }, ruleId, "attempt"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "MOD-POOL-ABO-MISMATCH");
    }

    [Fact]
    public async Task Pool_ExpirationUsesEarliestSource()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("POOLEXP");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Pool, "10D");
        var earlyExpiry = _factory.Clock.UtcNow.AddDays(2);
        var source1 = await CreateUnitAsync("U-POOLEXP-1", sourceProductId, expiresUtc: earlyExpiry);
        var source2 = await CreateUnitAsync("U-POOLEXP-2", sourceProductId, expiresUtc: _factory.Clock.UtcNow.AddDays(20));

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.PoolAsync(new PerformPoolRequest(new[] { source1, source2 }, ruleId, "Pool with mismatched expiry"));

        Assert.True(result.Succeeded);
        Assert.Equal(earlyExpiry, result.ResultUnits!.Single().ExpiresUtc);
    }

    [Fact]
    public async Task GetEligibleModifications_ReturnsOnlyActiveRulesForUnitsProduct()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("ELIGIBLE");
        var (otherSourceId, otherTargetId) = await EnsureProductTypesAsync("ELIGIBLE-OTHER");
        var activeRuleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Irradiate);
        await CreateRuleAsync(otherSourceId, otherTargetId, ModificationType.Thaw);
        var unitId = await CreateUnitAsync("U-ELIGIBLE-1", sourceProductId);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var eligible = await service.GetEligibleModificationsAsync(unitId);

        var only = Assert.Single(eligible);
        Assert.Equal(activeRuleId, only.RuleId);
        Assert.Equal(ModificationType.Irradiate, only.ModificationType);
    }

    [Fact]
    public async Task GetHistory_ReturnsModificationForBothSourceAndResultUnits()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("HISTORY");
        var ruleId = await CreateRuleAsync(sourceProductId, targetProductId, ModificationType.Irradiate);
        var sourceId = await CreateUnitAsync("U-HISTORY-SRC", sourceProductId);

        long resultUnitId;
        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var result = await service.ApplySingleAsync(sourceId, new PerformSingleModificationRequest(ruleId, null, "History test"));
            resultUnitId = result.ResultUnits!.Single().Id;
        }

        await using var verifyContext = _factory.Create();
        var verifyService = CreateService(verifyContext);

        var sourceHistory = await verifyService.GetHistoryAsync(sourceId);
        Assert.Single(sourceHistory);

        var resultHistory = await verifyService.GetHistoryAsync(resultUnitId);
        Assert.Single(resultHistory);
        Assert.Equal(sourceHistory[0].Id, resultHistory[0].Id);
    }

    [Fact]
    public async Task ApplySingle_CollectionRelative_DatesFromCollection()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("COLREL");
        var ruleId = await CreateRuleAsync(
            sourceProductId, targetProductId, ModificationType.Leukoreduction, "10D", ExpirationRelativeTo.CollectionDateTime);
        var collected = _factory.Clock.UtcNow.AddDays(-2);
        var sourceId = await CreateUnitAsync(
            "U-COLREL-SRC", sourceProductId, expiresUtc: _factory.Clock.UtcNow.AddDays(40), collectedUtc: collected);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.ApplySingleAsync(sourceId, new PerformSingleModificationRequest(ruleId, null, "Collection dating"));

        Assert.True(result.Succeeded);
        Assert.Equal(collected.AddDays(10), result.ResultUnits!.Single().ExpiresUtc);
    }

    [Fact]
    public async Task ApplySingle_CollectionRelativeMissingCollection_IsBlocked()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("COLMISS");
        var ruleId = await CreateRuleAsync(
            sourceProductId, targetProductId, ModificationType.Leukoreduction, "42D", ExpirationRelativeTo.CollectionDateTime);
        var sourceId = await CreateUnitAsync("U-COLMISS-SRC", sourceProductId);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var result = await service.ApplySingleAsync(sourceId, new PerformSingleModificationRequest(ruleId, null, "Missing collection"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == "MOD-COLLECTION-REQUIRED");
    }

    [Fact]
    public async Task GetEligibleModifications_CollectionRelativeWithoutCollection_IsUnavailable()
    {
        var (sourceProductId, targetProductId) = await EnsureProductTypesAsync("ELIGCOL");
        await CreateRuleAsync(
            sourceProductId, targetProductId, ModificationType.Leukoreduction, "42D", ExpirationRelativeTo.CollectionDateTime);
        var unitId = await CreateUnitAsync("U-ELIGCOL-1", sourceProductId);

        await using var context = _factory.Create();
        var service = CreateService(context);

        var eligible = await service.GetEligibleModificationsAsync(unitId);

        var only = Assert.Single(eligible);
        Assert.False(only.IsAvailable);
        Assert.True(only.RequiresCollectionDate);
        Assert.Null(only.PreviewExpiresUtc);
    }
}
