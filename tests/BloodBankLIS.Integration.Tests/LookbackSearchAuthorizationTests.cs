using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class LookbackSearchAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public LookbackSearchAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private LookbackService Lookback(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        var inventory = new InventoryService(
            new InventoryRepository(c),
            new EfRepository<UnitBloodAttribute>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            new IsbtLookupCatalog(
                new EfRepository<IsbtAboRhdCode>(c),
                new EfRepository<IsbtProductCode>(c)),
            c, _factory.Clock, _factory.CurrentUser, audit);
        return new LookbackService(
            new InventoryRepository(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<UnitModificationUnit>(c),
            new EfRepository<Issue>(c),
            new EfRepository<TransfusionEvent>(c),
            new EfRepository<Patient>(c),
            new EfRepository<LookbackNotification>(c),
            inventory,
            c, _factory.Clock, _factory.CurrentUser, audit,
            permissions);
    }

    [Fact]
    public async Task FindByDin_WithoutLookbackManage_IsRejected()
    {
        await using var c = _factory.Create();
        const string din = "W000077777777";
        c.ProductTypes.Add(new ProductType
        {
            ProductCode = "RBC-LB-FIND",
            Name = "Lookback find RBC",
            ComponentClass = ComponentClass.RedBloodCells
        });
        await c.SaveChangesAsync();
        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-LB-FIND",
            ProductTypeId = c.ProductTypes.Single().Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Din = din,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        });
        await c.SaveChangesAsync();
        var lookbackAuditsBeforeDeny = await c.AuditEvents.CountAsync(a => a.EventType == AuditEventType.Lookback);

        var denied = await Lookback(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .FindByDinAsync(din);
        Assert.False(denied.Succeeded);
        Assert.Contains("lookback.manage", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(lookbackAuditsBeforeDeny, await c.AuditEvents.CountAsync(a => a.EventType == AuditEventType.Lookback));

        var allowed = await Lookback(c, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .FindByDinAsync(din);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(din, allowed.Value!.Din);
        Assert.Contains(allowed.Value.Units, u => u.UnitNumber == "U-LB-FIND");
    }

    [Fact]
    public async Task FindByRecipient_WithoutLookbackManage_IsRejected()
    {
        await using var c = _factory.Create();
        c.Patients.Add(new Patient
        {
            MedicalRecordNumber = "MRN-LB-TRACE",
            LastName = "Trace",
            FirstName = "Denied",
            DateOfBirth = new DateOnly(1980, 1, 1)
        });
        await c.SaveChangesAsync();
        var lookbackAuditsBeforeDeny = await c.AuditEvents.CountAsync(a => a.EventType == AuditEventType.Lookback);

        var denied = await Lookback(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .FindByRecipientAsync("MRN-LB-TRACE", null);
        Assert.False(denied.Succeeded);
        Assert.Contains("lookback.manage", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(lookbackAuditsBeforeDeny, await c.AuditEvents.CountAsync(a => a.EventType == AuditEventType.Lookback));

        var allowed = await Lookback(c, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .FindByRecipientAsync("MRN-LB-TRACE", null);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("MRN-LB-TRACE", allowed.Value!.Patient.MedicalRecordNumber);
    }
}
