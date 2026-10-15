using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class InventoryServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public InventoryServiceTests(SqliteContextFactory factory) => _factory = factory;

    private InventoryService CreateService(BloodBankDbContext context, IPermissionEvaluator? permissions = null)
    {
        var repository = new InventoryRepository(context);
        var audit = new AuditWriter(context, _factory.Clock, _factory.CurrentUser);
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(context),
            new EfRepository<IsbtProductCode>(context));
        var retype = new ProductRetypeService(
            repository,
            new EfRepository<ProductRetypeResult>(context),
            new EfRepository<TestDefinition>(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit);
        return new InventoryService(
            repository,
            new EfRepository<UnitBloodAttribute>(context),
            new EfRepository<BloodAttributeDefinition>(context),
            lookups,
            context,
            _factory.Clock,
            _factory.CurrentUser,
            audit,
            new EfRepository<User>(context),
            new FacilityPolicyService(new EfRepository<SystemSetting>(context)),
            new EfRepository<Patient>(context),
            locations: new EfRepository<InventoryLocation>(context),
            productTypes: new EfRepository<ProductType>(context),
            permissions: permissions,
            retype: retype,
            retypeResults: new EfRepository<ProductRetypeResult>(context));
    }

    private static async Task<(long PosId, long NegId)> EnsureRhRetypeCatalogAsync(BloodBankDbContext context)
    {
        async Task<long> Ensure(string code, string name)
        {
            var existing = await context.TestDefinitions.FirstOrDefaultAsync(t => t.Code == code);
            if (existing is not null)
            {
                return existing.Id;
            }

            var test = new TestDefinition
            {
                Code = code,
                Name = name,
                Category = TestCategory.AboRhRetype,
                ResultValueType = ResultValueType.AboRh,
                IsActive = true,
                IsDraft = false,
                Version = 1
            };
            context.TestDefinitions.Add(test);
            await context.SaveChangesAsync();
            return test.Id;
        }

        return (
            await Ensure(ProductRetypeAssignment.RhPositiveTestCode, "Rh+ Retype"),
            await Ensure(ProductRetypeAssignment.RhNegativeTestCode, "Rh− Retype"));
    }

    private async Task SetReceiveVerifierPolicyAsync(bool required)
    {
        await using var context = _factory.Create();
        var setting = await context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == FacilityPolicyKeys.RequireReceiveVerifier);
        if (setting is null)
        {
            context.SystemSettings.Add(new SystemSetting
            {
                Key = FacilityPolicyKeys.RequireReceiveVerifier,
                Value = required ? "true" : "false",
                Category = "Inventory"
            });
        }
        else
        {
            setting.Value = required ? "true" : "false";
        }

        await context.SaveChangesAsync();
    }

    private async Task EnsureSecondVerifierAsync(string userName = "tech2")
    {
        await using var context = _factory.Create();
        if (await context.Users.AnyAsync(u => u.UserName == userName))
        {
            return;
        }

        context.Users.Add(new User
        {
            UserName = userName,
            DisplayName = "Tech Two",
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    private async Task EnsureProductCodesAsync()
    {
        await using var context = _factory.Create();
        if (!await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"))
        {
            context.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = "E0206",
                Description = "RED BLOOD CELLS|CPDA-1/450mL/refg|Irradiated",
                ComponentClass = nameof(ComponentClass.RedBloodCells),
                AttributesJson = "[]",
                StandardVersion = UsSupplierProductCodeSeed.StandardVersion,
                IsPlaceholder = true
            });
        }

        if (!await context.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0336"))
        {
            context.IsbtProductCodes.Add(new IsbtProductCode
            {
                ProductDescriptionCode = "E0336",
                Description = "RED BLOOD CELLS|CPD>AS1/500mL/refg|ResLeu:<5E6",
                ComponentClass = nameof(ComponentClass.RedBloodCells),
                AttributesJson = "[]",
                StandardVersion = UsSupplierProductCodeSeed.StandardVersion,
                IsPlaceholder = true
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task<long> EnsureProductTypeAsync()
    {
        await EnsureSecondVerifierAsync();
        await EnsureProductCodesAsync();
        await using var context = _factory.Create();
        var existing = await context.ProductTypes.FirstOrDefaultAsync(t => t.ProductCode == "RBC-TEST");
        if (existing is not null)
        {
            return existing.Id;
        }

        var type = new ProductType
        {
            ProductCode = "RBC-TEST",
            Name = "Test RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        context.ProductTypes.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
    }

    private async Task<long> EnsureAttributeDefinitionAsync(BloodBankDbContext context)
    {
        var existing = await context.BloodAttributeDefinitions.FirstOrDefaultAsync(d => d.Code == "K");
        if (existing is not null)
        {
            return existing.Id;
        }

        var kell = new BloodAttributeDefinition
        {
            Code = "K",
            Name = "Kell",
            AntibodyName = "anti-K",
            IsClinicallySignificant = true,
            SortOrder = 1,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = _factory.Clock.UtcNow,
            Version = 1
        };
        context.BloodAttributeDefinitions.Add(kell);
        await context.SaveChangesAsync();
        return kell.Id;
    }

    private ReceiveUnitRequest NewUnitRequest(
        string unitNumber,
        long productTypeId,
        DateTime? expires = null,
        string? productCode = "E0206") =>
        new(unitNumber, productTypeId, AboGroup.O, RhType.Positive,
            expires ?? _factory.Clock.UtcNow.AddDays(30),
            Isbt128ProductCode: productCode,
            SecondVerifier: "tech2",
            ReceiveTemperatureCelsius: 4.0m);

    [Fact]
    public async Task ReceiveUnit_CreatesQuarantineUnit_WithInitialHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var result = await service.ReceiveUnitAsync(NewUnitRequest("U-INTAKE-1", productTypeId));

            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Quarantine, result.Unit!.Status);
            Assert.Equal(UnitQuarantineReason.PendingRelease, result.Unit.QuarantineReasonCode);
            Assert.Equal("E0206", result.Unit.ProductDescriptionCode);
            unitId = result.Unit.Id;
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            var initial = Assert.Single(history);
            Assert.Null(initial.FromStatus);
            Assert.Equal(UnitStatus.Quarantine, initial.ToStatus);
        }
    }

    [Fact]
    public async Task ListNearExpiry_IncludesOnHandUnitsInsideWindow()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var soon = await service.ReceiveUnitAsync(
            NewUnitRequest("U-NEAR-YES", productTypeId, _factory.Clock.UtcNow.AddHours(12)));
        Assert.True(soon.Succeeded, soon.Error);
        var later = await service.ReceiveUnitAsync(NewUnitRequest("U-NEAR-NO", productTypeId));
        Assert.True(later.Succeeded, later.Error);

        var list = await service.ListNearExpiryAsync();
        Assert.Contains(list, i => i.UnitNumber == "U-NEAR-YES");
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-NEAR-NO");
    }

    [Fact]
    public async Task Discard_NearExpiryUnit_LeavesTheBoard()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(
                NewUnitRequest("U-NEAR-DISC", productTypeId, _factory.Clock.UtcNow.AddHours(8)));
            Assert.True(received.Succeeded, received.Error);
            unitId = received.Unit!.Id;
            Assert.Contains(await CreateService(setup).ListNearExpiryAsync(), i => i.UnitId == unitId);
        }

        await using (var act = _factory.Create())
        {
            var discarded = await CreateService(act).DiscardAsync(unitId, "Outdated before issue", "tech2");
            Assert.True(discarded.Succeeded, discarded.Error);
            Assert.Equal(UnitStatus.Discarded, discarded.Unit!.Status);
        }

        await using var verify = _factory.Create();
        Assert.DoesNotContain(await CreateService(verify).ListNearExpiryAsync(), i => i.UnitId == unitId);
    }

    [Fact]
    public async Task ListNearExpiry_NamesTheStorageLocation()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var fridge = new InventoryLocation
        {
            Code = $"NEAR-FR-{key}",
            Name = " Outdate fridge ",
            LocationType = LocationType.Refrigerator
        };
        var blank = new InventoryLocation
        {
            Code = $"NEAR-BL-{key}",
            Name = "   ",
            LocationType = LocationType.Refrigerator
        };
        context.InventoryLocations.AddRange(fridge, blank);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var expires = _factory.Clock.UtcNow.AddHours(10);
        var named = await service.ReceiveUnitAsync(NewUnitRequest($"U-NEAR-LOC-{key}", productTypeId, expires) with { LocationId = fridge.Id });
        var unnamed = await service.ReceiveUnitAsync(NewUnitRequest($"U-NEAR-NONE-{key}", productTypeId, expires));
        var blankNamed = await service.ReceiveUnitAsync(NewUnitRequest($"U-NEAR-BLANK-{key}", productTypeId, expires) with { LocationId = blank.Id });
        Assert.True(named.Succeeded, named.Error);
        Assert.True(unnamed.Succeeded, unnamed.Error);
        Assert.True(blankNamed.Succeeded, blankNamed.Error);

        var list = await service.ListNearExpiryAsync();
        var namedRow = Assert.Single(list, row => row.UnitNumber == $"U-NEAR-LOC-{key}");
        var unnamedRow = Assert.Single(list, row => row.UnitNumber == $"U-NEAR-NONE-{key}");
        var blankRow = Assert.Single(list, row => row.UnitNumber == $"U-NEAR-BLANK-{key}");

        Assert.Equal(fridge.Id, namedRow.CurrentLocationId);
        Assert.Equal("Outdate fridge", namedRow.LocationLabel);
        Assert.Null(unnamedRow.CurrentLocationId);
        Assert.Equal(StorageLocationLabel.None, unnamedRow.LocationLabel);
        Assert.Equal(blank.Id, blankRow.CurrentLocationId);
        Assert.Equal($"Location {blank.Id}", blankRow.LocationLabel);
        Assert.DoesNotContain(blank.Code, blankRow.LocationLabel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnitActivity_NamesTheStorageLocationsOnAMove()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var fromFridge = new InventoryLocation
        {
            Code = $"MOVE-FR-{key}",
            Name = " Paperwork fridge ",
            LocationType = LocationType.Refrigerator
        };
        var toFridge = new InventoryLocation
        {
            Code = $"MOVE-TO-{key}",
            Name = "Outdate fridge",
            LocationType = LocationType.Refrigerator
        };
        var blank = new InventoryLocation
        {
            Code = $"MOVE-BL-{key}",
            Name = "   ",
            LocationType = LocationType.Refrigerator
        };
        context.InventoryLocations.AddRange(fromFridge, toFridge, blank);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var placed = await service.ReceiveUnitAsync(
            NewUnitRequest($"U-MOVE-LOC-{key}", productTypeId) with { LocationId = fromFridge.Id });
        var unplaced = await service.ReceiveUnitAsync(NewUnitRequest($"U-MOVE-NONE-{key}", productTypeId));
        var blankNamed = await service.ReceiveUnitAsync(
            NewUnitRequest($"U-MOVE-BLANK-{key}", productTypeId) with { LocationId = blank.Id });
        Assert.True(placed.Succeeded, placed.Error);
        Assert.True(unplaced.Succeeded, unplaced.Error);
        Assert.True(blankNamed.Succeeded, blankNamed.Error);
        Assert.True((await service.TransferAsync(placed.Unit!.Id, toFridge.Id, "Move to outdate")).Succeeded);
        Assert.True((await service.TransferAsync(blankNamed.Unit!.Id, toFridge.Id, "Move from an unnamed place")).Succeeded);

        var activity = new UnitActivityService(
            new InventoryRepository(context),
            new EfRepository<Allocation>(context),
            new EfRepository<Crossmatch>(context),
            new EfRepository<Issue>(context),
            new EfRepository<Return>(context),
            new EfRepository<TransfusionEvent>(context),
            new EfRepository<OrderAction>(context),
            new EfRepository<ActionDefinition>(context),
            new EfRepository<UnitModificationUnit>(context),
            new EfRepository<UnitModification>(context),
            new EfRepository<Patient>(context),
            new EfRepository<InventoryLocation>(context));

        var placedTrail = await activity.ListAsync(placed.Unit.Id);
        var intake = Assert.Single(placedTrail, row => row.Summary.Contains("Initial intake", StringComparison.Ordinal));
        var move = Assert.Single(placedTrail, row => row.Summary.Contains("Move to outdate", StringComparison.Ordinal));
        Assert.Contains("to Paperwork fridge", intake.Summary, StringComparison.Ordinal);
        Assert.Contains("from Paperwork fridge to Outdate fridge", move.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(fromFridge.Code, intake.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(toFridge.Code, move.Summary, StringComparison.Ordinal);

        var unplacedTrail = await activity.ListAsync(unplaced.Unit!.Id);
        var unplacedIntake = Assert.Single(unplacedTrail, row => row.EventType == "Status");
        Assert.DoesNotContain(" · ", unplacedIntake.Summary, StringComparison.Ordinal);

        var blankTrail = await activity.ListAsync(blankNamed.Unit!.Id);
        var blankMove = Assert.Single(blankTrail, row => row.Summary.Contains("Move from an unnamed place", StringComparison.Ordinal));
        Assert.Contains($"from Location {blank.Id} to Outdate fridge", blankMove.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(blank.Code, blankMove.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnitActivity_NamesThePatientOnAnAllocation()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest($"U-ALLOC-NAME-{key}", productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var named = new Patient
        {
            MedicalRecordNumber = $"MRN-ALLOC-{key}",
            LastName = " Doe ",
            FirstName = " Jane ",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        var blank = new Patient
        {
            MedicalRecordNumber = $"MRN-ALLOC-BL-{key}",
            LastName = "   ",
            FirstName = " ",
            DateOfBirth = new DateOnly(1981, 2, 2),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        context.Patients.AddRange(named, blank);
        await context.SaveChangesAsync();

        var missingPatientId = 9_000_000_000 + named.Id;
        context.Allocations.AddRange(
            new Allocation
            {
                BloodProductId = received.Unit!.Id,
                PatientId = named.Id,
                Status = AllocationStatus.Released,
                AllocatedUtc = _factory.Clock.UtcNow.AddMinutes(-5),
                AllocatedBy = "tech"
            },
            new Allocation
            {
                BloodProductId = received.Unit.Id,
                PatientId = missingPatientId,
                Status = AllocationStatus.Expired,
                AllocatedUtc = _factory.Clock.UtcNow.AddMinutes(-4),
                AllocatedBy = "tech"
            },
            new Allocation
            {
                BloodProductId = received.Unit.Id,
                PatientId = blank.Id,
                Status = AllocationStatus.Consumed,
                AllocatedUtc = _factory.Clock.UtcNow.AddMinutes(-3),
                AllocatedBy = "tech"
            });
        await context.SaveChangesAsync();

        var activity = new UnitActivityService(
            new InventoryRepository(context),
            new EfRepository<Allocation>(context),
            new EfRepository<Crossmatch>(context),
            new EfRepository<Issue>(context),
            new EfRepository<Return>(context),
            new EfRepository<TransfusionEvent>(context),
            new EfRepository<OrderAction>(context),
            new EfRepository<ActionDefinition>(context),
            new EfRepository<UnitModificationUnit>(context),
            new EfRepository<UnitModification>(context),
            new EfRepository<Patient>(context));
        var trail = await activity.ListAsync(received.Unit.Id);
        var namedRow = Assert.Single(trail, row => row.Summary.StartsWith("Released to ", StringComparison.Ordinal));
        var missingRow = Assert.Single(trail, row => row.Summary.StartsWith("Expired to ", StringComparison.Ordinal));
        var blankRow = Assert.Single(trail, row => row.Summary.StartsWith("Consumed to ", StringComparison.Ordinal));

        Assert.Equal($"Released to Doe, Jane ({named.MedicalRecordNumber})", namedRow.Summary);
        Assert.Equal($"Expired to patient #{missingPatientId}", missingRow.Summary);
        Assert.Equal($"Consumed to patient #{blank.Id} ({blank.MedicalRecordNumber})", blankRow.Summary);
    }

    [Fact]
    public async Task UnitActivity_NamesThePatientOnACrossmatch()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest($"U-XM-NAME-{key}", productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var named = new Patient
        {
            MedicalRecordNumber = $"MRN-XM-{key}",
            LastName = " Roe ",
            FirstName = " Sam ",
            DateOfBirth = new DateOnly(1982, 3, 3),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        var blank = new Patient
        {
            MedicalRecordNumber = $"MRN-XM-BL-{key}",
            LastName = "   ",
            FirstName = " ",
            DateOfBirth = new DateOnly(1983, 4, 4),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        context.Patients.AddRange(named, blank);
        await context.SaveChangesAsync();

        var missingPatientId = 9_000_000_000 + named.Id;
        context.Crossmatches.AddRange(
            new Crossmatch
            {
                BloodProductId = received.Unit!.Id,
                PatientId = named.Id,
                SpecimenId = 1,
                Method = CrossmatchMethod.Serologic,
                Result = CrossmatchResult.Compatible,
                PerformedUtc = _factory.Clock.UtcNow.AddMinutes(-5),
                PerformedBy = "tech"
            },
            new Crossmatch
            {
                BloodProductId = received.Unit.Id,
                PatientId = missingPatientId,
                SpecimenId = 2,
                Method = CrossmatchMethod.Electronic,
                Result = CrossmatchResult.Compatible,
                PerformedUtc = _factory.Clock.UtcNow.AddMinutes(-4),
                PerformedBy = "tech"
            },
            new Crossmatch
            {
                BloodProductId = received.Unit.Id,
                PatientId = blank.Id,
                SpecimenId = 3,
                Method = CrossmatchMethod.ImmediateSpin,
                Result = CrossmatchResult.Incompatible,
                PerformedUtc = _factory.Clock.UtcNow.AddMinutes(-3),
                PerformedBy = "tech"
            });
        await context.SaveChangesAsync();

        var activity = new UnitActivityService(
            new InventoryRepository(context),
            new EfRepository<Allocation>(context),
            new EfRepository<Crossmatch>(context),
            new EfRepository<Issue>(context),
            new EfRepository<Return>(context),
            new EfRepository<TransfusionEvent>(context),
            new EfRepository<OrderAction>(context),
            new EfRepository<ActionDefinition>(context),
            new EfRepository<UnitModificationUnit>(context),
            new EfRepository<UnitModification>(context),
            new EfRepository<Patient>(context));
        var trail = await activity.ListAsync(received.Unit.Id);
        var namedRow = Assert.Single(trail, row => row.Summary.StartsWith("Serologic Compatible for ", StringComparison.Ordinal));
        var missingRow = Assert.Single(trail, row => row.Summary.StartsWith("Electronic Compatible for ", StringComparison.Ordinal));
        var blankRow = Assert.Single(trail, row => row.Summary.StartsWith("ImmediateSpin Incompatible for ", StringComparison.Ordinal));

        Assert.Equal($"Serologic Compatible for Roe, Sam ({named.MedicalRecordNumber})", namedRow.Summary);
        Assert.Equal($"Electronic Compatible for patient #{missingPatientId}", missingRow.Summary);
        Assert.Equal($"ImmediateSpin Incompatible for patient #{blank.Id} ({blank.MedicalRecordNumber})", blankRow.Summary);
    }

    [Fact]
    public async Task UnitActivity_NamesThePatientOnATransfusion()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest($"U-TX-NAME-{key}", productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var named = new Patient
        {
            MedicalRecordNumber = $"MRN-TX-{key}",
            LastName = " Poe ",
            FirstName = " Ann ",
            DateOfBirth = new DateOnly(1984, 5, 5),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        var blank = new Patient
        {
            MedicalRecordNumber = $"MRN-TX-BL-{key}",
            LastName = "   ",
            FirstName = " ",
            DateOfBirth = new DateOnly(1985, 6, 6),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        context.Patients.AddRange(named, blank);
        await context.SaveChangesAsync();

        var missingPatientId = 9_000_000_000 + named.Id;
        var namedIssue = new Issue
        {
            BloodProductId = received.Unit!.Id,
            PatientId = named.Id,
            IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-10),
            IssuedBy = "tech",
            Status = IssueStatus.Transfused
        };
        var missingIssue = new Issue
        {
            BloodProductId = received.Unit.Id,
            PatientId = missingPatientId,
            IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-9),
            IssuedBy = "tech",
            Status = IssueStatus.Returned
        };
        var blankIssue = new Issue
        {
            BloodProductId = received.Unit.Id,
            PatientId = blank.Id,
            IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-8),
            IssuedBy = "tech",
            Status = IssueStatus.Issued
        };
        context.Issues.AddRange(namedIssue, missingIssue, blankIssue);
        await context.SaveChangesAsync();

        context.TransfusionEvents.AddRange(
            new TransfusionEvent
            {
                IssueId = namedIssue.Id,
                BloodProductId = received.Unit.Id,
                PatientId = named.Id,
                FinalDisposition = TransfusionDisposition.Completed,
                StartUtc = _factory.Clock.UtcNow.AddMinutes(-6),
                DocumentedBy = "tech"
            },
            new TransfusionEvent
            {
                IssueId = missingIssue.Id,
                BloodProductId = received.Unit.Id,
                PatientId = missingPatientId,
                FinalDisposition = TransfusionDisposition.Stopped,
                StartUtc = _factory.Clock.UtcNow.AddMinutes(-5),
                DocumentedBy = "tech"
            },
            new TransfusionEvent
            {
                IssueId = blankIssue.Id,
                BloodProductId = received.Unit.Id,
                PatientId = blank.Id,
                FinalDisposition = TransfusionDisposition.Wasted,
                StartUtc = _factory.Clock.UtcNow.AddMinutes(-4),
                DocumentedBy = "tech"
            });
        await context.SaveChangesAsync();

        var activity = new UnitActivityService(
            new InventoryRepository(context),
            new EfRepository<Allocation>(context),
            new EfRepository<Crossmatch>(context),
            new EfRepository<Issue>(context),
            new EfRepository<Return>(context),
            new EfRepository<TransfusionEvent>(context),
            new EfRepository<OrderAction>(context),
            new EfRepository<ActionDefinition>(context),
            new EfRepository<UnitModificationUnit>(context),
            new EfRepository<UnitModification>(context),
            new EfRepository<Patient>(context));
        var trail = await activity.ListAsync(received.Unit.Id);
        var namedRow = Assert.Single(trail, row => row.EventType == "Transfusion" && row.Summary.StartsWith("Completed for ", StringComparison.Ordinal));
        var missingRow = Assert.Single(trail, row => row.EventType == "Transfusion" && row.Summary.StartsWith("Stopped for ", StringComparison.Ordinal));
        var blankRow = Assert.Single(trail, row => row.EventType == "Transfusion" && row.Summary.StartsWith("Wasted for ", StringComparison.Ordinal));

        Assert.Equal($"Completed for Poe, Ann ({named.MedicalRecordNumber})", namedRow.Summary);
        Assert.Equal($"Stopped for patient #{missingPatientId}", missingRow.Summary);
        Assert.Equal($"Wasted for patient #{blank.Id} ({blank.MedicalRecordNumber})", blankRow.Summary);
    }

    [Fact]
    public async Task UnitActivity_NamesThePatientOnAReturn()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest($"U-RET-NAME-{key}", productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var named = new Patient
        {
            MedicalRecordNumber = $"MRN-RET-{key}",
            LastName = " Poe ",
            FirstName = " Ann ",
            DateOfBirth = new DateOnly(1986, 7, 7),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        var blank = new Patient
        {
            MedicalRecordNumber = $"MRN-RET-BL-{key}",
            LastName = "   ",
            FirstName = " ",
            DateOfBirth = new DateOnly(1987, 8, 8),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        context.Patients.AddRange(named, blank);
        await context.SaveChangesAsync();

        var missingPatientId = 9_000_000_000 + named.Id;
        var namedIssue = new Issue
        {
            BloodProductId = received.Unit!.Id,
            PatientId = named.Id,
            IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-10),
            IssuedBy = "tech",
            Status = IssueStatus.Returned
        };
        var blankIssue = new Issue
        {
            BloodProductId = received.Unit.Id,
            PatientId = blank.Id,
            IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-9),
            IssuedBy = "tech",
            Status = IssueStatus.Transfused
        };
        var missingIssue = new Issue
        {
            BloodProductId = received.Unit.Id,
            PatientId = missingPatientId,
            IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-8),
            IssuedBy = "tech",
            Status = IssueStatus.Returned
        };
        context.Issues.AddRange(namedIssue, blankIssue, missingIssue);
        await context.SaveChangesAsync();

        context.Returns.AddRange(
            new Return
            {
                IssueId = namedIssue.Id,
                BloodProductId = received.Unit.Id,
                ReturnedUtc = _factory.Clock.UtcNow.AddMinutes(-6),
                ReturnedBy = "tech",
                Reason = " Unused ",
                ReissueEligible = false
            },
            new Return
            {
                IssueId = blankIssue.Id,
                BloodProductId = received.Unit.Id,
                ReturnedUtc = _factory.Clock.UtcNow.AddMinutes(-5),
                ReturnedBy = "tech",
                Reason = "Cooler intact",
                ReissueEligible = true
            },
            new Return
            {
                IssueId = missingIssue.Id,
                BloodProductId = received.Unit.Id,
                ReturnedUtc = _factory.Clock.UtcNow.AddMinutes(-4),
                ReturnedBy = "tech",
                Reason = "Not given",
                ReissueEligible = false
            });
        await context.SaveChangesAsync();

        var activity = new UnitActivityService(
            new InventoryRepository(context),
            new EfRepository<Allocation>(context),
            new EfRepository<Crossmatch>(context),
            new EfRepository<Issue>(context),
            new EfRepository<Return>(context),
            new EfRepository<TransfusionEvent>(context),
            new EfRepository<OrderAction>(context),
            new EfRepository<ActionDefinition>(context),
            new EfRepository<UnitModificationUnit>(context),
            new EfRepository<UnitModification>(context),
            new EfRepository<Patient>(context));
        var trail = await activity.ListAsync(received.Unit.Id);
        var namedRow = Assert.Single(trail, row => row.EventType == "Return" && row.Summary.StartsWith("Returned — Unused from ", StringComparison.Ordinal));
        var blankRow = Assert.Single(trail, row => row.EventType == "Return" && row.Summary.StartsWith("Returned (reissue eligible) — Cooler intact from ", StringComparison.Ordinal));
        var missingRow = Assert.Single(trail, row => row.EventType == "Return" && row.Summary.StartsWith("Returned — Not given from ", StringComparison.Ordinal));

        Assert.Equal($"Returned — Unused from Poe, Ann ({named.MedicalRecordNumber})", namedRow.Summary);
        Assert.Equal($"Returned (reissue eligible) — Cooler intact from patient #{blank.Id} ({blank.MedicalRecordNumber})", blankRow.Summary);
        Assert.Equal($"Returned — Not given from patient #{missingPatientId}", missingRow.Summary);
        Assert.DoesNotContain("could not be read", namedRow.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnitActivity_NamesThePatientOnAnOrderModification()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest($"U-MOD-NAME-{key}", productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var named = new Patient
        {
            MedicalRecordNumber = $"MRN-MOD-{key}",
            LastName = " Poe ",
            FirstName = " Ann ",
            DateOfBirth = new DateOnly(1988, 9, 9),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        var blank = new Patient
        {
            MedicalRecordNumber = $"MRN-MOD-BL-{key}",
            LastName = "   ",
            FirstName = " ",
            DateOfBirth = new DateOnly(1989, 10, 10),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        context.Patients.AddRange(named, blank);
        await context.SaveChangesAsync();
        var encounter = new Encounter
        {
            PatientId = named.Id,
            VisitNumber = $"VIS-MOD-{key}",
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        };
        var location = new OrderingLocation { Code = $"LOC-MOD-{key}", Name = "Ward", IsActive = true };
        context.Encounters.Add(encounter);
        context.OrderingLocations.Add(location);
        await context.SaveChangesAsync();

        Order MakeOrder(string number, long patientId) => new()
        {
            OrderNumber = number,
            OrderName = "Irradiate red cells",
            PatientId = patientId,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Product,
            ProductTypeId = productTypeId,
            Status = OrderStatus.InProcess,
            OrderedUtc = _factory.Clock.UtcNow
        };
        var namedOrder = MakeOrder($"ORD-MOD-{key}", named.Id);
        var blankOrder = MakeOrder($"ORD-MOD-BL-{key}", blank.Id);
        context.Orders.AddRange(namedOrder, blankOrder);
        var irradiate = new ActionDefinition
        {
            Code = $"IRRAD-{key}",
            Name = "Irradiate",
            Category = ActionCategory.Modification,
            ModificationType = ModificationType.Irradiate,
            IsActive = true
        };
        var wash = new ActionDefinition
        {
            Code = $"WASH-{key}",
            Name = "Wash",
            Category = ActionCategory.Modification,
            ModificationType = ModificationType.Wash,
            IsActive = true
        };
        context.ActionDefinitions.AddRange(irradiate, wash);
        await context.SaveChangesAsync();

        context.OrderActions.AddRange(
            new OrderAction
            {
                OrderId = namedOrder.Id,
                ActionDefinitionId = irradiate.Id,
                Status = OrderActionStatus.Completed,
                SourceBloodUnitId = received.Unit!.Id,
                CompletedUtc = _factory.Clock.UtcNow.AddMinutes(-5),
                CompletedBy = "tech"
            },
            new OrderAction
            {
                OrderId = blankOrder.Id,
                ActionDefinitionId = wash.Id,
                Status = OrderActionStatus.Pending,
                SourceBloodUnitId = received.Unit.Id
            });
        await context.SaveChangesAsync();

        var activity = new UnitActivityService(
            new InventoryRepository(context),
            new EfRepository<Allocation>(context),
            new EfRepository<Crossmatch>(context),
            new EfRepository<Issue>(context),
            new EfRepository<Return>(context),
            new EfRepository<TransfusionEvent>(context),
            new EfRepository<OrderAction>(context),
            new EfRepository<ActionDefinition>(context),
            new EfRepository<UnitModificationUnit>(context),
            new EfRepository<UnitModification>(context),
            new EfRepository<Patient>(context),
            orders: new EfRepository<Order>(context));
        var trail = await activity.ListAsync(received.Unit.Id);
        var namedRow = Assert.Single(trail, row => row.Summary.StartsWith("Irradiate Completed for ", StringComparison.Ordinal));
        var blankRow = Assert.Single(trail, row => row.Summary.StartsWith("Wash Pending for ", StringComparison.Ordinal));

        Assert.Equal($"Irradiate Completed for Poe, Ann ({named.MedicalRecordNumber})", namedRow.Summary);
        Assert.Equal($"Wash Pending for patient #{blank.Id} ({blank.MedicalRecordNumber})", blankRow.Summary);
        Assert.DoesNotContain("could not be read", namedRow.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnitActivity_SaysWhenABenchModificationHasNoPatientOrder()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest($"U-BENCH-{key}", productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var expiration = new ExpirationModificationCode
        {
            Code = $"24H-{key}",
            OffsetAmount = 24,
            OffsetUnit = ExpirationOffsetUnit.Hours,
            RelativeTo = ExpirationRelativeTo.ModificationDateTime,
            IsActive = true
        };
        context.ExpirationModificationCodes.Add(expiration);
        await context.SaveChangesAsync();
        var rule = new ModificationRule
        {
            ModificationCode = $"BENCH-{key}",
            SourceProductTypeId = productTypeId,
            TargetProductTypeId = productTypeId,
            ModificationType = ModificationType.Wash,
            ExpirationModificationCodeId = expiration.Id,
            IsActive = true
        };
        context.ModificationRules.Add(rule);
        await context.SaveChangesAsync();

        var bench = new UnitModification
        {
            ModificationRuleId = rule.Id,
            ModificationType = ModificationType.Wash,
            ExpirationOffsetCodeApplied = "24H",
            ResultExpiresUtc = _factory.Clock.UtcNow.AddHours(24),
            Reason = " Clinical need ",
            PerformedBy = "tech",
            PerformedUtc = _factory.Clock.UtcNow.AddMinutes(-30)
        };
        var blankReason = new UnitModification
        {
            ModificationRuleId = rule.Id,
            ModificationType = ModificationType.Irradiate,
            ExpirationOffsetCodeApplied = "24H",
            ResultExpiresUtc = _factory.Clock.UtcNow.AddHours(24),
            Reason = "   ",
            PerformedBy = "tech",
            PerformedUtc = _factory.Clock.UtcNow.AddMinutes(-20)
        };
        var linked = new UnitModification
        {
            ModificationRuleId = rule.Id,
            ModificationType = ModificationType.Pool,
            ExpirationOffsetCodeApplied = "24H",
            ResultExpiresUtc = _factory.Clock.UtcNow.AddHours(24),
            Reason = "For the order",
            PerformedBy = "tech",
            PerformedUtc = _factory.Clock.UtcNow.AddMinutes(-10)
        };
        context.UnitModifications.AddRange(bench, blankReason, linked);
        await context.SaveChangesAsync();
        context.UnitModificationUnits.AddRange(
            new UnitModificationUnit
            {
                UnitModificationId = bench.Id,
                BloodProductId = received.Unit!.Id,
                Role = ModificationUnitRole.Source
            },
            new UnitModificationUnit
            {
                UnitModificationId = blankReason.Id,
                BloodProductId = received.Unit.Id,
                Role = ModificationUnitRole.Result
            },
            new UnitModificationUnit
            {
                UnitModificationId = linked.Id,
                BloodProductId = received.Unit.Id,
                Role = ModificationUnitRole.Source
            });

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-BENCH-{key}",
            LastName = "Poe",
            FirstName = "Ann",
            DateOfBirth = new DateOnly(1988, 9, 9),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-BENCH-{key}",
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        };
        var location = new OrderingLocation { Code = $"LOC-BENCH-{key}", Name = "Ward", IsActive = true };
        context.Encounters.Add(encounter);
        context.OrderingLocations.Add(location);
        await context.SaveChangesAsync();
        var order = new Order
        {
            OrderNumber = $"ORD-BENCH-{key}",
            OrderName = "Pool platelets",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Product,
            ProductTypeId = productTypeId,
            Status = OrderStatus.InProcess,
            OrderedUtc = _factory.Clock.UtcNow
        };
        var definition = new ActionDefinition
        {
            Code = $"POOL-{key}",
            Name = "Pool",
            Category = ActionCategory.Modification,
            ModificationType = ModificationType.Pool,
            IsActive = true
        };
        context.Orders.Add(order);
        context.ActionDefinitions.Add(definition);
        await context.SaveChangesAsync();
        context.OrderActions.Add(new OrderAction
        {
            OrderId = order.Id,
            ActionDefinitionId = definition.Id,
            Status = OrderActionStatus.Completed,
            SourceBloodUnitId = received.Unit.Id,
            UnitModificationId = linked.Id,
            CompletedUtc = _factory.Clock.UtcNow,
            CompletedBy = "tech"
        });
        await context.SaveChangesAsync();

        var activity = new UnitActivityService(
            new InventoryRepository(context),
            new EfRepository<Allocation>(context),
            new EfRepository<Crossmatch>(context),
            new EfRepository<Issue>(context),
            new EfRepository<Return>(context),
            new EfRepository<TransfusionEvent>(context),
            new EfRepository<OrderAction>(context),
            new EfRepository<ActionDefinition>(context),
            new EfRepository<UnitModificationUnit>(context),
            new EfRepository<UnitModification>(context),
            new EfRepository<Patient>(context),
            orders: new EfRepository<Order>(context));
        var trail = await activity.ListAsync(received.Unit.Id);
        var benchRows = trail.Where(row => row.RelatedEntityType == nameof(UnitModification)).ToList();

        Assert.Contains(benchRows, row => row.Summary == "Wash (Source) — Clinical need. No patient order was recorded.");
        Assert.Contains(benchRows, row => row.Summary == "Irradiate (Result). No patient order was recorded.");
        var linkedRow = Assert.Single(benchRows, row => row.Summary.StartsWith("Pool (", StringComparison.Ordinal));
        Assert.Equal("Pool (Source) — For the order", linkedRow.Summary);
        Assert.DoesNotContain("No patient order was recorded", linkedRow.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListNearExpiry_ExcludesExpectedAndExpired()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var expected = await service.ExpectUnitAsync(
            NewUnitRequest("U-NEAR-ASN", productTypeId, _factory.Clock.UtcNow.AddHours(6)));
        Assert.True(expected.Succeeded, expected.Error);

        var expired = await service.ReceiveUnitAsync(
            NewUnitRequest("U-NEAR-PAST", productTypeId, _factory.Clock.UtcNow.AddHours(1)));
        Assert.True(expired.Succeeded, expired.Error);
        expired.Unit!.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
        await context.SaveChangesAsync();

        var list = await service.ListNearExpiryAsync();
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-NEAR-ASN");
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-NEAR-PAST");
    }

    [Fact]
    public async Task ListQuarantine_IncludesReceivedUnits()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-LIST", productTypeId));
        Assert.True(received.Succeeded, received.Error);
        var expected = await service.ExpectUnitAsync(NewUnitRequest("U-Q-ASN", productTypeId));
        Assert.True(expected.Succeeded, expected.Error);

        var list = await service.ListQuarantineAsync();
        var receivedRow = Assert.Single(list, i => i.UnitNumber == "U-Q-LIST");
        Assert.Equal(UnitQuarantineReason.PendingRelease, receivedRow.ReasonCode);
        Assert.Equal("RBC-TEST", receivedRow.ProductCode);
        Assert.False(receivedRow.RetypeMismatch);
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-Q-ASN");
    }

    [Fact]
    public async Task ListQuarantine_NamesTheStorageLocation()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var fridge = new InventoryLocation
        {
            Code = $"Q-FRIDGE-{key}",
            Name = " Fridge A ",
            LocationType = LocationType.Refrigerator
        };
        var blank = new InventoryLocation
        {
            Code = $"Q-BLANK-{key}",
            Name = "  ",
            LocationType = LocationType.Refrigerator
        };
        context.InventoryLocations.AddRange(fridge, blank);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var named = await service.ReceiveUnitAsync(NewUnitRequest($"U-Q-LOC-{key}", productTypeId));
        var unnamed = await service.ReceiveUnitAsync(NewUnitRequest($"U-Q-NONE-{key}", productTypeId));
        var blankNamed = await service.ReceiveUnitAsync(NewUnitRequest($"U-Q-BLANK-{key}", productTypeId));
        Assert.True(named.Succeeded, named.Error);
        Assert.True(unnamed.Succeeded, unnamed.Error);
        Assert.True(blankNamed.Succeeded, blankNamed.Error);
        Assert.True((await service.TransferAsync(named.Unit!.Id, fridge.Id, "Move the new result")).Succeeded);
        Assert.True((await service.TransferAsync(blankNamed.Unit!.Id, blank.Id, "Move to an unnamed place")).Succeeded);

        var list = await service.ListQuarantineAsync();
        var namedRow = Assert.Single(list, row => row.UnitNumber == $"U-Q-LOC-{key}");
        var unnamedRow = Assert.Single(list, row => row.UnitNumber == $"U-Q-NONE-{key}");
        var blankRow = Assert.Single(list, row => row.UnitNumber == $"U-Q-BLANK-{key}");

        Assert.Equal(fridge.Id, namedRow.CurrentLocationId);
        Assert.Equal("Fridge A", namedRow.LocationLabel);
        Assert.Equal("It is at Fridge A.", namedRow.ReleasePlaceSentence);
        Assert.Null(unnamedRow.CurrentLocationId);
        Assert.Equal(StorageLocationLabel.None, unnamedRow.LocationLabel);
        Assert.Equal("No storage location is recorded.", unnamedRow.ReleasePlaceSentence);
        Assert.Equal(blank.Id, blankRow.CurrentLocationId);
        Assert.Equal($"Location {blank.Id}", blankRow.LocationLabel);
        Assert.Equal($"It is at Location {blank.Id}.", blankRow.ReleasePlaceSentence);
    }

    [Fact]
    public async Task ListQuarantine_ShowsRecordedVolume_AndDoesNotInventOne()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var measured = await service.ReceiveUnitAsync(NewUnitRequest($"U-Q-VOL-{key}", productTypeId));
        var unmeasured = await service.ReceiveUnitAsync(NewUnitRequest($"U-Q-NOVOL-{key}", productTypeId));
        var zero = await service.ReceiveUnitAsync(NewUnitRequest($"U-Q-ZERO-{key}", productTypeId));
        Assert.True(measured.Succeeded, measured.Error);
        Assert.True(unmeasured.Succeeded, unmeasured.Error);
        Assert.True(zero.Succeeded, zero.Error);

        var unit = (await context.BloodUnits.FindAsync(measured.Unit!.Id))!;
        unit.Volume = 150.5m;
        var zeroUnit = (await context.BloodUnits.FindAsync(zero.Unit!.Id))!;
        zeroUnit.Volume = 0m;
        await context.SaveChangesAsync();

        var list = await service.ListQuarantineAsync();
        var measuredRow = Assert.Single(list, row => row.UnitNumber == $"U-Q-VOL-{key}");
        var unmeasuredRow = Assert.Single(list, row => row.UnitNumber == $"U-Q-NOVOL-{key}");
        var zeroRow = Assert.Single(list, row => row.UnitNumber == $"U-Q-ZERO-{key}");

        Assert.Equal(150.5m, measuredRow.Volume);
        Assert.Equal("150.5 mL", measuredRow.VolumeLabel);
        Assert.Null(unmeasuredRow.Volume);
        Assert.Equal("Volume not recorded", unmeasuredRow.VolumeLabel);
        Assert.Equal(0m, zeroRow.Volume);
        Assert.Equal("0 mL", zeroRow.VolumeLabel);
    }

    [Fact]
    public async Task ListQuarantine_SurfacesRetypeMismatch()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-RETYPE", productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var test = new TestDefinition
        {
            Code = "ABORH-RETYPE-Q",
            Name = "Quarantine retype",
            Category = TestCategory.AboRhRetype,
            ResultValueType = ResultValueType.AboRh,
            IsActive = true,
            IsDraft = false,
            Version = 1
        };
        context.TestDefinitions.Add(test);
        await context.SaveChangesAsync();

        context.ProductRetypeResults.Add(new ProductRetypeResult
        {
            BloodProductId = received.Unit!.Id,
            TestDefinitionId = test.Id,
            TestCode = test.Code,
            Value = "A|Positive",
            InterpretedAbo = AboGroup.A,
            InterpretedRh = RhType.Positive,
            MatchesLabel = false,
            DiscrepancyDetail = "Labeled O Positive; interpreted A Positive.",
            Status = ResultStatus.Verified,
            EnteredBy = "tech1",
            EnteredUtc = _factory.Clock.UtcNow,
            VerifiedBy = "tech2",
            VerifiedUtc = _factory.Clock.UtcNow
        });
        await context.SaveChangesAsync();

        var row = Assert.Single(await service.ListQuarantineAsync(), i => i.UnitNumber == "U-Q-RETYPE");
        Assert.Equal("RBC-TEST", row.ProductCode);
        Assert.True(row.RetypeMismatch);
        Assert.Equal(AboGroup.A, row.RetypeInterpretedAbo);
        Assert.Equal(RhType.Positive, row.RetypeInterpretedRh);
    }

    [Fact]
    public async Task Quarantine_Unspecified_IsHardStop()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-NONE", productTypeId));
        Assert.True(received.Succeeded, received.Error);
        var released = await service.ReleaseFromQuarantineAsync(received.Unit!.Id, "tech2");
        Assert.True(released.Succeeded, released.Error);

        var result = await service.QuarantineAsync(received.Unit.Id, UnitQuarantineReason.Unspecified);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == QuarantineReasonRule.Code);
    }

    [Fact]
    public async Task ReceiveUnit_WithoutInventoryReceive_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .ReceiveUnitAsync(NewUnitRequest("U-RCV-PERM", productTypeId));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.ReceiveCode);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-RCV-PERM"));

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .ReceiveUnitAsync(NewUnitRequest("U-RCV-PERM", productTypeId));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("U-RCV-PERM", allowed.Unit!.UnitNumber);
    }

    [Fact]
    public async Task SaveBloodAttribute_WithoutInventoryReceive_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        long definitionId;
        await using (var setup = _factory.Create())
        {
            unitId = (await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-ATTR-PERM", productTypeId))).Unit!.Id;
            definitionId = await EnsureAttributeDefinitionAsync(setup);
        }

        await using var context = _factory.Create();
        var request = new SaveUnitBloodAttributeRequest(definitionId, BloodAttributeKind.Antigen, AntigenResult.Negative);

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .SaveBloodAttributeAsync(unitId, request);
        Assert.False(denied.Succeeded);
        Assert.Equal(InventoryAuthorizationRule.EvaluateSaveAttribute(false).Message, denied.Error);
        Assert.False(await context.UnitBloodAttributes.AnyAsync(a => a.BloodProductId == unitId));

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .SaveBloodAttributeAsync(unitId, request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(AntigenResult.Negative, allowed.Value!.Result);
        Assert.Equal(definitionId, allowed.Value.BloodAttributeDefinitionId);
        Assert.Equal(UnitAttributeTypingSource.InHouse, allowed.Value.TypingSource);
    }

    [Fact]
    public async Task BloodUnitDto_From_IncludesSupplierAndCollection()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-SUP-DTO", productTypeId) with
        {
            Supplier = "Regional Blood Center",
            CollectionFacility = "RBC-West"
        });
        Assert.True(received.Succeeded, received.Error);

        var dto = BloodUnitDto.From((await context.BloodUnits.FindAsync(received.Unit!.Id))!);
        Assert.Equal("Regional Blood Center", dto.Supplier);
        Assert.Equal("RBC-West", dto.CollectionFacility);
    }

    [Fact]
    public async Task SaveBloodAttribute_PersistsSupplierAndInHouseSources()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-ATTR-SRC", productTypeId))).Unit!.Id;
        var definitionId = await EnsureAttributeDefinitionAsync(context);

        var supplier = await CreateService(context).SaveBloodAttributeAsync(
            unitId,
            new SaveUnitBloodAttributeRequest(definitionId, BloodAttributeKind.Antigen, AntigenResult.Negative, UnitAttributeTypingSource.Supplier));
        Assert.True(supplier.Succeeded, supplier.Error);
        Assert.Equal(UnitAttributeTypingSource.Supplier, supplier.Value!.TypingSource);

        var inHouse = await CreateService(context).SaveBloodAttributeAsync(
            unitId,
            new SaveUnitBloodAttributeRequest(definitionId, BloodAttributeKind.Antigen, AntigenResult.Positive, UnitAttributeTypingSource.InHouse));
        Assert.True(inHouse.Succeeded, inHouse.Error);
        Assert.Equal(UnitAttributeTypingSource.InHouse, inHouse.Value!.TypingSource);
        Assert.Equal(AntigenResult.Positive, inHouse.Value.Result);

        var stored = await context.UnitBloodAttributes.SingleAsync(a => a.BloodProductId == unitId);
        Assert.Equal(UnitAttributeTypingSource.InHouse, stored.TypingSource);
    }

    [Fact]
    public async Task SaveBloodAttribute_RejectsTestResultSource()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-ATTR-TEST", productTypeId))).Unit!.Id;
        var definitionId = await EnsureAttributeDefinitionAsync(context);

        var result = await CreateService(context).SaveBloodAttributeAsync(
            unitId,
            new SaveUnitBloodAttributeRequest(definitionId, BloodAttributeKind.Antigen, AntigenResult.Negative, UnitAttributeTypingSource.TestResult));
        Assert.False(result.Succeeded);
        Assert.Contains("contributing tests", result.Error);
        Assert.False(await context.UnitBloodAttributes.AnyAsync(a => a.BloodProductId == unitId));
    }

    [Fact]
    public async Task ReleaseFromQuarantine_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive));
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-PERM", productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var released = await service.ReleaseFromQuarantineAsync(received.Unit!.Id, "tech2");
        Assert.False(released.Succeeded);
        Assert.Contains(released.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.QuarantineReleaseCode);
        Assert.Equal(UnitStatus.Quarantine, (await context.BloodUnits.FindAsync(received.Unit.Id))!.Status);

        var allowed = CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease));
        var ok = await allowed.ReleaseFromQuarantineAsync(received.Unit.Id, "tech2");
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal(UnitStatus.Available, ok.Unit!.Status);
    }

    [Fact]
    public async Task Quarantine_OtherWithoutNotes_IsHardStop()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-OTHER", productTypeId));
        Assert.True(received.Succeeded, received.Error);
        var released = await service.ReleaseFromQuarantineAsync(received.Unit!.Id, "tech2");
        Assert.True(released.Succeeded, released.Error);

        var result = await service.QuarantineAsync(received.Unit.Id, UnitQuarantineReason.Other, "  ");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == QuarantineReasonRule.Code);
    }

    [Fact]
    public async Task Quarantine_WithCode_ThenRelease_ClearsDisposition()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        var received = await service.ReceiveUnitAsync(NewUnitRequest("U-Q-CODE", productTypeId));
        Assert.True(received.Succeeded, received.Error);
        var released = await service.ReleaseFromQuarantineAsync(received.Unit!.Id, "tech2");
        Assert.True(released.Succeeded, released.Error);
        Assert.Equal(UnitQuarantineReason.Unspecified, released.Unit!.QuarantineReasonCode);

        var quarantined = await service.QuarantineAsync(
            received.Unit.Id, UnitQuarantineReason.LookbackRecall, "Donor notified");
        Assert.True(quarantined.Succeeded, quarantined.Error);
        Assert.Equal(UnitStatus.Quarantine, quarantined.Unit!.Status);
        Assert.Equal(UnitQuarantineReason.LookbackRecall, quarantined.Unit.QuarantineReasonCode);
        Assert.Equal("Donor notified", quarantined.Unit.QuarantineReason);

        var list = await service.ListQuarantineAsync();
        Assert.Contains(list, i => i.UnitNumber == "U-Q-CODE" && i.ReasonCode == UnitQuarantineReason.LookbackRecall);

        var available = await service.ReleaseFromQuarantineAsync(received.Unit.Id, "tech2");
        Assert.True(available.Succeeded, available.Error);
        Assert.Equal(UnitQuarantineReason.Unspecified, available.Unit!.QuarantineReasonCode);
        Assert.Null(available.Unit.QuarantineReason);
    }

    [Fact]
    public async Task ReceiveUnit_MissingProductCode_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-NOPDC", productTypeId, productCode: null));

        Assert.False(result.Succeeded);
        Assert.Contains(IsbtErrorCodes.UnknownProductCode, result.Error);
    }

    [Fact]
    public async Task ReceiveUnit_UnknownProductCode_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-BADPDC", productTypeId, productCode: "EXXXX"));

        Assert.False(result.Succeeded);
        Assert.Contains(IsbtErrorCodes.UnknownProductCode, result.Error);
    }

    [Fact]
    public async Task ReceiveUnit_EightCharProductCodeData_StoresComponents()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-8CHAR", productTypeId, productCode: "E0336000"));

        Assert.True(result.Succeeded);
        Assert.Equal("E0336", result.Unit!.ProductDescriptionCode);
        Assert.Equal("E0336000", result.Unit.ProductCodeData);
        Assert.Equal("0", result.Unit.CollectionTypeCode);
        Assert.Equal("00", result.Unit.DivisionCode);
    }

    [Fact]
    public async Task ReceiveUnit_DuplicateUnitNumber_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        await service.ReceiveUnitAsync(NewUnitRequest("U-DUP", productTypeId));

        var second = await service.ReceiveUnitAsync(NewUnitRequest("U-DUP", productTypeId));
        Assert.False(second.Succeeded);
        Assert.Contains("already exists", second.Error);
    }

    [Fact]
    public async Task ReceiveUnit_PastExpiration_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using var context = _factory.Create();
        var service = CreateService(context);
        var result = await service.ReceiveUnitAsync(
            NewUnitRequest("U-PASTEXP", productTypeId, _factory.Clock.UtcNow.AddHours(-1)));

        Assert.False(result.Succeeded);
        Assert.Contains("future", result.Error);
    }

    [Fact]
    public async Task ReceiveUnit_VisualFail_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-VISFAIL", productTypeId) with { VisualInspectionAcceptable = false });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveVisualInspectionRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-VISFAIL"));
    }

    [Fact]
    public async Task ReceiveUnit_HemolysisAppearance_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-HEMOLYSIS", productTypeId) with { Appearance = UnitAppearance.Hemolysis });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveAppearanceRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-HEMOLYSIS"));
    }

    [Fact]
    public async Task ReceiveUnit_VisualPass_StoresInspection()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-VISOK", productTypeId) with { VisualInspectionNotes = "Clear, no clots" });
        Assert.True(result.Succeeded);
        Assert.True(result.Unit!.ReceiveVisualAcceptable);
        Assert.Equal("Clear, no clots", result.Unit.ReceiveVisualNotes);
        Assert.Equal(UnitAppearance.Acceptable, result.Unit.ReceiveAppearance);
        Assert.Equal(4.0m, result.Unit.ReceiveTemperatureCelsius);
    }

    [Fact]
    public async Task ReceiveUnit_AutologousWithoutRecipient_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-AUTO-MISS", productTypeId) with { DonationRestriction = DonationRestriction.Autologous });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == AutologousDirectedRule.ReceiveCode);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-AUTO-MISS"));
    }

    [Fact]
    public async Task ReceiveUnit_AutologousWithRecipient_StoresRestriction()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long patientId;
        await using (var context = _factory.Create())
        {
            var patient = new Patient
            {
                MedicalRecordNumber = "MRN-AUTO-1",
                LastName = "Auto",
                FirstName = "Donor",
                DateOfBirth = new DateOnly(1980, 1, 1)
            };
            context.Patients.Add(patient);
            await context.SaveChangesAsync();
            patientId = patient.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReceiveUnitAsync(
            NewUnitRequest("U-AUTO-OK", productTypeId) with
            {
                DonationRestriction = DonationRestriction.Autologous,
                ReservedPatientId = patientId
            });
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(DonationRestriction.Autologous, result.Unit!.DonationRestriction);
        Assert.Equal(patientId, result.Unit.ReservedPatientId);
    }

    [Fact]
    public async Task ConvertDirected_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var (unitId, _) = await ReceiveDirectedAsync("U-DIR-NOREASON", productTypeId);
        await using var act = _factory.Create();
        var result = await CreateService(act).ConvertDirectedToAllogeneicAsync(unitId, "  ", "tech2");
        Assert.False(result.Succeeded);
        Assert.Equal("A reason is required to convert a directed unit to allogeneic inventory.", result.Error);
    }

    [Fact]
    public async Task ConvertDirected_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var (unitId, _) = await ReceiveDirectedAsync("U-DIR-PERM", productTypeId);
        await using var act = _factory.Create();
        var denied = CreateService(act, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive));
        var result = await denied.ConvertDirectedToAllogeneicAsync(
            unitId, "Intended recipient discharged", "tech2");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.DirectedConversionCode);
        Assert.Equal(DonationRestriction.Directed, (await act.BloodUnits.FindAsync(unitId))!.DonationRestriction);

        var allowed = CreateService(act, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease));
        var ok = await allowed.ConvertDirectedToAllogeneicAsync(
            unitId, "Intended recipient discharged", "tech2");
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal(DonationRestriction.Allogeneic, ok.Unit!.DonationRestriction);
        Assert.Null(ok.Unit.ReservedPatientId);
    }

    [Fact]
    public async Task ConvertDirected_WithoutVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var (unitId, _) = await ReceiveDirectedAsync("U-DIR-NO2", productTypeId);
        await using var act = _factory.Create();
        var result = await CreateService(act).ConvertDirectedToAllogeneicAsync(unitId, "Intended recipient discharged", null);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == DirectedConversionVerifierRule.Code);
    }

    [Fact]
    public async Task ConvertDirected_Autologous_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var patient = new Patient
            {
                MedicalRecordNumber = "MRN-DIR-AUTO",
                LastName = "Auto",
                FirstName = "Keep",
                DateOfBirth = new DateOnly(1975, 3, 3)
            };
            context.Patients.Add(patient);
            await context.SaveChangesAsync();
            var received = await CreateService(context).ReceiveUnitAsync(
                NewUnitRequest("U-DIR-AUTO", productTypeId) with
                {
                    DonationRestriction = DonationRestriction.Autologous,
                    ReservedPatientId = patient.Id
                });
            Assert.True(received.Succeeded, received.Error);
            unitId = received.Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ConvertDirectedToAllogeneicAsync(unitId, "Should not convert autologous", "tech2");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == AutologousDirectedRule.ConvertCode);
    }

    [Fact]
    public async Task ConvertDirected_FromQuarantine_ClearsReservation()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var unitNumber = $"U-DIR-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var (unitId, patientId) = await ReceiveDirectedAsync(unitNumber, productTypeId);
        await using var act = _factory.Create();
        var result = await CreateService(act).ConvertDirectedToAllogeneicAsync(
            unitId, "Intended recipient no longer needs the unit", "tech2");
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(DonationRestriction.Allogeneic, result.Unit!.DonationRestriction);
        Assert.Null(result.Unit.ReservedPatientId);
        Assert.Equal("Intended recipient no longer needs the unit", result.Unit.DirectedConversionReason);
        Assert.Equal("tech-test", result.Unit.DirectedConvertedBy);
        Assert.NotNull(result.Unit.DirectedConvertedUtc);
        Assert.NotEqual(patientId, result.Unit.ReservedPatientId);
        Assert.True(await act.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(BloodUnit)
            && a.EntityId == unitId
            && a.EventType == AuditEventType.ProductStatus));
    }

    [Fact]
    public async Task ConvertDirected_FromAllocated_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var (unitId, _) = await ReceiveDirectedAsync("U-DIR-ALLOC", productTypeId);
        await using (var setup = _factory.Create())
        {
            var unit = await setup.BloodUnits.FindAsync(unitId);
            unit!.Status = UnitStatus.Allocated;
            await setup.SaveChangesAsync();
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ConvertDirectedToAllogeneicAsync(unitId, "Still reserved", "tech2");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == AutologousDirectedRule.ConvertCode);
    }

    private async Task<(long UnitId, long PatientId)> ReceiveDirectedAsync(string unitNumber, long productTypeId)
    {
        await using var context = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{unitNumber}",
            LastName = "Directed",
            FirstName = "Recipient",
            DateOfBirth = new DateOnly(1988, 4, 4)
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var received = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest(unitNumber, productTypeId) with
            {
                DonationRestriction = DonationRestriction.Directed,
                ReservedPatientId = patient.Id
            });
        Assert.True(received.Succeeded, received.Error);
        return (received.Unit!.Id, patient.Id);
    }

    [Fact]
    public async Task ReceiveUnit_MissingTemperature_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-TEMP-MISS", productTypeId) with { ReceiveTemperatureCelsius = null });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveTemperatureRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-TEMP-MISS"));
    }

    [Fact]
    public async Task ReceiveUnit_TooCold_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-TEMP-COLD", productTypeId) with { ReceiveTemperatureCelsius = 0.0m });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveTemperatureRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-TEMP-COLD"));
    }

    [Fact]
    public async Task ReceiveUnit_TooWarm_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-TEMP-WARM", productTypeId) with { ReceiveTemperatureCelsius = 15.0m });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveTemperatureRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-TEMP-WARM"));
    }

    [Fact]
    public async Task ReceiveUnit_MissingSecondVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await SetReceiveVerifierPolicyAsync(true);
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-RCV-NO2", productTypeId) with { SecondVerifier = null });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveVerifierRule.Code);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-RCV-NO2"));
    }

    [Fact]
    public async Task ReceiveUnit_SameUserVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await SetReceiveVerifierPolicyAsync(true);
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-RCV-SELF", productTypeId) with { SecondVerifier = "tech-test" });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveVerifierRule.Code);
    }

    [Fact]
    public async Task ReceiveUnit_MissingSecondVerifier_SucceedsWhenNotRequired()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await SetReceiveVerifierPolicyAsync(false);
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-RCV-OPT", productTypeId) with { SecondVerifier = null });
        Assert.True(result.Succeeded);
        Assert.NotNull(await context.BloodUnits.FirstOrDefaultAsync(u => u.UnitNumber == "U-RCV-OPT"));
    }

    [Fact]
    public async Task ReceiveUnit_UnknownVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(
            NewUnitRequest("U-RCV-UNK", productTypeId) with { SecondVerifier = "not-a-user" });
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == SecondVerifierDirectoryRule.Code);
    }

    [Fact]
    public async Task ExpectUnit_CreatesExpected_WithoutVisualGate()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var result = await CreateService(context).ExpectUnitAsync(
            NewUnitRequest("U-EXPECT-1", productTypeId) with { ShipmentId = "ASN-77" });

        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.Expected, result.Unit!.Status);
        Assert.Equal("ASN-77", result.Unit.ShipmentId);
        Assert.Equal("E0206", result.Unit.ProductDescriptionCode);
        Assert.Equal(_factory.Clock.UtcNow.AddHours(24), result.Unit.ExpectedArrivalDueUtc);
        var history = await context.InventoryStatusHistory.Where(h => h.BloodProductId == result.Unit.Id).ToListAsync();
        var initial = Assert.Single(history);
        Assert.Equal(UnitStatus.Expected, initial.ToStatus);
        Assert.Contains("ASN-77", initial.Reason);
    }

    [Fact]
    public async Task ListExpected_FlagsOverdueWhenPastDue()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var original = _factory.Clock.UtcNow;
        try
        {
            await using (var context = _factory.Create())
            {
                var created = await CreateService(context).ExpectUnitAsync(
                    NewUnitRequest("U-EXPECT-OVERDUE", productTypeId) with { ShipmentId = "ASN-LATE" });
                Assert.True(created.Succeeded, created.Error);
            }

            _factory.Clock.UtcNow = original.AddHours(25);
            await using var act = _factory.Create();
            var list = await CreateService(act).ListExpectedAsync();
            var row = Assert.Single(list, i => i.UnitNumber == "U-EXPECT-OVERDUE");
            Assert.True(row.IsOverdue);
            Assert.Equal("ASN-LATE", row.ShipmentId);
            Assert.Equal(productTypeId, row.ProductTypeId);
        }
        finally
        {
            _factory.Clock.UtcNow = original;
        }
    }

    [Fact]
    public async Task ReceiveExpected_OutdatedUnit_DoesNotEnterStock()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var created = await CreateService(setup).ExpectUnitAsync(NewUnitRequest("U-EXPECT-OLD", productTypeId));
            Assert.True(created.Succeeded, created.Error);
            unitId = created.Unit!.Id;
            var unit = await setup.BloodUnits.SingleAsync(u => u.Id == unitId);
            unit.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
            await setup.SaveChangesAsync();
        }

        await using var act = _factory.Create();
        var service = CreateService(act);
        var row = Assert.Single(await service.ListExpectedAsync(), i => i.UnitNumber == "U-EXPECT-OLD");
        Assert.True(row.IsOutdated);

        var result = await service.ReceiveExpectedUnitAsync(
            unitId,
            new ReceiveExpectedUnitRequest(SecondVerifier: "tech2", ReceiveTemperatureCelsius: 4.0m));
        Assert.False(result.Succeeded);
        Assert.Contains("outdated", result.Error, StringComparison.OrdinalIgnoreCase);

        var stored = await act.BloodUnits.SingleAsync(u => u.Id == unitId);
        Assert.Equal(UnitStatus.Expected, stored.Status);
    }

    [Fact]
    public async Task ReceiveExpected_WhenOverdue_StillSucceedsAndAuditsLate()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var original = _factory.Clock.UtcNow;
        long unitId;
        try
        {
            await using (var context = _factory.Create())
            {
                unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-LATE-OK", productTypeId))).Unit!.Id;
            }

            _factory.Clock.UtcNow = original.AddHours(25);
            await using var act = _factory.Create();
            var result = await CreateService(act).ReceiveExpectedUnitAsync(
                unitId,
                new ReceiveExpectedUnitRequest(
                    SecondVerifier: "tech2",
                    ReceiveTemperatureCelsius: 4.0m));
            Assert.True(result.Succeeded, result.Error);
            Assert.NotEqual(UnitStatus.Expected, result.Unit!.Status);

            var history = await act.InventoryStatusHistory
                .Where(h => h.BloodProductId == unitId && h.ToStatus != UnitStatus.Expected)
                .ToListAsync();
            Assert.Contains(history, h => h.Reason != null && h.Reason.Contains("late arrival"));
        }
        finally
        {
            _factory.Clock.UtcNow = original;
        }
    }

    [Fact]
    public async Task ExpectUnit_DuplicateNumber_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);
        await service.ExpectUnitAsync(NewUnitRequest("U-EXPECT-DUP", productTypeId));
        var second = await service.ExpectUnitAsync(NewUnitRequest("U-EXPECT-DUP", productTypeId));
        Assert.False(second.Succeeded);
        Assert.Contains("already exists", second.Error);
    }

    [Fact]
    public async Task ReceiveExpected_VisualFail_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-VIS", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).ReceiveExpectedUnitAsync(
                unitId, new ReceiveExpectedUnitRequest(VisualInspectionAcceptable: false));
            Assert.False(result.Succeeded);
            Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveVisualInspectionRule.Code);
        }

        await using var verify = _factory.Create();
        var unit = await verify.BloodUnits.SingleAsync(u => u.Id == unitId);
        Assert.Equal(UnitStatus.Expected, unit.Status);
    }

    [Fact]
    public async Task ReceiveExpected_VisualPass_LandsInQuarantine()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-OK", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReceiveExpectedUnitAsync(
            unitId, new ReceiveExpectedUnitRequest(
                VisualInspectionNotes: "Bag intact",
                SecondVerifier: "tech2",
                ReceiveTemperatureCelsius: 4.0m));
        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.Quarantine, result.Unit!.Status);
        Assert.True(result.Unit.ReceiveVisualAcceptable);
        Assert.Equal("Bag intact", result.Unit.ReceiveVisualNotes);
        Assert.Equal(4.0m, result.Unit.ReceiveTemperatureCelsius);
    }

    [Fact]
    public async Task ReceiveExpected_MissingTemperature_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-TEMP", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReceiveExpectedUnitAsync(
            unitId, new ReceiveExpectedUnitRequest(SecondVerifier: "tech2"));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ReceiveTemperatureRule.Code);

        await using var verify = _factory.Create();
        var unit = await verify.BloodUnits.SingleAsync(u => u.Id == unitId);
        Assert.Equal(UnitStatus.Expected, unit.Status);
    }

    [Fact]
    public async Task ReceiveExpected_RetypeProduct_LandsInReceived()
    {
        await EnsureSecondVerifierAsync();
        await EnsureProductCodesAsync();
        long productTypeId;
        await using (var context = _factory.Create())
        {
            var (posId, negId) = await EnsureRhRetypeCatalogAsync(context);
            var type = new ProductType
            {
                ProductCode = "RBC-EXPECT-RETYPE",
                Name = "Expect Retype RBC",
                ComponentClass = ComponentClass.RedBloodCells,
                RequiresRetype = true,
                RhPositiveRetypeTestId = posId,
                RhNegativeRetypeTestId = negId
            };
            context.ProductTypes.Add(type);
            await context.SaveChangesAsync();
            productTypeId = type.Id;
        }

        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-RET", productTypeId))).Unit!.Id;
        }

        await using var receive = _factory.Create();
        var result = await CreateService(receive).ReceiveExpectedUnitAsync(
            unitId, new ReceiveExpectedUnitRequest(SecondVerifier: "tech2", ReceiveTemperatureCelsius: 4.0m));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(UnitStatus.Received, result.Unit!.Status);
        var pending = Assert.Single(await receive.ProductRetypeResults.Where(r => r.BloodProductId == result.Unit.Id).ToListAsync());
        Assert.Equal(ProductRetypeAssignment.RhPositiveTestCode, pending.TestCode);
        Assert.Equal(ResultStatus.Pending, pending.Status);
    }

    [Fact]
    public async Task CancelExpected_MovesToCancelledAssignment()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-EXPECT-CXL", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).CancelExpectedUnitAsync(unitId, "Supplier cancelled ASN");
        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.CancelledAssignment, result.Unit!.Status);
    }

    [Fact]
    public async Task ExpectUnit_WithoutInventoryReceive_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await using var context = _factory.Create();

        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .ExpectUnitAsync(NewUnitRequest("U-EXPECT-PERM", productTypeId));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.ExpectCode);
        Assert.False(await context.BloodUnits.AnyAsync(u => u.UnitNumber == "U-EXPECT-PERM"));

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .ExpectUnitAsync(NewUnitRequest("U-EXPECT-PERM", productTypeId));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Expected, allowed.Unit!.Status);
    }

    [Fact]
    public async Task CancelExpected_WithoutInventoryReceive_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            unitId = (await CreateService(setup).ExpectUnitAsync(NewUnitRequest("U-EXPECT-CXL-PERM", productTypeId))).Unit!.Id;
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .CancelExpectedUnitAsync(unitId, "Supplier cancelled ASN");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.CancelExpectedCode);
        Assert.Equal(UnitStatus.Expected, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .CancelExpectedUnitAsync(unitId, "Supplier cancelled ASN");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.CancelledAssignment, allowed.Unit!.Status);
    }

    [Fact]
    public async Task ReceiveExpected_NotExpected_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-WALKIN", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReceiveExpectedUnitAsync(unitId, new ReceiveExpectedUnitRequest());
        Assert.False(result.Succeeded);
        Assert.Contains("expected", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Release_QuarantineToAvailable_AppendsHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var received = await service.ReceiveUnitAsync(NewUnitRequest("U-REL", productTypeId));
            unitId = received.Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var result = await service.ReleaseFromQuarantineAsync(unitId, "tech2");
            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Available, result.Unit!.Status);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Equal(2, history.Count);
            Assert.Contains(history, h => h.FromStatus == UnitStatus.Quarantine && h.ToStatus == UnitStatus.Available);
            Assert.Contains(history, h => h.Reason != null && h.Reason.Contains("tech2"));
        }
    }

    [Fact]
    public async Task Release_WithoutSecondVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-REL-NO2", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var result = await CreateService(ctx).ReleaseFromQuarantineAsync(unitId);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == QuarantineReleaseVerifierRule.Code);
    }

    [Fact]
    public async Task Release_SameUserOrUnknownVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-REL-BAD2", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var service = CreateService(ctx);
        var same = await service.ReleaseFromQuarantineAsync(unitId, "tech-test");
        Assert.Contains(same.Evaluation!.HardStops, r => r.Code == QuarantineReleaseVerifierRule.Code);

        var unknown = await service.ReleaseFromQuarantineAsync(unitId, "not-a-user");
        Assert.Contains(unknown.Evaluation!.HardStops, r => r.Code == SecondVerifierDirectoryRule.Code);
    }

    [Fact]
    public async Task Hold_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-NR", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).HoldAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("hold reason is required", result.Error);
        }
    }

    [Fact]
    public async Task Hold_ThenRelease_ReturnsAvailable_AndClearsReason()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-REL", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
        }

        await using (var context = _factory.Create())
        {
            var held = await CreateService(context).HoldAsync(unitId, "Pending packing slip");
            Assert.True(held.Succeeded);
            Assert.Equal(UnitStatus.OnHold, held.Unit!.Status);
            Assert.Equal("Pending packing slip", held.Unit.HoldReason);
        }

        await using (var context = _factory.Create())
        {
            var released = await CreateService(context).ReleaseFromHoldAsync(unitId);
            Assert.True(released.Succeeded);
            Assert.Equal(UnitStatus.Available, released.Unit!.Status);
            Assert.Null(released.Unit.HoldReason);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Contains(history, h => h.FromStatus == UnitStatus.Available && h.ToStatus == UnitStatus.OnHold);
            Assert.Contains(history, h => h.FromStatus == UnitStatus.OnHold && h.ToStatus == UnitStatus.Available);
        }
    }

    [Fact]
    public async Task ReleaseFromHold_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-PERM", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
            await CreateService(context).HoldAsync(unitId, "Pending packing slip");
        }

        await using var act = _factory.Create();
        var denied = CreateService(act, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive));
        var result = await denied.ReleaseFromHoldAsync(unitId);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.HoldReleaseCode);
        Assert.Equal(UnitStatus.OnHold, (await act.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = CreateService(act, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease));
        var ok = await allowed.ReleaseFromHoldAsync(unitId);
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal(UnitStatus.Available, ok.Unit!.Status);
        Assert.Null(ok.Unit.HoldReason);
    }

    [Fact]
    public async Task Hold_FromQuarantine_IsBlocked()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-Q", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).HoldAsync(unitId, "Should not bypass quarantine");
            Assert.False(result.Succeeded);
            Assert.NotNull(result.Evaluation);
            Assert.True(result.Evaluation!.IsHardStopped);
        }
    }

    [Fact]
    public async Task ReleaseFromHold_WhenNotOnHold_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-HOLD-NA", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).ReleaseFromHoldAsync(unitId);
            Assert.False(result.Succeeded);
            Assert.Contains("operational hold", result.Error);
        }
    }

    [Fact]
    public async Task MarkMissing_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-MISS-NR", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).MarkMissingAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("reason is required", result.Error);
        }
    }

    [Fact]
    public async Task MarkMissing_ThenLocate_EntersQuarantine()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-MISS-LOC", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
        }

        await using (var context = _factory.Create())
        {
            var missing = await CreateService(context).MarkMissingAsync(unitId, "Not on shelf at physical inventory");
            Assert.True(missing.Succeeded);
            Assert.Equal(UnitStatus.Missing, missing.Unit!.Status);
            Assert.Equal("Not on shelf at physical inventory", missing.Unit.MissingReason);
        }

        await using (var context = _factory.Create())
        {
            var located = await CreateService(context).LocateMissingAsync(unitId);
            Assert.True(located.Succeeded);
            Assert.Equal(UnitStatus.Quarantine, located.Unit!.Status);
            Assert.Null(located.Unit.MissingReason);
            Assert.Contains("Located after missing", located.Unit.QuarantineReason);
            Assert.Equal(UnitQuarantineReason.LocatedAfterMissing, located.Unit.QuarantineReasonCode);
        }
    }

    [Fact]
    public async Task Locate_WhenNotMissing_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-MISS-NA", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).LocateMissingAsync(unitId);
            Assert.False(result.Succeeded);
            Assert.Contains("missing unit", result.Error);
        }
    }

    [Fact]
    public async Task LocateMissing_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-LOC-PERM", productTypeId));
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
            Assert.True((await CreateService(setup).MarkMissingAsync(unitId, "Not on shelf")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .LocateMissingAsync(unitId);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.LocateMissingCode);
        Assert.Equal(UnitStatus.Missing, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .LocateMissingAsync(unitId);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Quarantine, allowed.Unit!.Status);
    }

    [Fact]
    public async Task MarkDamaged_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DMG-NR", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).MarkDamagedAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("reason is required", result.Error);
        }
    }

    [Fact]
    public async Task MarkDamaged_ThenInspect_EntersQuarantine()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            var received = await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DMG-INSP", productTypeId));
            unitId = received.Unit!.Id;
            await CreateService(context).ReleaseFromQuarantineAsync(unitId, "tech2");
        }

        await using (var context = _factory.Create())
        {
            var damaged = await CreateService(context).MarkDamagedAsync(unitId, "Bag leaking in refrigerator");
            Assert.True(damaged.Succeeded);
            Assert.Equal(UnitStatus.Damaged, damaged.Unit!.Status);
            Assert.Equal("Bag leaking in refrigerator", damaged.Unit.DamagedReason);
        }

        await using (var context = _factory.Create())
        {
            var inspected = await CreateService(context).InspectDamagedAsync(unitId);
            Assert.True(inspected.Succeeded);
            Assert.Equal(UnitStatus.Quarantine, inspected.Unit!.Status);
            Assert.Null(inspected.Unit.DamagedReason);
            Assert.Contains("Inspected after damage", inspected.Unit.QuarantineReason);
            Assert.Equal(UnitQuarantineReason.InspectedAfterDamage, inspected.Unit.QuarantineReasonCode);
        }
    }

    [Fact]
    public async Task InspectDamaged_WhenNotDamaged_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DMG-NA", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).InspectDamagedAsync(unitId);
            Assert.False(result.Succeeded);
            Assert.Contains("damaged unit", result.Error);
        }
    }

    [Fact]
    public async Task InspectDamaged_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-INSP-PERM", productTypeId));
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
            Assert.True((await CreateService(setup).MarkDamagedAsync(unitId, "Leaking bag")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .InspectDamagedAsync(unitId);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.InspectDamagedCode);
        Assert.Equal(UnitStatus.Damaged, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .InspectDamagedAsync(unitId);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Quarantine, allowed.Unit!.Status);
    }

    [Fact]
    public async Task Quarantine_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-Q-SET-PERM", productTypeId));
            Assert.True(received.Succeeded, received.Error);
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .QuarantineAsync(unitId, UnitQuarantineReason.LookbackRecall, "Donor notified");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.QuarantineCode);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .QuarantineAsync(unitId, UnitQuarantineReason.LookbackRecall, "Donor notified");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Quarantine, allowed.Unit!.Status);
    }

    [Fact]
    public async Task Hold_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-HOLD-SET-PERM", productTypeId));
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .HoldAsync(unitId, "Pending packing slip");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.PlaceHoldCode);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .HoldAsync(unitId, "Pending packing slip");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.OnHold, allowed.Unit!.Status);
    }

    [Fact]
    public async Task MarkMissing_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-MISS-PERM", productTypeId));
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .MarkMissingAsync(unitId, "Not on shelf");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.MarkMissingCode);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .MarkMissingAsync(unitId, "Not on shelf");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Missing, allowed.Unit!.Status);
    }

    [Fact]
    public async Task MarkDamaged_WithoutInventoryRelease_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            var received = await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-DMG-PERM", productTypeId));
            unitId = received.Unit!.Id;
            Assert.True((await CreateService(setup).ReleaseFromQuarantineAsync(unitId, "tech2")).Succeeded);
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .MarkDamagedAsync(unitId, "Bag leaking");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.MarkDamagedCode);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .MarkDamagedAsync(unitId, "Bag leaking");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Damaged, allowed.Unit!.Status);
    }

    [Fact]
    public async Task ListDiscrepancy_IncludesMissingAndDamaged_ExcludesAvailable()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);

        var missing = await service.ReceiveUnitAsync(NewUnitRequest("U-DISC-MISS", productTypeId));
        Assert.True(missing.Succeeded, missing.Error);
        var releasedMissing = await service.ReleaseFromQuarantineAsync(missing.Unit!.Id, "tech2");
        Assert.True(releasedMissing.Succeeded, releasedMissing.Error);
        var markedMissing = await service.MarkMissingAsync(missing.Unit.Id, "Not on shelf");
        Assert.True(markedMissing.Succeeded, markedMissing.Error);

        var damaged = await service.ReceiveUnitAsync(NewUnitRequest("U-DISC-DMG", productTypeId));
        Assert.True(damaged.Succeeded, damaged.Error);
        var releasedDamaged = await service.ReleaseFromQuarantineAsync(damaged.Unit!.Id, "tech2");
        Assert.True(releasedDamaged.Succeeded, releasedDamaged.Error);
        var markedDamaged = await service.MarkDamagedAsync(damaged.Unit.Id, "Leaking bag");
        Assert.True(markedDamaged.Succeeded, markedDamaged.Error);

        var available = await service.ReceiveUnitAsync(NewUnitRequest("U-DISC-OK", productTypeId));
        Assert.True(available.Succeeded, available.Error);
        var releasedOk = await service.ReleaseFromQuarantineAsync(available.Unit!.Id, "tech2");
        Assert.True(releasedOk.Succeeded, releasedOk.Error);

        var list = await service.ListDiscrepancyAsync();
        Assert.Contains(list, i => i.UnitNumber == "U-DISC-MISS" && i.Status == UnitStatus.Missing && i.Reason == "Not on shelf" && i.ProductCode == "RBC-TEST");
        Assert.Contains(list, i => i.UnitNumber == "U-DISC-DMG" && i.Status == UnitStatus.Damaged && i.Reason == "Leaking bag" && i.ProductCode == "RBC-TEST");
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-DISC-OK");
    }

    [Fact]
    public async Task ListDiscrepancy_NamesTheStorageLocation()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var fridge = new InventoryLocation
        {
            Code = $"DISC-FR-{key}",
            Name = " Issue fridge ",
            LocationType = LocationType.Refrigerator
        };
        var blank = new InventoryLocation
        {
            Code = $"DISC-BL-{key}",
            Name = "   ",
            LocationType = LocationType.Refrigerator
        };
        context.InventoryLocations.AddRange(fridge, blank);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var named = await service.ReceiveUnitAsync(NewUnitRequest($"U-DISC-LOC-{key}", productTypeId));
        var unnamed = await service.ReceiveUnitAsync(NewUnitRequest($"U-DISC-NONE-{key}", productTypeId));
        var blankNamed = await service.ReceiveUnitAsync(NewUnitRequest($"U-DISC-BLANK-{key}", productTypeId));
        Assert.True(named.Succeeded, named.Error);
        Assert.True(unnamed.Succeeded, unnamed.Error);
        Assert.True(blankNamed.Succeeded, blankNamed.Error);
        Assert.True((await service.ReleaseFromQuarantineAsync(named.Unit!.Id, "tech2")).Succeeded);
        Assert.True((await service.ReleaseFromQuarantineAsync(unnamed.Unit!.Id, "tech2")).Succeeded);
        Assert.True((await service.ReleaseFromQuarantineAsync(blankNamed.Unit!.Id, "tech2")).Succeeded);
        Assert.True((await service.TransferAsync(named.Unit.Id, fridge.Id, "Move before it went missing")).Succeeded);
        Assert.True((await service.TransferAsync(blankNamed.Unit.Id, blank.Id, "Move to an unnamed place")).Succeeded);
        Assert.True((await service.MarkMissingAsync(named.Unit.Id, "Not on the named shelf")).Succeeded);
        Assert.True((await service.MarkMissingAsync(unnamed.Unit.Id, "Not on any shelf")).Succeeded);
        Assert.True((await service.MarkDamagedAsync(blankNamed.Unit.Id, "Leaking in an unnamed place")).Succeeded);

        var list = await service.ListDiscrepancyAsync();
        var namedRow = Assert.Single(list, row => row.UnitNumber == $"U-DISC-LOC-{key}");
        var unnamedRow = Assert.Single(list, row => row.UnitNumber == $"U-DISC-NONE-{key}");
        var blankRow = Assert.Single(list, row => row.UnitNumber == $"U-DISC-BLANK-{key}");

        Assert.Equal(fridge.Id, namedRow.CurrentLocationId);
        Assert.Equal("Issue fridge", namedRow.LocationLabel);
        Assert.Null(unnamedRow.CurrentLocationId);
        Assert.Equal(StorageLocationLabel.None, unnamedRow.LocationLabel);
        Assert.Equal(blank.Id, blankRow.CurrentLocationId);
        Assert.Equal($"Location {blank.Id}", blankRow.LocationLabel);
    }

    [Fact]
    public async Task ListOnHold_SurfacesProductAndHoldReason()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        await using var context = _factory.Create();
        var service = CreateService(context);

        var held = await service.ReceiveUnitAsync(NewUnitRequest("U-HOLD-BOARD", productTypeId));
        Assert.True(held.Succeeded, held.Error);
        var released = await service.ReleaseFromQuarantineAsync(held.Unit!.Id, "tech2");
        Assert.True(released.Succeeded, released.Error);
        var onHold = await service.HoldAsync(held.Unit.Id, "Pending packing slip");
        Assert.True(onHold.Succeeded, onHold.Error);

        var available = await service.ReceiveUnitAsync(NewUnitRequest("U-HOLD-OK", productTypeId));
        Assert.True(available.Succeeded, available.Error);
        var releasedOk = await service.ReleaseFromQuarantineAsync(available.Unit!.Id, "tech2");
        Assert.True(releasedOk.Succeeded, releasedOk.Error);

        var list = await service.ListOnHoldAsync();
        var row = Assert.Single(list, i => i.UnitNumber == "U-HOLD-BOARD");
        Assert.Equal("RBC-TEST", row.ProductCode);
        Assert.Equal("Pending packing slip", row.HoldReason);
        Assert.DoesNotContain(list, i => i.UnitNumber == "U-HOLD-OK");

        var afterRelease = await service.ReleaseFromHoldAsync(held.Unit.Id);
        Assert.True(afterRelease.Succeeded, afterRelease.Error);
        Assert.DoesNotContain(await service.ListOnHoldAsync(), i => i.UnitNumber == "U-HOLD-BOARD");
    }

    [Fact]
    public async Task ListOnHold_NamesTheStorageLocation()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var fridge = new InventoryLocation
        {
            Code = $"HOLD-FR-{key}",
            Name = " Paperwork fridge ",
            LocationType = LocationType.Refrigerator
        };
        var blank = new InventoryLocation
        {
            Code = $"HOLD-BL-{key}",
            Name = "   ",
            LocationType = LocationType.Refrigerator
        };
        context.InventoryLocations.AddRange(fridge, blank);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var named = await service.ReceiveUnitAsync(NewUnitRequest($"U-HOLD-LOC-{key}", productTypeId));
        var unnamed = await service.ReceiveUnitAsync(NewUnitRequest($"U-HOLD-NONE-{key}", productTypeId));
        var blankNamed = await service.ReceiveUnitAsync(NewUnitRequest($"U-HOLD-BLANK-{key}", productTypeId));
        Assert.True(named.Succeeded, named.Error);
        Assert.True(unnamed.Succeeded, unnamed.Error);
        Assert.True(blankNamed.Succeeded, blankNamed.Error);
        Assert.True((await service.ReleaseFromQuarantineAsync(named.Unit!.Id, "tech2")).Succeeded);
        Assert.True((await service.ReleaseFromQuarantineAsync(unnamed.Unit!.Id, "tech2")).Succeeded);
        Assert.True((await service.ReleaseFromQuarantineAsync(blankNamed.Unit!.Id, "tech2")).Succeeded);
        Assert.True((await service.TransferAsync(named.Unit.Id, fridge.Id, "Move before hold")).Succeeded);
        Assert.True((await service.TransferAsync(blankNamed.Unit.Id, blank.Id, "Move to an unnamed place")).Succeeded);
        Assert.True((await service.HoldAsync(named.Unit.Id, "Pending packing slip")).Succeeded);
        Assert.True((await service.HoldAsync(unnamed.Unit.Id, "Pending review")).Succeeded);
        Assert.True((await service.HoldAsync(blankNamed.Unit.Id, "Pending paperwork")).Succeeded);

        var list = await service.ListOnHoldAsync();
        var namedRow = Assert.Single(list, row => row.UnitNumber == $"U-HOLD-LOC-{key}");
        var unnamedRow = Assert.Single(list, row => row.UnitNumber == $"U-HOLD-NONE-{key}");
        var blankRow = Assert.Single(list, row => row.UnitNumber == $"U-HOLD-BLANK-{key}");

        Assert.Equal(fridge.Id, namedRow.CurrentLocationId);
        Assert.Equal("Paperwork fridge", namedRow.LocationLabel);
        Assert.Null(unnamedRow.CurrentLocationId);
        Assert.Equal(StorageLocationLabel.None, unnamedRow.LocationLabel);
        Assert.Equal(blank.Id, blankRow.CurrentLocationId);
        Assert.Equal($"Location {blank.Id}", blankRow.LocationLabel);
    }

    [Fact]
    public async Task ReturnToSupplier_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-RTS-NR", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).ReturnToSupplierAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("reason is required", result.Error);
        }
    }

    [Fact]
    public async Task ReturnToSupplier_FromExpected_IsTerminal()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ExpectUnitAsync(NewUnitRequest("U-RTS-EXP", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReturnToSupplierAsync(unitId, "Hemolysis at consignee receipt");
        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.ReturnedToSupplier, result.Unit!.Status);
        Assert.Equal("Hemolysis at consignee receipt", result.Unit.SupplierReturnReason);
    }

    [Fact]
    public async Task ReturnToSupplier_FromQuarantine_StoresReason()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-RTS-Q", productTypeId))).Unit!.Id;
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReturnToSupplierAsync(unitId, "Unused stock credit");
        Assert.True(result.Succeeded);
        Assert.Equal(UnitStatus.ReturnedToSupplier, result.Unit!.Status);
        Assert.Equal("Unused stock credit", result.Unit.SupplierReturnReason);
    }

    [Fact]
    public async Task ReturnToSupplier_WithoutInventoryReceive_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var setup = _factory.Create())
        {
            unitId = (await CreateService(setup).ReceiveUnitAsync(NewUnitRequest("U-RTS-PERM", productTypeId))).Unit!.Id;
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .ReturnToSupplierAsync(unitId, "Unused stock credit");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.ReturnToSupplierCode);
        Assert.NotEqual(UnitStatus.ReturnedToSupplier, (await context.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .ReturnToSupplierAsync(unitId, "Unused stock credit");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.ReturnedToSupplier, allowed.Unit!.Status);
    }

    [Fact]
    public async Task ReturnToSupplier_FromIssued_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-RTS-ISS", productTypeId))).Unit!.Id;
            var unit = await context.BloodUnits.SingleAsync(u => u.Id == unitId);
            unit.Status = UnitStatus.Issued;
            await context.SaveChangesAsync();
        }

        await using var act = _factory.Create();
        var result = await CreateService(act).ReturnToSupplierAsync(unitId, "Should not leave the ward this way");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == InventoryStatusTransition.IllegalTransitionCode);
    }

    [Fact]
    public async Task Discard_WithoutReason_Fails()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC-NR", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "  ");
            Assert.False(result.Succeeded);
            Assert.Contains("reason is required", result.Error);
        }
    }

    [Fact]
    public async Task Discard_WithoutInventoryDiscard_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC-PERM", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var denied = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .DiscardAsync(unitId, "Bag integrity compromised", "tech2");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.DiscardCode);
        Assert.NotEqual(UnitStatus.Discarded, (await ctx.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryDiscard))
            .DiscardAsync(unitId, "Bag integrity compromised", "tech2");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Discarded, allowed.Unit!.Status);
    }

    [Fact]
    public async Task Discard_SetsStatus_AndWritesDiscardAudit()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "Bag integrity compromised", "tech2");
            Assert.True(result.Succeeded);
            Assert.Equal(UnitStatus.Discarded, result.Unit!.Status);
        }

        await using (var verify = _factory.Create())
        {
            var discardAudit = await verify.AuditEvents
                .Where(a => a.EntityType == nameof(BloodUnit) && a.EntityId == unitId && a.EventType == AuditEventType.Discard)
                .SingleAsync();
            Assert.Contains("Bag integrity compromised", discardAudit.Reason);
            Assert.Contains("tech2", discardAudit.Reason);
        }
    }

    [Fact]
    public async Task Discard_WithoutSecondVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC-NO2", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var result = await CreateService(ctx).DiscardAsync(unitId, "Bag integrity compromised");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == DiscardVerifierRule.Code);
    }

    [Fact]
    public async Task Discard_SameUserOrUnknownVerifier_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        await EnsureSecondVerifierAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-DISC-BAD2", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var service = CreateService(ctx);
        var same = await service.DiscardAsync(unitId, "attempt", "tech-test");
        Assert.Contains(same.Evaluation!.HardStops, r => r.Code == DiscardVerifierRule.Code);

        var unknown = await service.DiscardAsync(unitId, "attempt", "not-a-user");
        Assert.Contains(unknown.Evaluation!.HardStops, r => r.Code == SecondVerifierDirectoryRule.Code);
    }

    [Fact]
    public async Task Discard_TransfusedUnit_IsBlockedByTransitionGuard()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;

        await using (var context = _factory.Create())
        {
            var unit = new BloodUnit
            {
                UnitNumber = "U-TX",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
                Status = UnitStatus.Transfused
            };
            context.BloodUnits.Add(unit);
            await context.SaveChangesAsync();
            unitId = unit.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).DiscardAsync(unitId, "attempt");
            Assert.False(result.Succeeded);
            Assert.NotNull(result.Evaluation);
            Assert.True(result.Evaluation!.IsHardStopped);
        }
    }

    [Fact]
    public async Task Transfer_ChangesLocation_AndAppendsHistory()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        long toLocationId;

        await using (var context = _factory.Create())
        {
            var location = new InventoryLocation { Code = "LOC-XFER", Name = "Transfer Target", LocationType = LocationType.Refrigerator };
            context.InventoryLocations.Add(location);
            await context.SaveChangesAsync();
            toLocationId = location.Id;

            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-XFER", productTypeId))).Unit!.Id;
        }

        await using (var context = _factory.Create())
        {
            var result = await CreateService(context).TransferAsync(unitId, toLocationId, "Move to issue fridge");
            Assert.True(result.Succeeded);
            Assert.Equal(toLocationId, result.Unit!.CurrentLocationId);
        }

        await using (var verify = _factory.Create())
        {
            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == unitId).ToListAsync();
            Assert.Contains(history, h => h.ToLocationId == toLocationId && h.FromStatus == h.ToStatus);
        }
    }

    [Fact]
    public async Task Transfer_WithoutInventoryTransfer_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        long toLocationId;

        await using (var context = _factory.Create())
        {
            var location = new InventoryLocation { Code = "LOC-XFER-PERM", Name = "Transfer Target", LocationType = LocationType.Refrigerator };
            context.InventoryLocations.Add(location);
            await context.SaveChangesAsync();
            toLocationId = location.Id;
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-XFER-PERM", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var priorLocation = (await ctx.BloodUnits.FindAsync(unitId))!.CurrentLocationId;
        var denied = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .TransferAsync(unitId, toLocationId, "Move to issue fridge");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.TransferCode);
        Assert.Equal(priorLocation, (await ctx.BloodUnits.FindAsync(unitId))!.CurrentLocationId);

        var allowed = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryTransfer))
            .TransferAsync(unitId, toLocationId, "Move to issue fridge");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(toLocationId, allowed.Unit!.CurrentLocationId);
    }

    [Fact]
    public async Task Recall_WithoutInventoryRecall_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-RCL-PERM", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var denied = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryReceive))
            .RecallAsync(unitId, "Donor subsequently reactive");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.RecallCode);
        Assert.NotEqual(UnitStatus.Recalled, (await ctx.BloodUnits.FindAsync(unitId))!.Status);

        var allowed = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRecall))
            .RecallAsync(unitId, "Donor subsequently reactive");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(UnitStatus.Recalled, allowed.Unit!.Status);
    }

    [Fact]
    public async Task RecallForLookback_DoesNotRequireInventoryRecall()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long unitId;
        await using (var context = _factory.Create())
        {
            unitId = (await CreateService(context).ReceiveUnitAsync(NewUnitRequest("U-RCL-LB", productTypeId))).Unit!.Id;
        }

        await using var ctx = _factory.Create();
        var result = await CreateService(ctx, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .RecallForLookbackAsync(unitId, "Donor subsequently reactive");
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(UnitStatus.Recalled, result.Unit!.Status);
    }

    [Fact]
    public async Task ExpireDueUnits_MovesOnlyPastDueUnits()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long pastDueId;
        long futureId;

        await using (var context = _factory.Create())
        {
            var pastDue = new BloodUnit
            {
                UnitNumber = "U-PASTDUE",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = UnitStatus.Available
            };
            var future = new BloodUnit
            {
                UnitNumber = "U-FUTURE",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(5),
                Status = UnitStatus.Available
            };
            context.BloodUnits.AddRange(pastDue, future);
            await context.SaveChangesAsync();
            pastDueId = pastDue.Id;
            futureId = future.Id;
        }

        await using (var context = _factory.Create())
        {
            var expired = await CreateService(context).ExpireDueUnitsAsync();
            Assert.True(expired.Succeeded, expired.Error);
            Assert.True(expired.AffectedCount >= 1);
        }

        await using (var verify = _factory.Create())
        {
            Assert.Equal(UnitStatus.Expired, (await verify.BloodUnits.FindAsync(pastDueId))!.Status);
            Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.FindAsync(futureId))!.Status);
        }
    }

    [Fact]
    public async Task ExpireDueUnits_WithoutInventoryDiscard_IsHardStopped()
    {
        var productTypeId = await EnsureProductTypeAsync();
        long pastDueId;
        await using (var setup = _factory.Create())
        {
            var pastDue = new BloodUnit
            {
                UnitNumber = "U-EXP-PERM",
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1),
                Status = UnitStatus.Available
            };
            setup.BloodUnits.Add(pastDue);
            await setup.SaveChangesAsync();
            pastDueId = pastDue.Id;
        }

        await using var context = _factory.Create();
        var denied = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryRelease))
            .ExpireDueUnitsAsync();
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == InventoryAuthorizationRule.ExpireCode);
        Assert.Equal(UnitStatus.Available, (await context.BloodUnits.FindAsync(pastDueId))!.Status);

        var allowed = await CreateService(context, new FixedPermissionEvaluator(1, PermissionCodes.InventoryDiscard))
            .ExpireDueUnitsAsync();
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.True(allowed.AffectedCount >= 1);
        Assert.Equal(UnitStatus.Expired, (await context.BloodUnits.FindAsync(pastDueId))!.Status);
    }

    [Fact]
    public async Task Search_FiltersByStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            await service.ReceiveUnitAsync(NewUnitRequest("U-SEARCH-Q", productTypeId)); // stays Quarantine
        }

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var quarantined = await service.SearchAsync(new InventorySearchCriteria(Status: UnitStatus.Quarantine));
            Assert.Contains(quarantined, u => u.UnitNumber == "U-SEARCH-Q");
            Assert.All(quarantined, u => Assert.Equal(UnitStatus.Quarantine, u.Status));
        }
    }

    [Fact]
    public async Task Search_UnitNumber_MatchesContains()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var unitNumber = $"U-CONTAINS-{key}";

        await using (var context = _factory.Create())
        {
            context.BloodUnits.Add(new BloodUnit
            {
                UnitNumber = unitNumber,
                ProductTypeId = productTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(30),
                Status = UnitStatus.Available
            });
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var fragment = await service.SearchAsync(new InventorySearchCriteria(UnitNumber: key.ToLowerInvariant()));
            Assert.Contains(fragment, u => u.UnitNumber == unitNumber);

            var miss = await service.SearchAsync(new InventorySearchCriteria(UnitNumber: $"ZZZ-{key}"));
            Assert.DoesNotContain(miss, u => u.UnitNumber == unitNumber);
        }
    }

    [Fact]
    public async Task Search_FiltersByStatuses()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var availableNumber = $"U-MULTI-A-{key}";
        var quarantineNumber = $"U-MULTI-Q-{key}";
        var issuedNumber = $"U-MULTI-I-{key}";

        await using (var context = _factory.Create())
        {
            context.BloodUnits.AddRange(
                new BloodUnit
                {
                    UnitNumber = availableNumber,
                    ProductTypeId = productTypeId,
                    Abo = AboGroup.O,
                    RhD = RhType.Positive,
                    ExpiresUtc = _factory.Clock.UtcNow.AddDays(30),
                    Status = UnitStatus.Available
                },
                new BloodUnit
                {
                    UnitNumber = quarantineNumber,
                    ProductTypeId = productTypeId,
                    Abo = AboGroup.O,
                    RhD = RhType.Positive,
                    ExpiresUtc = _factory.Clock.UtcNow.AddDays(30),
                    Status = UnitStatus.Quarantine
                },
                new BloodUnit
                {
                    UnitNumber = issuedNumber,
                    ProductTypeId = productTypeId,
                    Abo = AboGroup.O,
                    RhD = RhType.Positive,
                    ExpiresUtc = _factory.Clock.UtcNow.AddDays(30),
                    Status = UnitStatus.Issued
                });
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            var service = CreateService(context);
            var rows = await service.SearchAsync(new InventorySearchCriteria(
                Statuses: [UnitStatus.Available, UnitStatus.Quarantine]));
            var numbers = rows.Select(u => u.UnitNumber).ToHashSet();
            Assert.Contains(availableNumber, numbers);
            Assert.Contains(quarantineNumber, numbers);
            Assert.DoesNotContain(issuedNumber, numbers);
        }
    }

    [Fact]
    public async Task ReceiveUnit_RequiresRetype_CreatesReceivedUnit()
    {
        await EnsureSecondVerifierAsync();
        await EnsureProductCodesAsync();
        long productTypeId;
        await using (var context = _factory.Create())
        {
            var (posId, negId) = await EnsureRhRetypeCatalogAsync(context);
            var type = new ProductType
            {
                ProductCode = "RBC-RETYPE",
                Name = "Retype RBC",
                ComponentClass = ComponentClass.RedBloodCells,
                RequiresRetype = true,
                RhPositiveRetypeTestId = posId,
                RhNegativeRetypeTestId = negId
            };
            context.ProductTypes.Add(type);
            await context.SaveChangesAsync();
            productTypeId = type.Id;
        }

        await using var receive = _factory.Create();
        var service = CreateService(receive);
        var result = await service.ReceiveUnitAsync(NewUnitRequest("U-RETYPE-1", productTypeId));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(UnitStatus.Received, result.Unit!.Status);
        var history = await receive.InventoryStatusHistory.Where(h => h.BloodProductId == result.Unit.Id).ToListAsync();
        var initial = Assert.Single(history);
        Assert.Equal(UnitStatus.Received, initial.ToStatus);
        Assert.Contains("retype", initial.Reason, StringComparison.OrdinalIgnoreCase);
        var pending = Assert.Single(await receive.ProductRetypeResults.Where(r => r.BloodProductId == result.Unit.Id).ToListAsync());
        Assert.Equal(ResultStatus.Pending, pending.Status);
        Assert.Equal(ProductRetypeAssignment.RhPositiveTestCode, pending.TestCode);
    }

    [Fact]
    public async Task ReceiveUnit_RequiresRetype_RhNegative_AddsNegativeTest()
    {
        await EnsureSecondVerifierAsync();
        await EnsureProductCodesAsync();
        long productTypeId;
        await using (var context = _factory.Create())
        {
            var (posId, negId) = await EnsureRhRetypeCatalogAsync(context);
            var type = new ProductType
            {
                ProductCode = "RBC-RETYPE-NEG",
                Name = "Retype RBC Neg",
                ComponentClass = ComponentClass.RedBloodCells,
                RequiresRetype = true,
                RhPositiveRetypeTestId = posId,
                RhNegativeRetypeTestId = negId
            };
            context.ProductTypes.Add(type);
            await context.SaveChangesAsync();
            productTypeId = type.Id;
        }

        await using var receive = _factory.Create();
        var result = await CreateService(receive).ReceiveUnitAsync(
            NewUnitRequest("U-RETYPE-NEG", productTypeId) with { RhD = RhType.Negative, Abo = AboGroup.A });

        Assert.True(result.Succeeded, result.Error);
        var pending = Assert.Single(await receive.ProductRetypeResults.Where(r => r.BloodProductId == result.Unit!.Id).ToListAsync());
        Assert.Equal(ProductRetypeAssignment.RhNegativeTestCode, pending.TestCode);
        Assert.Equal(ResultStatus.Pending, pending.Status);
    }

    [Fact]
    public async Task ReceiveUnit_RequiresRetype_MissingAssignment_Fails()
    {
        await EnsureSecondVerifierAsync();
        await EnsureProductCodesAsync();
        long productTypeId;
        await using (var context = _factory.Create())
        {
            var type = new ProductType
            {
                ProductCode = "RBC-RETYPE-BARE",
                Name = "Retype without tests",
                ComponentClass = ComponentClass.RedBloodCells,
                RequiresRetype = true
            };
            context.ProductTypes.Add(type);
            await context.SaveChangesAsync();
            productTypeId = type.Id;
        }

        await using var receive = _factory.Create();
        var result = await CreateService(receive).ReceiveUnitAsync(NewUnitRequest("U-RETYPE-BARE", productTypeId));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ProductRetypeAssignment.MissingTestCode);
        Assert.False(await receive.BloodUnits.AnyAsync(u => u.UnitNumber == "U-RETYPE-BARE"));
    }

    [Fact]
    public async Task ReceiveUnit_WritesProductStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var unitNumber = $"U-RCV-AUD-{key}";
        await using var context = _factory.Create();
        var result = await CreateService(context).ReceiveUnitAsync(NewUnitRequest(unitNumber, productTypeId));
        Assert.True(result.Succeeded, result.Error);
        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == result.Unit!.Id
            && e.Reason == "Unit received."
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(unitNumber));
    }

    [Fact]
    public async Task ExpectReceiveAndCancel_WriteProductStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var arriveNumber = $"U-EXP-ARR-{key}";
        var cancelNumber = $"U-EXP-CXL-{key}";
        await using var context = _factory.Create();
        var svc = CreateService(context);

        var expected = await svc.ExpectUnitAsync(
            NewUnitRequest(arriveNumber, productTypeId) with { ShipmentId = $"ASN-{key}" });
        Assert.True(expected.Succeeded, expected.Error);
        var pendingCancel = await svc.ExpectUnitAsync(NewUnitRequest(cancelNumber, productTypeId));
        Assert.True(pendingCancel.Succeeded, pendingCancel.Error);

        var arrived = await svc.ReceiveExpectedUnitAsync(
            expected.Unit!.Id,
            new ReceiveExpectedUnitRequest(SecondVerifier: "tech2", ReceiveTemperatureCelsius: 4.0m));
        Assert.True(arrived.Succeeded, arrived.Error);

        var cancelled = await svc.CancelExpectedUnitAsync(pendingCancel.Unit!.Id, "Supplier cancelled ASN");
        Assert.True(cancelled.Succeeded, cancelled.Error);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityId == expected.Unit.Id
            && e.Reason == "Expected inbound unit recorded."
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(arriveNumber));
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityId == expected.Unit.Id
            && e.Reason == "Expected inbound unit received."
            && e.OldValueJson is not null
            && e.NewValueJson is not null);
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityId == pendingCancel.Unit.Id
            && e.Reason == "Expected inbound unit cancelled."
            && e.OldValueJson is not null
            && e.NewValueJson is not null);
    }

    [Fact]
    public async Task ReleaseFromQuarantineAndHold_WriteProductStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var qNumber = $"U-REL-Q-{key}";
        var hNumber = $"U-REL-H-{key}";
        await using var context = _factory.Create();
        var svc = CreateService(context);

        var quarantined = await svc.ReceiveUnitAsync(NewUnitRequest(qNumber, productTypeId));
        Assert.True(quarantined.Succeeded, quarantined.Error);
        var fromQuarantine = await svc.ReleaseFromQuarantineAsync(quarantined.Unit!.Id, "tech2");
        Assert.True(fromQuarantine.Succeeded, fromQuarantine.Error);

        var forHold = await svc.ReceiveUnitAsync(NewUnitRequest(hNumber, productTypeId));
        Assert.True(forHold.Succeeded, forHold.Error);
        Assert.True((await svc.ReleaseFromQuarantineAsync(forHold.Unit!.Id, "tech2")).Succeeded);
        Assert.True((await svc.HoldAsync(forHold.Unit.Id, "Pending paperwork")).Succeeded);
        var fromHold = await svc.ReleaseFromHoldAsync(forHold.Unit.Id);
        Assert.True(fromHold.Succeeded, fromHold.Error);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityId == quarantined.Unit.Id
            && e.Reason == "Unit released from quarantine."
            && e.OldValueJson is not null
            && e.NewValueJson is not null);
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityId == forHold.Unit.Id
            && e.Reason == "Unit released from hold."
            && e.OldValueJson is not null
            && e.NewValueJson is not null);
    }

    [Fact]
    public async Task Recall_WritesProductStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var unitNumber = $"U-RCL-AUD-{key}";
        await using var context = _factory.Create();
        var svc = CreateService(context);
        var received = await svc.ReceiveUnitAsync(NewUnitRequest(unitNumber, productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var recalled = await svc.RecallAsync(received.Unit!.Id, "Donor subsequently reactive");
        Assert.True(recalled.Succeeded, recalled.Error);
        Assert.Equal(UnitStatus.Recalled, recalled.Unit!.Status);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == received.Unit.Id
            && e.Reason == "Unit recalled."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(unitNumber));
    }

    [Fact]
    public async Task QuarantineAndHold_WriteProductStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var qNumber = $"U-PLC-Q-{key}";
        var hNumber = $"U-PLC-H-{key}";
        await using var context = _factory.Create();
        var svc = CreateService(context);

        var forQuarantine = await svc.ReceiveUnitAsync(NewUnitRequest(qNumber, productTypeId));
        Assert.True(forQuarantine.Succeeded, forQuarantine.Error);
        Assert.True((await svc.ReleaseFromQuarantineAsync(forQuarantine.Unit!.Id, "tech2")).Succeeded);
        var quarantined = await svc.QuarantineAsync(
            forQuarantine.Unit.Id, UnitQuarantineReason.LookbackRecall, "Donor notified");
        Assert.True(quarantined.Succeeded, quarantined.Error);
        Assert.Equal(UnitStatus.Quarantine, quarantined.Unit!.Status);

        var forHold = await svc.ReceiveUnitAsync(NewUnitRequest(hNumber, productTypeId));
        Assert.True(forHold.Succeeded, forHold.Error);
        Assert.True((await svc.ReleaseFromQuarantineAsync(forHold.Unit!.Id, "tech2")).Succeeded);
        var held = await svc.HoldAsync(forHold.Unit.Id, "Pending paperwork");
        Assert.True(held.Succeeded, held.Error);
        Assert.Equal(UnitStatus.OnHold, held.Unit!.Status);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == forQuarantine.Unit.Id
            && e.Reason == "Unit placed in quarantine."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(qNumber));
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == forHold.Unit.Id
            && e.Reason == "Unit placed on hold."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(hNumber));
    }

    [Fact]
    public async Task MarkMissingAndDamaged_WriteProductStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var mNumber = $"U-MIS-AUD-{key}";
        var dNumber = $"U-DMG-AUD-{key}";
        await using var context = _factory.Create();
        var svc = CreateService(context);

        var forMissing = await svc.ReceiveUnitAsync(NewUnitRequest(mNumber, productTypeId));
        Assert.True(forMissing.Succeeded, forMissing.Error);
        Assert.True((await svc.ReleaseFromQuarantineAsync(forMissing.Unit!.Id, "tech2")).Succeeded);
        var missing = await svc.MarkMissingAsync(forMissing.Unit.Id, "Not on shelf at physical inventory");
        Assert.True(missing.Succeeded, missing.Error);
        Assert.Equal(UnitStatus.Missing, missing.Unit!.Status);

        var forDamaged = await svc.ReceiveUnitAsync(NewUnitRequest(dNumber, productTypeId));
        Assert.True(forDamaged.Succeeded, forDamaged.Error);
        Assert.True((await svc.ReleaseFromQuarantineAsync(forDamaged.Unit!.Id, "tech2")).Succeeded);
        var damaged = await svc.MarkDamagedAsync(forDamaged.Unit.Id, "Bag leaking in refrigerator");
        Assert.True(damaged.Succeeded, damaged.Error);
        Assert.Equal(UnitStatus.Damaged, damaged.Unit!.Status);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == forMissing.Unit.Id
            && e.Reason == "Unit marked missing."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(mNumber));
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == forDamaged.Unit.Id
            && e.Reason == "Unit marked damaged."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(dNumber));
    }

    [Fact]
    public async Task LocateAndInspect_WriteProductStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var mNumber = $"U-LOC-AUD-{key}";
        var dNumber = $"U-INS-AUD-{key}";
        await using var context = _factory.Create();
        var svc = CreateService(context);

        var forMissing = await svc.ReceiveUnitAsync(NewUnitRequest(mNumber, productTypeId));
        Assert.True(forMissing.Succeeded, forMissing.Error);
        Assert.True((await svc.ReleaseFromQuarantineAsync(forMissing.Unit!.Id, "tech2")).Succeeded);
        Assert.True((await svc.MarkMissingAsync(forMissing.Unit.Id, "Not on shelf at physical inventory")).Succeeded);
        var located = await svc.LocateMissingAsync(forMissing.Unit.Id);
        Assert.True(located.Succeeded, located.Error);
        Assert.Equal(UnitStatus.Quarantine, located.Unit!.Status);

        var forDamaged = await svc.ReceiveUnitAsync(NewUnitRequest(dNumber, productTypeId));
        Assert.True(forDamaged.Succeeded, forDamaged.Error);
        Assert.True((await svc.ReleaseFromQuarantineAsync(forDamaged.Unit!.Id, "tech2")).Succeeded);
        Assert.True((await svc.MarkDamagedAsync(forDamaged.Unit.Id, "Bag leaking in refrigerator")).Succeeded);
        var inspected = await svc.InspectDamagedAsync(forDamaged.Unit.Id);
        Assert.True(inspected.Succeeded, inspected.Error);
        Assert.Equal(UnitStatus.Quarantine, inspected.Unit!.Status);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == forMissing.Unit.Id
            && e.Reason == "Unit located after missing."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(mNumber));
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == forDamaged.Unit.Id
            && e.Reason == "Unit inspected after damage."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(dNumber));
    }

    [Fact]
    public async Task ReturnToSupplier_WritesProductStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var unitNumber = $"U-RTS-AUD-{key}";
        await using var context = _factory.Create();
        var svc = CreateService(context);
        var received = await svc.ReceiveUnitAsync(NewUnitRequest(unitNumber, productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var returned = await svc.ReturnToSupplierAsync(received.Unit!.Id, "Unused stock credit");
        Assert.True(returned.Succeeded, returned.Error);
        Assert.Equal(UnitStatus.ReturnedToSupplier, returned.Unit!.Status);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == received.Unit.Id
            && e.Reason == "Unit returned to supplier."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(unitNumber));
    }

    [Fact]
    public async Task Transfer_WritesProductStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var unitNumber = $"U-XFER-AUD-{key}";
        await using var context = _factory.Create();
        var location = new InventoryLocation
        {
            Code = $"LOC-XFER-AUD-{key}",
            Name = "Transfer audit fridge",
            LocationType = LocationType.Refrigerator
        };
        context.InventoryLocations.Add(location);
        await context.SaveChangesAsync();

        var svc = CreateService(context);
        var received = await svc.ReceiveUnitAsync(NewUnitRequest(unitNumber, productTypeId));
        Assert.True(received.Succeeded, received.Error);

        var transferred = await svc.TransferAsync(received.Unit!.Id, location.Id, "Move to issue fridge");
        Assert.True(transferred.Succeeded, transferred.Error);
        Assert.Equal(location.Id, transferred.Unit!.CurrentLocationId);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == received.Unit.Id
            && e.Reason == "Unit transferred."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(unitNumber));
    }

    [Fact]
    public async Task ExpireDueUnits_WritesProductStatus()
    {
        var productTypeId = await EnsureProductTypeAsync();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var unitNumber = $"U-EXP-AUD-{key}";
        await using var context = _factory.Create();
        var pastDue = new BloodUnit
        {
            UnitNumber = unitNumber,
            ProductTypeId = productTypeId,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1),
            Status = UnitStatus.Available
        };
        context.BloodUnits.Add(pastDue);
        await context.SaveChangesAsync();

        var expired = await CreateService(context).ExpireDueUnitsAsync();
        Assert.True(expired.Succeeded, expired.Error);
        Assert.Equal(UnitStatus.Expired, (await context.BloodUnits.FindAsync(pastDue.Id))!.Status);

        var events = context.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.ProductStatus
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == pastDue.Id
            && e.Reason == "Unit expired."
            && e.OldValueJson is not null
            && e.NewValueJson is not null
            && e.NewValueJson.Contains(unitNumber));
    }
}
