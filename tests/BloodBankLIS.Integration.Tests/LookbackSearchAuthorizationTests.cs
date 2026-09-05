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
        var findProduct = new ProductType
        {
            ProductCode = "RBC-LB-FIND",
            Name = "Lookback find RBC",
            ComponentClass = ComponentClass.RedBloodCells
        };
        c.ProductTypes.Add(findProduct);
        await c.SaveChangesAsync();
        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-LB-FIND",
            ProductTypeId = findProduct.Id,
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

    [Fact]
    public async Task RecallAndRecordAttempt_WriteLookback()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var din = $"W0000{key}";
        var product = new ProductType
        {
            ProductCode = $"RBC-LB-AUD-{key}",
            Name = "Lookback audit RBC",
            ComponentClass = ComponentClass.RedBloodCells
        };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();

        var available = new BloodUnit
        {
            UnitNumber = $"U-LB-AUD-A-{key}",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Din = din,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        var transfused = new BloodUnit
        {
            UnitNumber = $"U-LB-AUD-T-{key}",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Din = din,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Transfused
        };
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-LB-AUD-{key}",
            LastName = "Lookback",
            FirstName = "Audit",
            DateOfBirth = new DateOnly(1980, 1, 1)
        };
        c.Patients.Add(patient);
        c.BloodUnits.AddRange(available, transfused);
        await c.SaveChangesAsync();
        c.Issues.Add(new Issue
        {
            BloodProductId = transfused.Id,
            PatientId = patient.Id,
            IssuedUtc = _factory.Clock.UtcNow.AddHours(-2),
            IssuedBy = "tech-test",
            Status = IssueStatus.Transfused
        });
        await c.SaveChangesAsync();

        var recalled = await Lookback(c).RecallByDinAsync(din, "Donor subsequently reactive");
        Assert.True(recalled.Succeeded, recalled.Error);
        var notification = Assert.Single(recalled.Value!.Notifications);

        var attempted = await Lookback(c).RecordAttemptAsync(
            notification.Id,
            new RecordLookbackAttemptRequest("Dr. Record", "Physician notified", LookbackNotificationStatus.Attempted));
        Assert.True(attempted.Succeeded, attempted.Error);

        var events = c.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Lookback
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId is not null
            && e.Reason == "Donor subsequently reactive");
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Lookback
            && e.EntityType == nameof(LookbackNotification)
            && e.EntityId == notification.Id
            && e.Reason == "Lookback notification attempt recorded."
            && e.OldValueJson is not null
            && e.NewValueJson is not null);
    }
}
