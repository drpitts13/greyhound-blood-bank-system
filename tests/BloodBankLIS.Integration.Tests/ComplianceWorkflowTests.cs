using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class ComplianceWorkflowTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public ComplianceWorkflowTests(SqliteContextFactory factory) => _factory = factory;

    private LookbackService Lookback(BloodBankDbContext c, IPermissionEvaluator? permissions = null, bool withLocations = false)
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
            permissions,
            withLocations ? new EfRepository<InventoryLocation>(c) : null);
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
    public async Task RecallByDin_IssuedUnit_OpensOneNotification_AndSecondRecallDoesNotDuplicate()
    {
        const string din = "W000077777777";
        long issuedId;
        long patientId;
        await using (var c = _factory.Create())
        {
            var productType = new ProductType
            {
                ProductCode = "RBC-LB-ISS",
                Name = "Lookback issued RBC",
                ComponentClass = ComponentClass.RedBloodCells
            };
            c.ProductTypes.Add(productType);
            await c.SaveChangesAsync();

            var issued = new BloodUnit
            {
                UnitNumber = "U-LB-ISS",
                ProductTypeId = productType.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Din = din,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Issued
            };
            var patient = new Patient
            {
                MedicalRecordNumber = "MRN-LB-ISS",
                LastName = "Issued",
                FirstName = "Recipient",
                DateOfBirth = new DateOnly(1982, 2, 2)
            };
            c.Patients.Add(patient);
            c.BloodUnits.Add(issued);
            await c.SaveChangesAsync();
            issuedId = issued.Id;
            patientId = patient.Id;
            c.Issues.Add(new Issue
            {
                BloodProductId = issued.Id,
                PatientId = patient.Id,
                IssuedUtc = _factory.Clock.UtcNow.AddHours(-1),
                IssuedBy = "tech-test",
                Status = IssueStatus.Issued
            });
            await c.SaveChangesAsync();
        }

        await using (var c = _factory.Create())
        {
            var first = await Lookback(c).RecallByDinAsync(din, "Donor subsequently reactive");
            Assert.True(first.Succeeded, first.Error);
            var note = Assert.Single(first.Value!.Notifications, n => n.BloodProductId == issuedId);
            Assert.Equal(LookbackNotificationStatus.Pending, note.Status);
            Assert.Equal(patientId, note.PatientId);
            Assert.Contains(first.Value.Units, u => u.BloodProductId == issuedId && u.Status == UnitStatus.Recalled);

            var second = await Lookback(c).RecallByDinAsync(din, "Donor subsequently reactive");
            Assert.True(second.Succeeded, second.Error);
            Assert.Single(second.Value!.Notifications, n => n.BloodProductId == issuedId);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Recalled, (await verify.BloodUnits.FindAsync(issuedId))!.Status);
        Assert.Equal(1, await verify.LookbackNotifications.CountAsync(n => n.BloodProductId == issuedId));
    }

    [Fact]
    public async Task ListOpenNotifications_IncludesPending_AndOmitsCompleted()
    {
        const string din = "W000088888888";
        long pendingId;
        long completedId;
        await using (var c = _factory.Create())
        {
            var productType = new ProductType
            {
                ProductCode = "RBC-LB-OPEN",
                Name = "Lookback open RBC",
                ComponentClass = ComponentClass.RedBloodCells
            };
            c.ProductTypes.Add(productType);
            var patient = new Patient
            {
                MedicalRecordNumber = "MRN-LB-OPEN",
                LastName = "Open",
                FirstName = "Recipient",
                DateOfBirth = new DateOnly(1984, 4, 4)
            };
            c.Patients.Add(patient);
            await c.SaveChangesAsync();

            var pendingUnit = new BloodUnit
            {
                UnitNumber = "U-LB-OPEN",
                ProductTypeId = productType.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Din = din,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Recalled
            };
            var completedUnit = new BloodUnit
            {
                UnitNumber = "U-LB-DONE",
                ProductTypeId = productType.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Din = din,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Transfused
            };
            c.BloodUnits.AddRange(pendingUnit, completedUnit);
            await c.SaveChangesAsync();
            pendingId = pendingUnit.Id;
            completedId = completedUnit.Id;
            c.LookbackNotifications.AddRange(
                new LookbackNotification
                {
                    Din = din,
                    BloodProductId = pendingUnit.Id,
                    PatientId = patient.Id,
                    Status = LookbackNotificationStatus.Pending,
                    Reason = "Still due"
                },
                new LookbackNotification
                {
                    Din = din,
                    BloodProductId = completedUnit.Id,
                    PatientId = patient.Id,
                    Status = LookbackNotificationStatus.Completed,
                    Reason = "Already called"
                });
            await c.SaveChangesAsync();
        }

        await using var c2 = _factory.Create();
        var open = await Lookback(c2).ListOpenNotificationsAsync();
        Assert.True(open.Succeeded, open.Error);
        var row = Assert.Single(open.Value!, n => n.BloodProductId == pendingId);
        Assert.Equal("U-LB-OPEN", row.UnitNumber);
        Assert.Equal("MRN-LB-OPEN", row.MedicalRecordNumber);
        Assert.Equal("Open, Recipient", row.PatientName);
        Assert.DoesNotContain(open.Value!, n => n.BloodProductId == completedId);
    }

    [Fact]
    public async Task RecallByDin_RecallsCrossmatchedUnit()
    {
        const string din = "W000044444444";
        long unitId;
        await using (var c = _factory.Create())
        {
            var productType = new ProductType
            {
                ProductCode = "RBC-LB-XM",
                Name = "Lookback XM RBC",
                ComponentClass = ComponentClass.RedBloodCells
            };
            c.ProductTypes.Add(productType);
            await c.SaveChangesAsync();

            var unit = new BloodUnit
            {
                UnitNumber = "U-LB-XM",
                ProductTypeId = productType.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Din = din,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Crossmatched
            };
            c.BloodUnits.Add(unit);
            await c.SaveChangesAsync();
            unitId = unit.Id;
        }

        await using (var c = _factory.Create())
        {
            var recalled = await Lookback(c).RecallByDinAsync(din, "Donor subsequently reactive");
            Assert.True(recalled.Succeeded, recalled.Error);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Recalled, (await verify.BloodUnits.FindAsync(unitId))!.Status);
    }

    [Fact]
    public async Task RecallByDin_WithoutLookbackManage_IsRejected()
    {
        const string din = "W000055555555";
        await using var c = _factory.Create();
        var productType = new ProductType
        {
            ProductCode = "RBC-LB-PERM",
            Name = "Lookback perm RBC",
            ComponentClass = ComponentClass.RedBloodCells
        };
        c.ProductTypes.Add(productType);
        await c.SaveChangesAsync();
        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = "U-LB-PERM",
            ProductTypeId = productType.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Din = din,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        });
        await c.SaveChangesAsync();

        var denied = await Lookback(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .RecallByDinAsync(din, "Donor subsequently reactive");
        Assert.False(denied.Succeeded);
        Assert.Contains("lookback.manage", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UnitStatus.Available, (await c.BloodUnits.SingleAsync(u => u.Din == din)).Status);
    }

    [Fact]
    public async Task RecordAttempt_WithoutLookbackManage_IsRejected()
    {
        await using var c = _factory.Create();
        var productType = new ProductType
        {
            ProductCode = "RBC-LB-ATT",
            Name = "Lookback attempt RBC",
            ComponentClass = ComponentClass.RedBloodCells
        };
        c.ProductTypes.Add(productType);
        await c.SaveChangesAsync();
        var unit = new BloodUnit
        {
            UnitNumber = "U-LB-ATT",
            ProductTypeId = productType.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Din = "W000066666666",
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Transfused
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        var note = new LookbackNotification
        {
            Din = unit.Din!,
            BloodProductId = unit.Id,
            Status = LookbackNotificationStatus.Pending,
            Reason = "Donor subsequently reactive"
        };
        c.LookbackNotifications.Add(note);
        await c.SaveChangesAsync();
        var notificationId = note.Id;
        var request = new RecordLookbackAttemptRequest(
            "Dr. Record", "Physician notified", LookbackNotificationStatus.Attempted);

        var denied = await Lookback(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .RecordAttemptAsync(notificationId, request);
        Assert.False(denied.Succeeded);
        Assert.Contains("lookback.manage", denied.Error, StringComparison.OrdinalIgnoreCase);
        var unchanged = await c.LookbackNotifications.AsNoTracking().SingleAsync(n => n.Id == notificationId);
        Assert.Equal(LookbackNotificationStatus.Pending, unchanged.Status);
        Assert.Null(unchanged.AttemptedBy);
        Assert.Null(unchanged.AttemptedUtc);

        var allowed = await Lookback(c, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .RecordAttemptAsync(notificationId, request);
        Assert.True(allowed.Succeeded);
        Assert.Equal(LookbackNotificationStatus.Attempted, allowed.Value!.Status);
        Assert.Equal("Dr. Record", allowed.Value.PhysicianOfRecord);
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

    [Fact]
    public async Task Lookback_NamesTheStorageLocationWhenThePlaceIsALocationCode()
    {
        const string key = "LB222";
        const string din = "W222000000222";
        long blankId;
        long namedIssueId;
        await using (var c = _factory.Create())
        {
            var patient = new Patient
            {
                MedicalRecordNumber = $"MRN-{key}",
                LastName = "Poe",
                FirstName = "Ann",
                DateOfBirth = new DateOnly(1975, 6, 1)
            };
            var product = new ProductType
            {
                ProductCode = $"RBC-{key}",
                Name = "Lookback RBC",
                ComponentClass = ComponentClass.RedBloodCells
            };
            c.Patients.Add(patient);
            c.ProductTypes.Add(product);
            var fridgeLocation = new InventoryLocation { Code = $"F1{key}", Name = " Issue fridge " };
            var blank = new InventoryLocation { Code = $"F2{key}", Name = "   " };
            var coded = new InventoryLocation { Code = $"OR{key}", Name = "Main OR" };
            var alsoNamed = new InventoryLocation { Code = $"X{key}", Name = $"OR{key}" };
            c.InventoryLocations.AddRange(fridgeLocation, blank, coded, alsoNamed);
            await c.SaveChangesAsync();
            blankId = blank.Id;

            BloodUnit Unit(string suffix) => new()
            {
                UnitNumber = $"U-{key}-{suffix}",
                ProductTypeId = product.Id,
                Din = din,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Issued
            };

            var fridgeUnit = Unit("FR");
            var blankUnit = Unit("BL");
            var wardUnit = Unit("WD");
            var ambiguousUnit = Unit("AM");
            c.BloodUnits.AddRange(fridgeUnit, blankUnit, wardUnit, ambiguousUnit);
            await c.SaveChangesAsync();

            Issue Issued(BloodUnit unit, string place) => new()
            {
                BloodProductId = unit.Id,
                PatientId = patient.Id,
                IssuedUtc = _factory.Clock.UtcNow,
                IssuedBy = "tech",
                IssuedToLocation = place,
                Status = IssueStatus.Issued
            };

            var fridgeIssue = Issued(fridgeUnit, $" f1{key.ToLower()} ");
            c.Issues.AddRange(
                fridgeIssue,
                Issued(blankUnit, $"F2{key}"),
                Issued(wardUnit, " 4W Med/Surg "),
                Issued(ambiguousUnit, $"or{key.ToLower()}"));
            await c.SaveChangesAsync();
            namedIssueId = fridgeIssue.Id;
        }

        await using var read = _factory.Create();
        var dinReport = await Lookback(read, withLocations: true).FindByDinAsync(din);
        Assert.True(dinReport.Succeeded, dinReport.Error);
        string Place(string suffix) =>
            Assert.Single(dinReport.Value!.Recipients, row => row.UnitNumber == $"U-{key}-{suffix}").IssuedToLocation!;
        Assert.Equal("Issue fridge", Place("FR"));
        Assert.DoesNotContain($"F1{key}", Place("FR"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"Location {blankId}", Place("BL"));
        Assert.DoesNotContain($"F2{key}", Place("BL"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("4W Med/Surg", Place("WD"));
        Assert.Equal($"or{key.ToLower()}", Place("AM"));
        Assert.Equal($" f1{key.ToLower()} ", (await read.Issues.FindAsync(namedIssueId))!.IssuedToLocation);

        var trace = await Lookback(read, withLocations: true).FindByRecipientAsync($"MRN-{key}", null);
        Assert.True(trace.Succeeded, trace.Error);
        string Traced(string suffix) =>
            Assert.Single(trace.Value!.Units, row => row.UnitNumber == $"U-{key}-{suffix}").IssuedToLocation!;
        Assert.Equal("Issue fridge", Traced("FR"));
        Assert.Equal($"Location {blankId}", Traced("BL"));
        Assert.Equal("4W Med/Surg", Traced("WD"));
        Assert.Equal($"or{key.ToLower()}", Traced("AM"));

        var unread = await Lookback(read).FindByDinAsync(din);
        Assert.True(unread.Succeeded, unread.Error);
        var unreadFridge = Assert.Single(unread.Value!.Recipients, row => row.UnitNumber == $"U-{key}-FR");
        Assert.Equal($"f1{key.ToLower()}", unreadFridge.IssuedToLocation);
        Assert.DoesNotContain("Issue fridge", unreadFridge.IssuedToLocation);
    }
}
