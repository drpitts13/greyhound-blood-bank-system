using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ComplianceWorkflowTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ComplianceWorkflowTests(SqliteContextFactory factory) => _factory = factory;

    private LookbackService Lookback(BloodBankDbContext c)
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
            new EfRepository<LookbackNotification>(c),
            inventory,
            c, _factory.Clock, _factory.CurrentUser, audit);
    }

    [Fact]
    public async Task RecallByDin_RecallsNonTransfusedAndListsRecipients()
    {
        const string din = "W000011234567";
        long availableId;
        long transfusedId;
        await using (var c = _factory.Create())
        {
            var productType = new ProductType
            {
                ProductCode = "RBC-LB",
                Name = "Lookback RBC",
                ComponentClass = ComponentClass.RedBloodCells
            };
            c.ProductTypes.Add(productType);
            await c.SaveChangesAsync();

            var available = new BloodUnit
            {
                UnitNumber = "U-LB-A",
                ProductTypeId = productType.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Din = din,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Available
            };
            var transfused = new BloodUnit
            {
                UnitNumber = "U-LB-T",
                ProductTypeId = productType.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Din = din,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Transfused
            };
            var patient = new Patient
            {
                MedicalRecordNumber = "MRN-LB",
                LastName = "Lookback",
                FirstName = "Recipient",
                DateOfBirth = new DateOnly(1980, 1, 1)
            };
            c.Patients.Add(patient);
            c.BloodUnits.AddRange(available, transfused);
            await c.SaveChangesAsync();
            availableId = available.Id;
            transfusedId = transfused.Id;

            c.Issues.Add(new Issue
            {
                BloodProductId = transfused.Id,
                PatientId = patient.Id,
                IssuedUtc = _factory.Clock.UtcNow.AddHours(-2),
                IssuedBy = "tech-test",
                Status = IssueStatus.Transfused
            });
            await c.SaveChangesAsync();
        }

        await using (var c = _factory.Create())
        {
            var recalled = await Lookback(c).RecallByDinAsync(din, "Donor subsequently reactive");
            Assert.True(recalled.Succeeded);
            Assert.Contains(recalled.Value!.Units, u => u.BloodProductId == availableId);
            Assert.Contains(recalled.Value.Notifications, n => n.BloodProductId == transfusedId);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Recalled, (await verify.BloodUnits.FindAsync(availableId))!.Status);
        Assert.Equal(UnitStatus.Transfused, (await verify.BloodUnits.FindAsync(transfusedId))!.Status);
    }

    [Fact]
    public async Task Deviation_CreateAndClose_Persists()
    {
        await using var c = _factory.Create();
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        var service = new DeviationService(new EfRepository<Deviation>(c), c, _factory.Clock, _factory.CurrentUser, audit);
        var created = await service.CreateAsync(new CreateDeviationRequest("QC miss", "Daily QC not documented", DeviationSeverity.Major, "TestResult", 1));
        Assert.True(created.Succeeded);
        var updated = await service.UpdateStatusAsync(created.Value!.Id, DeviationStatus.Closed, "Retrained staff");
        Assert.True(updated.Succeeded);
        Assert.Equal(DeviationStatus.Closed, updated.Value!.Status);
    }
}
