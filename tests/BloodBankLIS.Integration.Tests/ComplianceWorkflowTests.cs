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
            new EfRepository<Patient>(c),
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
    public async Task FindByRecipient_ReturnsIssuedUnitsRelatedComponentsAndCoRecipients()
    {
        const string sharedDin = "W000022222222";
        const string soloDin = "W000033333333";
        long indexPatientId;
        long coPatientId;
        long sharedIssuedId;
        long relatedAvailableId;
        long coIssuedId;
        long soloIssuedId;

        await using (var c = _factory.Create())
        {
            var productType = new ProductType
            {
                ProductCode = "RBC-TRACE",
                Name = "Traceback RBC",
                ComponentClass = ComponentClass.RedBloodCells
            };
            c.ProductTypes.Add(productType);
            await c.SaveChangesAsync();

            var index = new Patient
            {
                MedicalRecordNumber = "MRN-TRACE-A",
                LastName = "Index",
                FirstName = "Recipient",
                DateOfBirth = new DateOnly(1975, 3, 3)
            };
            var coRecipient = new Patient
            {
                MedicalRecordNumber = "MRN-TRACE-B",
                LastName = "Cohort",
                FirstName = "Recipient",
                DateOfBirth = new DateOnly(1982, 6, 6)
            };
            c.Patients.AddRange(index, coRecipient);

            var sharedIssued = new BloodUnit
            {
                UnitNumber = "U-TR-A",
                ProductTypeId = productType.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Din = sharedDin,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Transfused
            };
            var relatedAvailable = new BloodUnit
            {
                UnitNumber = "U-TR-AVAIL",
                ProductTypeId = productType.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Din = sharedDin,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Available
            };
            var coIssued = new BloodUnit
            {
                UnitNumber = "U-TR-B",
                ProductTypeId = productType.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Din = sharedDin,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Transfused
            };
            var soloIssued = new BloodUnit
            {
                UnitNumber = "U-TR-SOLO",
                ProductTypeId = productType.Id,
                Abo = AboGroup.A,
                RhD = RhType.Negative,
                Din = soloDin,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
                Status = UnitStatus.Issued
            };
            c.BloodUnits.AddRange(sharedIssued, relatedAvailable, coIssued, soloIssued);
            await c.SaveChangesAsync();

            indexPatientId = index.Id;
            coPatientId = coRecipient.Id;
            sharedIssuedId = sharedIssued.Id;
            relatedAvailableId = relatedAvailable.Id;
            coIssuedId = coIssued.Id;
            soloIssuedId = soloIssued.Id;

            c.Issues.AddRange(
                new Issue
                {
                    BloodProductId = sharedIssued.Id,
                    PatientId = index.Id,
                    IssuedUtc = _factory.Clock.UtcNow.AddDays(-2),
                    IssuedBy = "tech-test",
                    IssuedToLocation = "ICU",
                    Status = IssueStatus.Transfused
                },
                new Issue
                {
                    BloodProductId = soloIssued.Id,
                    PatientId = index.Id,
                    IssuedUtc = _factory.Clock.UtcNow.AddHours(-6),
                    IssuedBy = "tech-test",
                    Status = IssueStatus.Issued
                },
                new Issue
                {
                    BloodProductId = coIssued.Id,
                    PatientId = coRecipient.Id,
                    IssuedUtc = _factory.Clock.UtcNow.AddDays(-1),
                    IssuedBy = "tech-test",
                    Status = IssueStatus.Transfused
                });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var missing = await Lookback(ctx).FindByRecipientAsync("MRN-UNKNOWN", null);
        Assert.False(missing.Succeeded);
        Assert.Contains("not found", missing.Error, StringComparison.OrdinalIgnoreCase);

        var byMrn = await Lookback(ctx).FindByRecipientAsync("mrn-trace-a", null);
        Assert.True(byMrn.Succeeded);
        var report = byMrn.Value!;
        Assert.Equal(indexPatientId, report.Patient.PatientId);
        Assert.Equal("MRN-TRACE-A", report.Patient.MedicalRecordNumber);
        Assert.Contains(report.Units, u => u.BloodProductId == sharedIssuedId && u.Din == sharedDin);
        Assert.Contains(report.Units, u => u.BloodProductId == soloIssuedId && u.Din == soloDin);
        Assert.DoesNotContain(report.Units, u => u.BloodProductId == coIssuedId);
        Assert.Contains(report.RelatedComponents, r => r.BloodProductId == relatedAvailableId);
        Assert.Contains(report.RelatedComponents, r => r.BloodProductId == coIssuedId);
        Assert.Contains(report.CoRecipients, r => r.PatientId == coPatientId && r.BloodProductId == coIssuedId);

        var byId = await Lookback(ctx).FindByRecipientAsync(null, indexPatientId);
        Assert.True(byId.Succeeded);
        Assert.Equal(2, byId.Value!.Units.Count);
    }

    [Fact]
    public async Task FindByRecipient_FollowsMergedPatientToSurvivor()
    {
        long survivorId;
        await using (var c = _factory.Create())
        {
            var productType = new ProductType
            {
                ProductCode = "RBC-MERGE",
                Name = "Merge traceback RBC",
                ComponentClass = ComponentClass.RedBloodCells
            };
            c.ProductTypes.Add(productType);

            var survivor = new Patient
            {
                MedicalRecordNumber = "MRN-SURVIVOR",
                LastName = "Survivor",
                FirstName = "Patient",
                DateOfBirth = new DateOnly(1990, 1, 1)
            };
            var duplicate = new Patient
            {
                MedicalRecordNumber = "MRN-DUPLICATE",
                LastName = "Duplicate",
                FirstName = "Patient",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Status = PatientStatus.Merged
            };
            c.Patients.AddRange(survivor, duplicate);
            await c.SaveChangesAsync();
            duplicate.MergedIntoPatientId = survivor.Id;
            survivorId = survivor.Id;

            var unit = new BloodUnit
            {
                UnitNumber = "U-MERGE",
                ProductTypeId = productType.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Din = "W000044444444",
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(5),
                Status = UnitStatus.Transfused
            };
            c.BloodUnits.Add(unit);
            await c.SaveChangesAsync();
            c.Issues.Add(new Issue
            {
                BloodProductId = unit.Id,
                PatientId = survivor.Id,
                IssuedUtc = _factory.Clock.UtcNow.AddHours(-3),
                IssuedBy = "tech-test",
                Status = IssueStatus.Transfused
            });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var report = await Lookback(ctx).FindByRecipientAsync("MRN-DUPLICATE", null);
        Assert.True(report.Succeeded);
        Assert.Equal(survivorId, report.Value!.Patient.PatientId);
        Assert.True(report.Value.Patient.ResolvedFromMerge);
        Assert.Single(report.Value.Units);
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
