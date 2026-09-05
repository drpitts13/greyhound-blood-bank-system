using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ComponentIdentityCorrectionServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ComponentIdentityCorrectionServiceTests(SqliteContextFactory factory) => _factory = factory;

    private ComponentIdentityCorrectionService CreateService(
        BloodBankDbContext context,
        IPermissionEvaluator? permissions = null)
    {
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        return new ComponentIdentityCorrectionService(
            new InventoryRepository(context),
            new EfRepository<BloodComponentIdentityCorrection>(context),
            lookups,
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser),
            permissions: permissions);
    }

    [Fact]
    public async Task Correct_WithoutInventoryCorrectIdentity_IsHardStopped()
    {
        await using var context = _factory.Create();
        var product = new ProductType { ProductCode = "RBC-ID-PERM", Name = "RBC" };
        context.ProductTypes.Add(product);
        await context.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = "U-ID-PERM",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            AboRhdCode = "DEMO",
            Status = UnitStatus.Available,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(30)
        };
        context.BloodUnits.Add(unit);
        await context.SaveChangesAsync();

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryModify))
            .CorrectAsync(new CorrectIdentityRequest(unit.Id, "AboRhdCode", "DEMN", "Misread label"));
        Assert.False(denied.Succeeded);
        Assert.Equal(InventoryAuthorizationRule.EvaluateCorrectIdentity(false).Message, denied.Error);
        Assert.Equal("DEMO", (await context.BloodUnits.FindAsync(unit.Id))!.AboRhdCode);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryCorrectIdentity))
            .CorrectAsync(new CorrectIdentityRequest(unit.Id, "AboRhdCode", "DEMN", "Misread label"));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("DEMN", allowed.Value!.CorrectedValue);
        Assert.Equal("DEMN", (await context.BloodUnits.FindAsync(unit.Id))!.AboRhdCode);
    }

    [Fact]
    public async Task Correct_WritesCorrectWithCorrectionId()
    {
        await using var context = _factory.Create();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var product = new ProductType { ProductCode = $"RBC-ID-AUD-{suffix}", Name = "RBC" };
        context.ProductTypes.Add(product);
        await context.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = $"U-ID-AUD-{suffix}",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            AboRhdCode = "DEMO",
            Status = UnitStatus.Available,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(30)
        };
        context.BloodUnits.Add(unit);
        await context.SaveChangesAsync();

        var corrected = await CreateService(context)
            .CorrectAsync(new CorrectIdentityRequest(unit.Id, "AboRhdCode", "DEMN", "Misread label"));
        Assert.True(corrected.Succeeded, corrected.Error);
        Assert.True(corrected.Value!.Id > 0);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Correct
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == unit.Id
            && e.Reason == "Misread label"
            && e.OldValueJson is not null
            && e.OldValueJson.Contains($"\"CorrectionId\":{corrected.Value.Id}")
            && e.OldValueJson.Contains("\"Value\":\"DEMO\"")
            && e.NewValueJson is not null
            && e.NewValueJson.Contains($"\"CorrectionId\":{corrected.Value.Id}")
            && e.NewValueJson.Contains("\"Value\":\"DEMN\""));
    }
}
