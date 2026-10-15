using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class PatientAllocationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public PatientAllocationTests(SqliteContextFactory factory) => _factory = factory;

    private BloodAttributeCompatLoader BloodAttrCompat(BloodBankDbContext c) =>
        new(
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<AntigenProfile>(c),
            new EfRepository<UnitBloodAttribute>(c),
            new EfRepository<BloodAttributeDefinition>(c));

    private AntibodyScreenCompatLoader AntibodyScreenCompat(BloodBankDbContext c) =>
        new(
            new EfRepository<TestResult>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<AntibodyHistory>(c),
            specimens: new EfRepository<Specimen>(c));

    private CompatibilityService Compatibility(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            BloodAttrCompat(c), AntibodyScreenCompat(c), c, _factory.Clock, _factory.CurrentUser,
            permissions: permissions,
            exceptionDefinitions: new EfRepository<ExceptionDefinition>(c),
            overrides: new EfRepository<Override>(c),
            aboCompatibilities: new EfRepository<ProductAboCompatibility>(c),
            rhCompatibilities: new EfRepository<ProductRhCompatibility>(c));

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c);

    private PatientAllocationService Allocations(
        BloodBankDbContext c,
        IPermissionEvaluator? permissions = null,
        bool includeSpecialRequirements = false)
    {
        var perms = permissions ?? new FixedPermissionEvaluator(3);
        return new(
            Compatibility(c, perms),
            Orders(c),
            new InventoryRepository(c),
            new EfRepository<Allocation>(c),
            new EfRepository<Crossmatch>(c),
            new EfRepository<Patient>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<Encounter>(c),
            new EfRepository<OrderingLocation>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<ExceptionDefinition>(c),
            new EfRepository<Override>(c),
            BloodAttrCompat(c),
            AntibodyScreenCompat(c),
            perms,
            _factory.Clock,
            _factory.CurrentUser,
            c,
            orderLines: new EfRepository<OrderLine>(c),
            orderRepo: new EfRepository<Order>(c),
            orderSpecimens: new EfRepository<OrderSpecimen>(c),
            issues: new EfRepository<Issue>(c),
            specialRequirements: includeSpecialRequirements ? new EfRepository<SpecialTransfusionRequirement>(c) : null,
            requirementDefinitions: includeSpecialRequirements ? new EfRepository<SpecialRequirementDefinition>(c) : null,
            productAttributeAssignments: includeSpecialRequirements ? new EfRepository<ProductAttributeAssignment>(c) : null,
            productAttributes: includeSpecialRequirements ? new EfRepository<ProductAttribute>(c) : null,
            locations: new EfRepository<InventoryLocation>(c),
            aboCompatibilities: new EfRepository<ProductAboCompatibility>(c),
            rhCompatibilities: new EfRepository<ProductRhCompatibility>(c));
    }

    private async Task<(Patient Patient, BloodUnit Unit)> SeedAvailableUnitAsync(BloodBankDbContext c, string key)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}",
            LastName = "Perm",
            FirstName = "Alloc",
            DateOfBirth = new DateOnly(1988, 2, 2)
        };
        c.Patients.Add(patient);
        var rbc = new ProductType
        {
            ProductCode = key,
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });
        var unit = new BloodUnit
        {
            UnitNumber = $"U-{key}",
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        return (patient, unit);
    }

    private async Task SeedCrossmatchCatalogAsync(BloodBankDbContext c)
    {
        if (!await c.TestDefinitions.AnyAsync(t => t.Code == "XM"))
        {
            c.TestDefinitions.Add(new TestDefinition
            {
                Code = "XM",
                Name = "Crossmatch",
                Category = TestCategory.Crossmatch,
                ResultValueType = ResultValueType.Crossmatch,
                AllowedResultValues = "Compatible\nIncompatible",
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = _factory.Clock.UtcNow,
                Version = 1
            });
        }

        if (!await c.TestDefinitions.AnyAsync(t => t.Code == "CXM"))
        {
            c.TestDefinitions.Add(new TestDefinition
            {
                Code = "CXM",
                Name = "Complex Crossmatch",
                Category = TestCategory.Crossmatch,
                ResultValueType = ResultValueType.ComplexCrossmatch,
                AllowedResultValues = "Compatible\nIncompatible",
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = _factory.Clock.UtcNow,
                Version = 1
            });
        }

        if (!await c.ExceptionDefinitions.AnyAsync(e => e.RuleCode == AntibodyHistoryCrossmatchRule.RuleCode))
        {
            c.ExceptionDefinitions.Add(new ExceptionDefinition
            {
                RuleCode = AntibodyHistoryCrossmatchRule.RuleCode,
                Name = "Simple XM with Ab history",
                MinSecurityLevel = 2,
                IsOverridable = true,
                IsActive = true
            });
        }

        await c.SaveChangesAsync();
    }

    [Fact]
    public async Task ListCompatibleUnits_ExcludesAboIncompatible()
    {
        await using var c = _factory.Create();
        await SeedCrossmatchCatalogAsync(c);

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-COMPAT-{Guid.NewGuid():N}",
            LastName = "Compat",
            FirstName = "Test",
            DateOfBirth = new DateOnly(1990, 1, 1)
        };
        c.Patients.Add(patient);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

        c.BloodUnits.AddRange(
            new BloodUnit
            {
                UnitNumber = $"U-OK-{Guid.NewGuid():N}"[..16],
                ProductTypeId = rbc.Id,
                Abo = AboGroup.A,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
                Status = UnitStatus.Available
            },
            new BloodUnit
            {
                UnitNumber = $"U-BAD-{Guid.NewGuid():N}"[..16],
                ProductTypeId = rbc.Id,
                Abo = AboGroup.B,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
                Status = UnitStatus.Available
            });
        await c.SaveChangesAsync();

        var result = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(result.Succeeded);
        Assert.Contains(result.Value!, u => u.UnitNumber.StartsWith("U-OK-", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Value!, u => u.UnitNumber.StartsWith("U-BAD-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Allocate_Rbc_OrdersCxm_AndListsReserved()
    {
        await using var c = _factory.Create();
        await SeedCrossmatchCatalogAsync(c);

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-ALLOC-{Guid.NewGuid():N}",
            LastName = "Alloc",
            FirstName = "Test",
            DateOfBirth = new DateOnly(1988, 2, 2)
        };
        c.Patients.Add(patient);
        var loc = new OrderingLocation { Code = $"L-{Guid.NewGuid():N}"[..10], Name = "BB", IsActive = true };
        c.OrderingLocations.Add(loc);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

        var enc = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{Guid.NewGuid():N}"[..12],
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow.AddHours(-2)
        };
        c.Encounters.Add(enc);

        var unit = new BloodUnit
        {
            UnitNumber = $"U-ALLOC-{Guid.NewGuid():N}"[..16],
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        var fridge = new InventoryLocation
        {
            Code = $"FR-{Guid.NewGuid():N}"[..12],
            Name = "Main Blood Bank Refrigerator"
        };
        c.InventoryLocations.Add(fridge);
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();
        unit.CurrentLocationId = fridge.Id;
        await c.SaveChangesAsync();

        var choices = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(choices.Succeeded, choices.Error);
        var choice = Assert.Single(choices.Value!, u => u.BloodUnitId == unit.Id);
        Assert.Equal("Main Blood Bank Refrigerator", choice.StorageLocationName);
        Assert.False(choice.CrossmatchCompatible);
        Assert.Null(choice.CrossmatchExpiresUtc);
        Assert.Null(choice.LatestCrossmatchResult);

        var alloc = await Allocations(c).AllocateAsync(patient.Id, new AllocatePatientUnitRequest(
            unit.Id, enc.Id, null, loc.Id, null, "CXM"));

        Assert.True(alloc.Succeeded, alloc.Error);
        Assert.Equal("CXM", alloc.Value!.CrossmatchTestCode);
        Assert.NotNull(alloc.Value.CrossmatchOrderId);
        Assert.Equal(ProductAllocationDisplayStatus.Reserved, alloc.Value.Allocation.DisplayStatus);

        var list = await Allocations(c).ListActiveAsync(patient.Id);
        Assert.Single(list);
        Assert.Equal(unit.UnitNumber, list[0].UnitNumber);
        Assert.Equal("RBC", list[0].ProductName);
        Assert.Equal(unit.ExpiresUtc, list[0].UnitExpiresUtc);
        Assert.Equal("O+", list[0].UnitBloodType);
        Assert.Equal("Main Blood Bank Refrigerator", list[0].StorageLocationName);
        Assert.Equal(unit.ExpiresUtc, alloc.Value.Allocation.UnitExpiresUtc);
        Assert.Equal("O+", alloc.Value.Allocation.UnitBloodType);
        Assert.Equal("Main Blood Bank Refrigerator", alloc.Value.Allocation.StorageLocationName);
        Assert.Null(list[0].CrossmatchExpiresUtc);
        Assert.Null(list[0].CrossmatchResult);
        Assert.Null(alloc.Value.Allocation.CrossmatchExpiresUtc);
        Assert.Null(alloc.Value.Allocation.CrossmatchResult);
        Assert.Equal(ProductAllocationDisplayStatus.Reserved, list[0].DisplayStatus);

        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}"[..12],
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2),
            ReceivedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();
        var xm = await Compatibility(c).RecordCrossmatchAsync(new RecordCrossmatchRequest(
            unit.Id, patient.Id, specimen.Id, CrossmatchMethod.Serologic, CrossmatchResult.Compatible));
        Assert.True(xm.Succeeded, xm.Error);

        var afterXm = await Allocations(c).ListActiveAsync(patient.Id);
        Assert.Equal(CrossmatchResult.Compatible, Assert.Single(afterXm).CrossmatchResult);
        Assert.Equal(_factory.Clock.UtcNow.AddDays(2), Assert.Single(afterXm).CrossmatchExpiresUtc);

        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            SpecimenId = specimen.Id,
            Result = CrossmatchResult.Incompatible,
            PerformedUtc = _factory.Clock.UtcNow.AddMinutes(1),
            PerformedBy = "tech"
        });
        await c.SaveChangesAsync();
        var afterIncompatible = await Allocations(c).ListActiveAsync(patient.Id);
        Assert.Equal(CrossmatchResult.Incompatible, Assert.Single(afterIncompatible).CrossmatchResult);
        Assert.Equal(_factory.Clock.UtcNow.AddDays(2), Assert.Single(afterIncompatible).CrossmatchExpiresUtc);

        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            SpecimenId = specimen.Id,
            Result = CrossmatchResult.NotPerformed,
            PerformedUtc = _factory.Clock.UtcNow.AddMinutes(2),
            PerformedBy = "tech"
        });
        await c.SaveChangesAsync();
        var afterBlank = await Allocations(c).ListActiveAsync(patient.Id);
        Assert.Null(Assert.Single(afterBlank).CrossmatchResult);

        var openUnit = new BloodUnit
        {
            UnitNumber = $"U-OPEN-{Guid.NewGuid():N}"[..16],
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(18),
            Status = UnitStatus.Available,
            CurrentLocationId = fridge.Id
        };
        c.BloodUnits.Add(openUnit);
        await c.SaveChangesAsync();
        var openXm = await Compatibility(c).RecordCrossmatchAsync(new RecordCrossmatchRequest(
            openUnit.Id, patient.Id, specimen.Id, CrossmatchMethod.Serologic, CrossmatchResult.Compatible));
        Assert.True(openXm.Succeeded, openXm.Error);

        var openChoices = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(openChoices.Succeeded, openChoices.Error);
        var openChoice = Assert.Single(openChoices.Value!, u => u.BloodUnitId == openUnit.Id);
        Assert.True(openChoice.CrossmatchCompatible);
        Assert.Equal(CrossmatchResult.Compatible, openChoice.LatestCrossmatchResult);
        Assert.Equal(_factory.Clock.UtcNow.AddDays(2), openChoice.CrossmatchExpiresUtc);
        Assert.DoesNotContain(openChoices.Value!, u => u.BloodUnitId == unit.Id);

        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = openUnit.Id,
            PatientId = patient.Id,
            SpecimenId = specimen.Id,
            Result = CrossmatchResult.Incompatible,
            PerformedUtc = _factory.Clock.UtcNow.AddMinutes(3),
            PerformedBy = "tech"
        });
        await c.SaveChangesAsync();
        var incompatibleChoices = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        var incompatibleChoice = Assert.Single(incompatibleChoices.Value!, u => u.BloodUnitId == openUnit.Id);
        Assert.Equal(CrossmatchResult.Incompatible, incompatibleChoice.LatestCrossmatchResult);
        Assert.Equal(_factory.Clock.UtcNow.AddDays(2), incompatibleChoice.CrossmatchExpiresUtc);

        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = openUnit.Id,
            PatientId = patient.Id,
            SpecimenId = specimen.Id,
            Result = CrossmatchResult.NotPerformed,
            PerformedUtc = _factory.Clock.UtcNow.AddMinutes(4),
            PerformedBy = "tech"
        });
        await c.SaveChangesAsync();
        var blankChoices = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        Assert.Null(Assert.Single(blankChoices.Value!, u => u.BloodUnitId == openUnit.Id).LatestCrossmatchResult);
    }

    [Fact]
    public async Task Allocate_WithoutCompatibilityAllocate_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (patient, unit) = await SeedAvailableUnitAsync(c, "PERM-ALLOC");

        var denied = await Compatibility(c, new FixedPermissionEvaluator(1, PermissionCodes.CompatibilityCrossmatch))
            .AllocateUnitAsync(new AllocateUnitRequest(unit.Id, patient.Id));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == CompatibilityAuthorizationRule.AllocateCode);
        Assert.Equal(UnitStatus.Available, (await c.BloodUnits.FindAsync(unit.Id))!.Status);

        var allowed = await Compatibility(c, new FixedPermissionEvaluator(1, PermissionCodes.CompatibilityAllocate))
            .AllocateUnitAsync(new AllocateUnitRequest(unit.Id, patient.Id));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(AllocationStatus.Reserved, allowed.Value!.Status);
    }

    [Fact]
    public async Task ReleaseAllocation_WithoutCompatibilityAllocate_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (patient, unit) = await SeedAvailableUnitAsync(c, "PERM-REL");
        var reserved = await Compatibility(c).AllocateUnitAsync(new AllocateUnitRequest(unit.Id, patient.Id));
        Assert.True(reserved.Succeeded, reserved.Error);

        var denied = await Compatibility(c, new FixedPermissionEvaluator(1, PermissionCodes.CompatibilityCrossmatch))
            .ReleaseAllocationAsync(reserved.Value!.Id, "Not needed");
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == CompatibilityAuthorizationRule.ReleaseCode);
        Assert.Equal(AllocationStatus.Reserved, (await c.Allocations.FindAsync(reserved.Value.Id))!.Status);
        Assert.Equal(UnitStatus.Assigned, (await c.BloodUnits.FindAsync(unit.Id))!.Status);

        var allowed = await Compatibility(c, new FixedPermissionEvaluator(1, PermissionCodes.CompatibilityAllocate))
            .ReleaseAllocationAsync(reserved.Value.Id, "Not needed");
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(AllocationStatus.Released, allowed.Value!.Status);
        Assert.Equal(UnitStatus.Available, (await c.BloodUnits.FindAsync(unit.Id))!.Status);
    }

    [Fact]
    public async Task RecordCrossmatch_WithoutCompatibilityCrossmatch_IsHardStopped()
    {
        await using var c = _factory.Create();
        var (patient, unit) = await SeedAvailableUnitAsync(c, "PERM-XM");
        var specimen = new Specimen
        {
            AccessionNumber = "ACC-PERM-XM",
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2),
            ReceivedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();

        var request = new RecordCrossmatchRequest(
            unit.Id, patient.Id, specimen.Id, CrossmatchMethod.Serologic, CrossmatchResult.Compatible);
        var denied = await Compatibility(c, new FixedPermissionEvaluator(1, PermissionCodes.CompatibilityAllocate))
            .RecordCrossmatchAsync(request);
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == CompatibilityAuthorizationRule.CrossmatchCode);
        Assert.False(await c.Crossmatches.AnyAsync(x => x.BloodProductId == unit.Id));

        var allowed = await Compatibility(c, new FixedPermissionEvaluator(1, PermissionCodes.CompatibilityCrossmatch))
            .RecordCrossmatchAsync(request);
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(CrossmatchResult.Compatible, allowed.Value!.Result);
    }

    [Fact]
    public async Task Allocate_AntibodyHistory_SimpleXm_RequiresOverride()
    {
        await using var c = _factory.Create();
        await SeedCrossmatchCatalogAsync(c);

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-AB-{Guid.NewGuid():N}",
            LastName = "Ab",
            FirstName = "Hx",
            DateOfBirth = new DateOnly(1980, 3, 3)
        };
        c.Patients.Add(patient);
        var loc = new OrderingLocation { Code = $"L-{Guid.NewGuid():N}"[..10], Name = "BB", IsActive = true };
        c.OrderingLocations.Add(loc);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Negative,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });
        c.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = patient.Id,
            AntibodySpecificity = "Anti-K",
            Status = AntibodyStatus.Identified,
            IsActive = true
        });
        var enc = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{Guid.NewGuid():N}"[..12],
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        };
        c.Encounters.Add(enc);
        var unit = new BloodUnit
        {
            UnitNumber = $"U-AB-{Guid.NewGuid():N}"[..16],
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Negative,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        var blocked = await Allocations(c).AllocateAsync(patient.Id, new AllocatePatientUnitRequest(
            unit.Id, enc.Id, null, loc.Id, null, "XM"));
        Assert.False(blocked.Succeeded);
        Assert.True(blocked.RequiresOverride);

        var ok = await Allocations(c).AllocateAsync(patient.Id, new AllocatePatientUnitRequest(
            unit.Id, enc.Id, null, loc.Id, null, "XM", "Ab history documented; IS XM authorized", "supervisor"));
        Assert.True(ok.Succeeded, ok.Error);
        Assert.True(ok.Value!.AntibodyHistoryOverrideApplied);
    }

    [Fact]
    public async Task Allocate_PositiveAbsc_SimpleXm_RequiresOverride()
    {
        await using var c = _factory.Create();
        await SeedCrossmatchCatalogAsync(c);

        if (!await c.TestDefinitions.AnyAsync(t => t.Code == "ABSC"))
        {
            c.TestDefinitions.Add(new TestDefinition
            {
                Code = "ABSC",
                Name = "Antibody Screen",
                Category = TestCategory.AntibodyScreen,
                ResultValueType = ResultValueType.Coded,
                AllowedResultValues = "Negative\nPositive",
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = _factory.Clock.UtcNow,
                Version = 1
            });
        }

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-ABSC-{Guid.NewGuid():N}",
            LastName = "Screen",
            FirstName = "Pos",
            DateOfBirth = new DateOnly(1985, 4, 4)
        };
        c.Patients.Add(patient);
        var loc = new OrderingLocation { Code = $"L-{Guid.NewGuid():N}"[..10], Name = "BB", IsActive = true };
        c.OrderingLocations.Add(loc);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });
        var specimen = new Specimen
        {
            PatientId = patient.Id,
            AccessionNumber = $"ACC-{Guid.NewGuid():N}"[..12],
            SpecimenType = "EDT",
            Status = SpecimenStatus.Accepted,
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ReceivedUtc = _factory.Clock.UtcNow,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(3)
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();

        c.TestResults.Add(new TestResult
        {
            PatientId = patient.Id,
            SpecimenId = specimen.Id,
            TestCode = "ABSC",
            Value = "Positive",
            Status = ResultStatus.Verified,
            VerifiedUtc = _factory.Clock.UtcNow,
            VerifiedBy = "tech1"
        });

        var enc = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{Guid.NewGuid():N}"[..12],
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        };
        c.Encounters.Add(enc);
        var unit = new BloodUnit
        {
            UnitNumber = $"U-ABSC-{Guid.NewGuid():N}"[..16],
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        var blocked = await Allocations(c).AllocateAsync(patient.Id, new AllocatePatientUnitRequest(
            unit.Id, enc.Id, null, loc.Id, null, "XM"));
        Assert.False(blocked.Succeeded);
        Assert.True(blocked.RequiresOverride);
        Assert.Contains(blocked.Evaluation!.Warnings, r => r.Code == AntibodyHistoryCrossmatchRule.RuleCode);
    }

    [Fact]
    public async Task Allocate_AntiK_UntypedUnit_RequiresSupervisorOverride()
    {
        await using var c = _factory.Create();
        await SeedCrossmatchCatalogAsync(c);

        if (!await c.ExceptionDefinitions.AnyAsync(e => e.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode))
        {
            c.ExceptionDefinitions.Add(new ExceptionDefinition
            {
                RuleCode = BloodAttributeCompatibilityRule.AntigenNegCode,
                Name = "Antigen-negative requirement not met",
                MinSecurityLevel = 2,
                IsOverridable = true,
                IsActive = true
            });
        }

        var kell = await c.BloodAttributeDefinitions.FirstOrDefaultAsync(d => d.Code == "K");
        if (kell is null)
        {
            kell = new BloodAttributeDefinition
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
            c.BloodAttributeDefinitions.Add(kell);
            await c.SaveChangesAsync();
        }

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-K-{Guid.NewGuid():N}",
            LastName = "Kell",
            FirstName = "Ab",
            DateOfBirth = new DateOnly(1970, 1, 1)
        };
        c.Patients.Add(patient);
        var loc = new OrderingLocation { Code = $"L-{Guid.NewGuid():N}"[..10], Name = "BB", IsActive = true };
        c.OrderingLocations.Add(loc);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });
        c.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = patient.Id,
            AntibodySpecificity = "anti-K",
            BloodAttributeDefinitionId = kell.Id,
            Status = AntibodyStatus.HistoricalOnly,
            IsActive = false
        });
        var enc = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{Guid.NewGuid():N}"[..12],
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        };
        c.Encounters.Add(enc);
        var unit = new BloodUnit
        {
            UnitNumber = $"U-K-{Guid.NewGuid():N}"[..16],
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        var blocked = await Allocations(c).AllocateAsync(patient.Id, new AllocatePatientUnitRequest(
            unit.Id, enc.Id, null, loc.Id, null, "CXM"));
        Assert.False(blocked.Succeeded);
        Assert.True(blocked.RequiresOverride);
        Assert.Contains(blocked.Evaluation!.Warnings, r => r.Code == BloodAttributeCompatibilityRule.AntigenNegCode);

        var ok = await Allocations(c).AllocateAsync(patient.Id, new AllocatePatientUnitRequest(
            unit.Id, enc.Id, null, loc.Id, null, "CXM",
            "No K-neg unit available; clinical urgency", "supervisor"));
        Assert.True(ok.Succeeded, ok.Error);
        Assert.True(await c.Overrides.AnyAsync(o =>
            o.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode
            && o.ContextType == nameof(Allocation)
            && o.Reason == "No K-neg unit available; clinical urgency"));
    }

    [Fact]
    public async Task Allocate_AntiK_Level1Override_IsBlocked()
    {
        await using var c = _factory.Create();
        var (patient, unit, enc, loc) = await SeedAntiKUnitAsync(c, typedPositive: false);

        var blocked = await Allocations(c, new FixedPermissionEvaluator(1)).AllocateAsync(
            patient.Id,
            new AllocatePatientUnitRequest(
                unit.Id, enc.Id, null, loc.Id, null, "CXM",
                "No K-neg unit available; clinical urgency", "tech"));
        Assert.False(blocked.Succeeded);
        Assert.False(blocked.RequiresOverride);
        Assert.Contains(blocked.Evaluation!.HardStops, r => r.Code == "EXC-SECURITY-LEVEL");
        Assert.False(await c.Allocations.AnyAsync(a => a.BloodProductId == unit.Id));
        Assert.False(await c.Overrides.AnyAsync(o =>
            o.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode && o.AuthorizedBy == "tech"));
        Assert.Equal(UnitStatus.Available, (await c.BloodUnits.SingleAsync(u => u.Id == unit.Id)).Status);
    }

    [Fact]
    public async Task AllocateUnit_AntiK_RequiresPrivilegedOverrideRow()
    {
        await using var c = _factory.Create();
        var (patient, unit, _, _) = await SeedAntiKUnitAsync(c, typedPositive: true);

        var missing = await Compatibility(c, new FixedPermissionEvaluator(2)).AllocateUnitAsync(
            new AllocateUnitRequest(unit.Id, patient.Id));
        Assert.False(missing.Succeeded);
        Assert.True(missing.RequiresOverride);
        Assert.Contains(missing.Evaluation!.Warnings, r =>
            r.Code == BloodAttributeCompatibilityRule.AntigenNegCode
            && r.Message.Contains("Positive", StringComparison.Ordinal));

        var low = await Compatibility(c, new FixedPermissionEvaluator(1)).AllocateUnitAsync(
            new AllocateUnitRequest(unit.Id, patient.Id, OverrideReason: "Clinical urgency for untyped red cells", AuthorizedBy: "tech"));
        Assert.False(low.Succeeded);
        Assert.Contains(low.Evaluation!.HardStops, r => r.Code == "EXC-SECURITY-LEVEL");

        var ok = await Compatibility(c, new FixedPermissionEvaluator(2)).AllocateUnitAsync(
            new AllocateUnitRequest(unit.Id, patient.Id, OverrideReason: "Clinical urgency for untyped red cells", AuthorizedBy: "supervisor"));
        Assert.True(ok.Succeeded, ok.Error);
        Assert.True(await c.Overrides.AnyAsync(o =>
            o.ContextType == nameof(Allocation)
            && o.ContextId == ok.Value!.Id
            && o.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode
            && o.AuthorizedBy == "supervisor"));
    }

    private async Task<(Patient Patient, BloodUnit Unit, Encounter Encounter, OrderingLocation Location)> SeedAntiKUnitAsync(BloodBankDbContext c, bool typedPositive)
    {
        await SeedCrossmatchCatalogAsync(c);
        if (!await c.ExceptionDefinitions.AnyAsync(e => e.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode))
        {
            c.ExceptionDefinitions.Add(new ExceptionDefinition
            {
                RuleCode = BloodAttributeCompatibilityRule.AntigenNegCode,
                Name = "Antigen-negative requirement not met",
                MinSecurityLevel = 2,
                IsOverridable = true,
                IsActive = true
            });
        }

        var kell = await c.BloodAttributeDefinitions.FirstOrDefaultAsync(d => d.Code == "K");
        if (kell is null)
        {
            kell = new BloodAttributeDefinition
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
            c.BloodAttributeDefinitions.Add(kell);
        }

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-KDIR-{Guid.NewGuid():N}"[..20],
            LastName = "Kell",
            FirstName = "Direct",
            DateOfBirth = new DateOnly(1972, 3, 3)
        };
        c.Patients.Add(patient);
        var loc = new OrderingLocation { Code = $"L-{Guid.NewGuid():N}"[..10], Name = "BB", IsActive = true };
        c.OrderingLocations.Add(loc);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = false
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });
        c.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = patient.Id,
            AntibodySpecificity = "anti-K",
            BloodAttributeDefinitionId = kell.Id,
            Status = AntibodyStatus.Identified,
            IsActive = true
        });
        var enc = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{Guid.NewGuid():N}"[..12],
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        };
        c.Encounters.Add(enc);
        var unit = new BloodUnit
        {
            UnitNumber = $"U-KDIR-{Guid.NewGuid():N}"[..16],
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        if (typedPositive)
        {
            c.UnitBloodAttributes.Add(new UnitBloodAttribute
            {
                BloodProductId = unit.Id,
                BloodAttributeDefinitionId = kell.Id,
                AttributeKind = BloodAttributeKind.Antigen,
                Result = AntigenResult.Positive
            });
            await c.SaveChangesAsync();
        }

        return (patient, unit, enc, loc);
    }

    [Fact]
    public async Task ListCompatibleUnits_FlagsUntypedAndAntigenPositive()
    {
        await using var c = _factory.Create();
        await SeedCrossmatchCatalogAsync(c);

        var kell = await c.BloodAttributeDefinitions.FirstOrDefaultAsync(d => d.Code == "K");
        if (kell is null)
        {
            kell = new BloodAttributeDefinition
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
            c.BloodAttributeDefinitions.Add(kell);
            await c.SaveChangesAsync();
        }

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-LISTK-{Guid.NewGuid():N}",
            LastName = "List",
            FirstName = "K",
            DateOfBirth = new DateOnly(1991, 2, 2)
        };
        c.Patients.Add(patient);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });
        c.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = patient.Id,
            AntibodySpecificity = "anti-K",
            BloodAttributeDefinitionId = kell.Id,
            Status = AntibodyStatus.Identified,
            IsActive = true
        });

        var okUnit = new BloodUnit
        {
            UnitNumber = $"U-KNEG-{Guid.NewGuid():N}"[..16],
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
            Status = UnitStatus.Available
        };
        var badUnit = new BloodUnit
        {
            UnitNumber = $"U-KPOS-{Guid.NewGuid():N}"[..16],
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
            Status = UnitStatus.Available
        };
        var untyped = new BloodUnit
        {
            UnitNumber = $"U-KNONE-{Guid.NewGuid():N}"[..16],
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(10),
            Status = UnitStatus.Available
        };
        c.BloodUnits.AddRange(okUnit, badUnit, untyped);
        await c.SaveChangesAsync();

        c.UnitBloodAttributes.AddRange(
            new UnitBloodAttribute
            {
                BloodProductId = okUnit.Id,
                BloodAttributeDefinitionId = kell.Id,
                AttributeKind = BloodAttributeKind.Antigen,
                Result = AntigenResult.Negative
            },
            new UnitBloodAttribute
            {
                BloodProductId = badUnit.Id,
                BloodAttributeDefinitionId = kell.Id,
                AttributeKind = BloodAttributeKind.Antigen,
                Result = AntigenResult.Positive
            });
        await c.SaveChangesAsync();

        var result = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(result.Succeeded);
        var negative = Assert.Single(result.Value!, u => u.UnitNumber.StartsWith("U-KNEG-", StringComparison.Ordinal));
        Assert.Null(negative.AntigenNegFlag);
        var positive = Assert.Single(result.Value!, u => u.UnitNumber.StartsWith("U-KPOS-", StringComparison.Ordinal));
        Assert.Equal(BloodAttributeCompatibilityRule.NotNegativeFlag, positive.AntigenNegFlag);
        Assert.Contains("Positive", positive.AntigenNegDetail, StringComparison.Ordinal);
        var missing = Assert.Single(result.Value!, u => u.UnitNumber.StartsWith("U-KNONE-", StringComparison.Ordinal));
        Assert.Equal(BloodAttributeCompatibilityRule.UntestedFlag, missing.AntigenNegFlag);
        Assert.Contains("not tested", missing.AntigenNegDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListCompatibleUnits_RanksOwnRestrictedAndIdenticalBeforeSubstitute()
    {
        await using var c = _factory.Create();
        await SeedCrossmatchCatalogAsync(c);

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-SEL-{Guid.NewGuid():N}",
            LastName = "Select",
            FirstName = "Rank",
            DateOfBirth = new DateOnly(1992, 3, 3)
        };
        var other = new Patient
        {
            MedicalRecordNumber = $"MRN-OTH-{Guid.NewGuid():N}",
            LastName = "Other",
            FirstName = "Donor",
            DateOfBirth = new DateOnly(1985, 4, 4)
        };
        c.Patients.AddRange(patient, other);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

        var ownAutologous = Unit(rbc.Id, "U-OWN-", AboGroup.A, days: 40, DonationRestriction.Autologous, patient.Id);
        var ownDirected = Unit(rbc.Id, "U-DIR-", AboGroup.A, days: 5, DonationRestriction.Directed, patient.Id);
        var identicalSoon = Unit(rbc.Id, "U-SAME-S-", AboGroup.A, days: 12);
        var identicalLate = Unit(rbc.Id, "U-SAME-L-", AboGroup.A, days: 30);
        var substitute = Unit(rbc.Id, "U-SUB-", AboGroup.O, days: 1);
        var otherAutologous = Unit(rbc.Id, "U-OTH-", AboGroup.A, days: 1, DonationRestriction.Autologous, other.Id);
        var incompatible = Unit(rbc.Id, "U-BAD-", AboGroup.B, days: 10);
        c.BloodUnits.AddRange(ownAutologous, ownDirected, identicalSoon, identicalLate, substitute, otherAutologous, incompatible);
        await c.SaveChangesAsync();

        var result = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(result.Succeeded, result.Error);
        var listed = result.Value!.Where(u => u.ProductTypeId == rbc.Id).ToList();

        Assert.Equal(
            [ownAutologous.UnitNumber, ownDirected.UnitNumber, identicalSoon.UnitNumber, identicalLate.UnitNumber, substitute.UnitNumber],
            listed.Select(u => u.UnitNumber).ToArray());
        Assert.Equal(ProductSelectionOrder.PatientAutologousCue, listed[0].SelectionCue);
        Assert.Equal(DonationRestriction.Autologous, listed[0].DonationRestriction);
        Assert.Equal(ProductSelectionOrder.PatientDirectedCue, listed[1].SelectionCue);
        Assert.Equal(ProductSelectionOrder.IdenticalCue, listed[2].SelectionCue);
        Assert.Equal(ProductSelectionOrder.SubstituteCue, listed[4].SelectionCue);
        Assert.DoesNotContain(listed, u => u.UnitNumber == otherAutologous.UnitNumber);
        Assert.DoesNotContain(listed, u => u.UnitNumber == incompatible.UnitNumber);

        var blocked = await Compatibility(c).AllocateUnitAsync(new AllocateUnitRequest(otherAutologous.Id, patient.Id));
        Assert.Contains(blocked.Evaluation!.HardStops, r => r.Code == AutologousDirectedRule.IssueCode);
        Assert.Equal(UnitStatus.Available, (await c.BloodUnits.FindAsync(otherAutologous.Id))!.Status);
    }

    private BloodUnit Unit(
        long productTypeId,
        string prefix,
        AboGroup abo,
        int days,
        DonationRestriction restriction = DonationRestriction.Allogeneic,
        long? reservedPatientId = null) =>
        new()
        {
            UnitNumber = $"{prefix}{Guid.NewGuid():N}"[..16],
            ProductTypeId = productTypeId,
            Abo = abo,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(days),
            Status = UnitStatus.Available,
            DonationRestriction = restriction,
            ReservedPatientId = reservedPatientId
        };

    [Fact]
    public async Task ListCompatibleUnits_OmitsOutdatedUnit()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-EXP-{Guid.NewGuid():N}",
            LastName = "Select",
            FirstName = "Outdate",
            DateOfBirth = new DateOnly(1993, 5, 5)
        };
        c.Patients.Add(patient);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

        var inDate = Unit(rbc.Id, "U-IN-", AboGroup.A, days: 3);
        var outdated = Unit(rbc.Id, "U-OUT-", AboGroup.A, days: -1);
        var atNow = Unit(rbc.Id, "U-NOW-", AboGroup.O, days: 1);
        atNow.ExpiresUtc = _factory.Clock.UtcNow;
        c.BloodUnits.AddRange(inDate, outdated, atNow);
        await c.SaveChangesAsync();

        var result = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(result.Succeeded, result.Error);
        var listed = result.Value!.Where(u => u.ProductTypeId == rbc.Id).ToList();
        Assert.Equal([inDate.UnitNumber], listed.Select(u => u.UnitNumber).ToArray());
        Assert.Equal(UnitStatus.Available, (await c.BloodUnits.FindAsync(outdated.Id))!.Status);
    }

    [Fact]
    public async Task ListCompatibleUnits_MarksUnitInsideNearExpiryWindow()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-SOON-{Guid.NewGuid():N}",
            LastName = "Select",
            FirstName = "Soon",
            DateOfBirth = new DateOnly(1994, 6, 6)
        };
        c.Patients.Add(patient);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

        var soon = Unit(rbc.Id, "U-SOON-", AboGroup.A, days: 1);
        soon.ExpiresUtc = _factory.Clock.UtcNow.AddHours(2);
        var later = Unit(rbc.Id, "U-LATER-", AboGroup.A, days: 3);
        c.BloodUnits.AddRange(soon, later);
        await c.SaveChangesAsync();

        var result = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(result.Succeeded, result.Error);
        var listed = result.Value!.Where(u => u.ProductTypeId == rbc.Id).ToList();
        Assert.Equal([soon.UnitNumber, later.UnitNumber], listed.Select(u => u.UnitNumber).ToArray());
        Assert.True(listed[0].ExpiresSoon);
        Assert.False(listed[1].ExpiresSoon);
    }

    [Fact]
    public async Task ListCompatibleUnits_OmitsUnitThatFailsSpecialRequirement()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-SR-{Guid.NewGuid():N}",
            LastName = "Select",
            FirstName = "Special",
            DateOfBirth = new DateOnly(1991, 4, 4)
        };
        c.Patients.Add(patient);
        var plain = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        var irradiated = new ProductType
        {
            ProductCode = $"IRR-{Guid.NewGuid():N}"[..12],
            Name = "Irradiated RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        var attr = new ProductAttribute { Code = SpecialRequirementCatalog.Irradiated, Name = "Irradiated" };
        c.ProductTypes.AddRange(plain, irradiated);
        c.ProductAttributes.Add(attr);
        await c.SaveChangesAsync();
        c.ProductAttributeAssignments.Add(new ProductAttributeAssignment
        {
            ProductTypeId = irradiated.Id,
            ProductAttributeId = attr.Id
        });
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });
        var missing = Unit(plain.Id, "U-PLN-", AboGroup.A, days: 5);
        var meets = Unit(irradiated.Id, "U-IRR-", AboGroup.A, days: 6);
        c.BloodUnits.AddRange(missing, meets);
        c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
        {
            PatientId = patient.Id,
            RequirementType = SpecialTransfusionRequirementType.Irradiated,
            Reason = "Required",
            EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
            IsActive = true,
            EnteredBy = "tech-test"
        });
        await c.SaveChangesAsync();

        var result = await Allocations(c, includeSpecialRequirements: true).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(result.Succeeded, result.Error);
        var listed = result.Value!.Select(u => u.UnitNumber).ToArray();
        Assert.Contains(meets.UnitNumber, listed);
        Assert.DoesNotContain(missing.UnitNumber, listed);
        Assert.Equal(UnitStatus.Available, (await c.BloodUnits.FindAsync(missing.Id))!.Status);
    }

    [Fact]
    public async Task ListCompatibleUnits_KeepsUnitForAcknowledgmentOnlyRequirement()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-ACK-{Guid.NewGuid():N}",
            LastName = "Select",
            FirstName = "Ack",
            DateOfBirth = new DateOnly(1992, 4, 4)
        };
        c.Patients.Add(patient);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });
        var unit = Unit(rbc.Id, "U-ACK-", AboGroup.A, days: 4);
        c.BloodUnits.Add(unit);
        c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
        {
            PatientId = patient.Id,
            RequirementType = SpecialTransfusionRequirementType.Other,
            Reason = "Blood warmer",
            EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
            IsActive = true,
            EnteredBy = "tech-test"
        });
        await c.SaveChangesAsync();

        var result = await Allocations(c, includeSpecialRequirements: true).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(result.Succeeded, result.Error);
        Assert.Contains(result.Value!, u => u.UnitNumber == unit.UnitNumber);
    }

    [Fact]
    public async Task ListCompatibleUnits_ListsCurrentCrossmatchAheadOfShorterDatedUnit()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-XM-{Guid.NewGuid():N}",
            LastName = "Select",
            FirstName = "Crossmatch",
            DateOfBirth = new DateOnly(1990, 3, 3)
        };
        var other = new Patient
        {
            MedicalRecordNumber = $"MRN-XM2-{Guid.NewGuid():N}",
            LastName = "Select",
            FirstName = "Other",
            DateOfBirth = new DateOnly(1990, 3, 4)
        };
        c.Patients.AddRange(patient, other);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

        var matched = Unit(rbc.Id, "U-XM-", AboGroup.A, days: 10);
        var shorter = Unit(rbc.Id, "U-NO-", AboGroup.A, days: 2);
        c.BloodUnits.AddRange(matched, shorter);
        await c.SaveChangesAsync();
        c.Crossmatches.AddRange(
            new Crossmatch
            {
                BloodProductId = matched.Id,
                PatientId = patient.Id,
                SpecimenId = 0,
                Result = CrossmatchResult.Compatible,
                PerformedUtc = _factory.Clock.UtcNow,
                PerformedBy = "tech",
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(1)
            },
            new Crossmatch
            {
                BloodProductId = shorter.Id,
                PatientId = other.Id,
                SpecimenId = 0,
                Result = CrossmatchResult.Compatible,
                PerformedUtc = _factory.Clock.UtcNow,
                PerformedBy = "tech"
            },
            new Crossmatch
            {
                BloodProductId = shorter.Id,
                PatientId = patient.Id,
                SpecimenId = 0,
                Result = CrossmatchResult.Compatible,
                PerformedUtc = _factory.Clock.UtcNow.AddDays(-2),
                PerformedBy = "tech",
                ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-1)
            });
        await c.SaveChangesAsync();

        var result = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(result.Succeeded, result.Error);
        var listed = result.Value!.Where(u => u.ProductTypeId == rbc.Id).ToList();
        Assert.Equal([matched.UnitNumber, shorter.UnitNumber], listed.Select(u => u.UnitNumber).ToArray());
        Assert.True(listed[0].CrossmatchCompatible);
        Assert.False(listed[1].CrossmatchCompatible);
    }

    [Fact]
    public async Task ListCompatibleUnits_ListsIncompatibleCrossmatchAfterUnmatchedUnit()
    {
        await using var c = _factory.Create();
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-INC-{Guid.NewGuid():N}",
            LastName = "Select",
            FirstName = "Incompatible",
            DateOfBirth = new DateOnly(1991, 4, 4)
        };
        var other = new Patient
        {
            MedicalRecordNumber = $"MRN-INC2-{Guid.NewGuid():N}",
            LastName = "Select",
            FirstName = "Other",
            DateOfBirth = new DateOnly(1991, 4, 5)
        };
        c.Patients.AddRange(patient, other);
        var rbc = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

        var laterCompatible = Unit(rbc.Id, "U-OK-", AboGroup.A, days: 10);
        var unmatched = Unit(rbc.Id, "U-NO-", AboGroup.A, days: 3);
        var incompatible = Unit(rbc.Id, "U-BAD-", AboGroup.A, days: 1);
        c.BloodUnits.AddRange(laterCompatible, unmatched, incompatible);
        await c.SaveChangesAsync();
        c.Crossmatches.AddRange(
            new Crossmatch
            {
                BloodProductId = laterCompatible.Id,
                PatientId = patient.Id,
                SpecimenId = 0,
                Result = CrossmatchResult.Incompatible,
                PerformedUtc = _factory.Clock.UtcNow.AddDays(-2),
                PerformedBy = "tech",
                ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-1)
            },
            new Crossmatch
            {
                BloodProductId = laterCompatible.Id,
                PatientId = patient.Id,
                SpecimenId = 0,
                Result = CrossmatchResult.Compatible,
                PerformedUtc = _factory.Clock.UtcNow,
                PerformedBy = "tech",
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(1)
            },
            new Crossmatch
            {
                BloodProductId = unmatched.Id,
                PatientId = other.Id,
                SpecimenId = 0,
                Result = CrossmatchResult.Incompatible,
                PerformedUtc = _factory.Clock.UtcNow,
                PerformedBy = "tech"
            },
            new Crossmatch
            {
                BloodProductId = incompatible.Id,
                PatientId = patient.Id,
                SpecimenId = 0,
                Result = CrossmatchResult.Incompatible,
                PerformedUtc = _factory.Clock.UtcNow.AddDays(-1),
                PerformedBy = "tech",
                ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-1)
            });
        await c.SaveChangesAsync();

        var result = await Allocations(c).ListCompatibleUnitsAsync(patient.Id);
        Assert.True(result.Succeeded, result.Error);
        var listed = result.Value!.Where(u => u.ProductTypeId == rbc.Id).ToList();
        Assert.Equal(
            [laterCompatible.UnitNumber, unmatched.UnitNumber, incompatible.UnitNumber],
            listed.Select(u => u.UnitNumber).ToArray());
        Assert.True(listed[0].CrossmatchCompatible);
        Assert.False(listed[0].CrossmatchIncompatible);
        Assert.False(listed[1].CrossmatchCompatible);
        Assert.False(listed[1].CrossmatchIncompatible);
        Assert.False(listed[2].CrossmatchCompatible);
        Assert.True(listed[2].CrossmatchIncompatible);
    }

    [Fact]
    public async Task ListAllocatableOrders_FiltersOpenOrdersForProductType()
    {
        await using var c = _factory.Create();
        var (patient, unit) = await SeedAvailableUnitAsync(c, "ORD-LIST");
        var loc = new OrderingLocation { Code = $"L-{Guid.NewGuid():N}"[..10], Name = "BB", IsActive = true };
        c.OrderingLocations.Add(loc);
        var enc = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = $"VIS-{Guid.NewGuid():N}"[..12],
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow
        };
        c.Encounters.Add(enc);
        await c.SaveChangesAsync();

        var created = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Id, loc.Id, $"ORD-OPEN-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Product, null, unit.ProductTypeId)],
            OrderPriority.Routine, _factory.Clock.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(created.Succeeded, created.Error);

        var list = await Allocations(c).ListAllocatableOrdersForUnitAsync(unit.Id);
        Assert.Contains(list, o => o.OrderId == created.Value!.Id);
        Assert.Equal(patient.Id, list[0].PatientId);
    }

    [Fact]
    public async Task AlternateRh_OPositiveRbc_IsAllocatableForMale_NotFemale()
    {
        await using var c = _factory.Create();
        var product = new ProductType
        {
            ProductCode = $"RBC-{Guid.NewGuid():N}"[..12],
            Name = "RBC alternate",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();

        var policy = StandardProductAboRhPolicy.For(ComponentClass.RedBloodCells)
            .WithRh(RhCompatibilitySex.Male, RhType.Negative, RhType.Positive, true);
        foreach (var cell in policy.AboCells)
        {
            c.ProductAboCompatibilities.Add(new ProductAboCompatibility
            {
                ProductTypeId = product.Id,
                PatientAbo = cell.PatientAbo,
                UnitAbo = cell.UnitAbo,
                IsAllowed = cell.IsAllowed
            });
        }

        foreach (var cell in policy.RhCells)
        {
            c.ProductRhCompatibilities.Add(new ProductRhCompatibility
            {
                ProductTypeId = product.Id,
                SexGroup = cell.SexGroup,
                PatientRh = cell.PatientRh,
                UnitRh = cell.UnitRh,
                IsAllowed = cell.IsAllowed
            });
        }

        var male = new Patient
        {
            MedicalRecordNumber = $"MRN-M-{Guid.NewGuid():N}"[..16],
            LastName = "Male",
            FirstName = "Alt",
            Sex = Sex.Male,
            DateOfBirth = new DateOnly(1980, 1, 1)
        };
        var female = new Patient
        {
            MedicalRecordNumber = $"MRN-F-{Guid.NewGuid():N}"[..16],
            LastName = "Female",
            FirstName = "Alt",
            Sex = Sex.Female,
            DateOfBirth = new DateOnly(1982, 1, 1)
        };
        c.Patients.AddRange(male, female);
        await c.SaveChangesAsync();

        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = male.Id,
                Abo = AboGroup.A,
                RhD = RhType.Negative,
                Source = BloodTypeSource.ManualEntry,
                IsCurrent = true
            },
            new PatientBloodTypeHistory
            {
                PatientId = female.Id,
                Abo = AboGroup.A,
                RhD = RhType.Negative,
                Source = BloodTypeSource.ManualEntry,
                IsCurrent = true
            });

        var alternate = new BloodUnit
        {
            UnitNumber = $"U-ALT-{Guid.NewGuid():N}"[..16],
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        var typeSpecific = new BloodUnit
        {
            UnitNumber = $"U-TS-{Guid.NewGuid():N}"[..16],
            ProductTypeId = product.Id,
            Abo = AboGroup.A,
            RhD = RhType.Negative,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        c.BloodUnits.AddRange(alternate, typeSpecific);
        await c.SaveChangesAsync();

        var maleChoices = await Allocations(c).ListCompatibleUnitsAsync(male.Id);
        Assert.True(maleChoices.Succeeded, maleChoices.Error);
        Assert.Contains(maleChoices.Value!, u => u.BloodUnitId == alternate.Id);
        Assert.Contains(maleChoices.Value!, u => u.BloodUnitId == typeSpecific.Id);

        var femaleChoices = await Allocations(c).ListCompatibleUnitsAsync(female.Id);
        Assert.True(femaleChoices.Succeeded, femaleChoices.Error);
        Assert.DoesNotContain(femaleChoices.Value!, u => u.BloodUnitId == alternate.Id);
        Assert.Contains(femaleChoices.Value!, u => u.BloodUnitId == typeSpecific.Id);

        var reserved = await Compatibility(c).AllocateUnitAsync(new AllocateUnitRequest(alternate.Id, male.Id));
        Assert.True(reserved.Succeeded, reserved.Error);

        var blocked = await Compatibility(c).AllocateUnitAsync(new AllocateUnitRequest(typeSpecific.Id, female.Id));
        Assert.True(blocked.Succeeded, blocked.Error);

        var oPositiveForFemale = new BloodUnit
        {
            UnitNumber = $"U-F-{Guid.NewGuid():N}"[..16],
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(oPositiveForFemale);
        await c.SaveChangesAsync();

        var denied = await Compatibility(c).AllocateUnitAsync(new AllocateUnitRequest(oPositiveForFemale.Id, female.Id));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == AboCompatibilityRule.RhCode);
    }

    [Fact]
    public async Task CompatibleAndReserved_KeepTheLocationIdWhenTheNameIsBlank()
    {
        const string code = "F2ALLOC224";
        long locationId;
        long patientId;
        long unitId;
        string unitNumber;
        await using (var c = _factory.Create())
        {
            var (patient, unit) = await SeedAvailableUnitAsync(c, "ALLOC224");
            var location = new InventoryLocation { Code = code, Name = "   " };
            c.InventoryLocations.Add(location);
            await c.SaveChangesAsync();
            locationId = location.Id;
            unit.CurrentLocationId = location.Id;
            await c.SaveChangesAsync();
            patientId = patient.Id;
            unitId = unit.Id;
            unitNumber = unit.UnitNumber;
        }

        await using var read = _factory.Create();
        var choices = await Allocations(read).ListCompatibleUnitsAsync(patientId);
        Assert.True(choices.Succeeded, choices.Error);
        var choice = Assert.Single(choices.Value!, row => row.BloodUnitId == unitId);
        Assert.Equal($"Location {locationId}", choice.StorageLocationName);
        Assert.DoesNotContain(code, choice.StorageLocationName, StringComparison.OrdinalIgnoreCase);

        await SeedCrossmatchCatalogAsync(read);
        var loc = new OrderingLocation { Code = "L224", Name = "BB", IsActive = true };
        var enc = new Encounter
        {
            PatientId = patientId,
            VisitNumber = "VIS-224",
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow.AddHours(-2)
        };
        read.OrderingLocations.Add(loc);
        read.Encounters.Add(enc);
        await read.SaveChangesAsync();

        var alloc = await Allocations(read).AllocateAsync(patientId, new AllocatePatientUnitRequest(
            unitId, enc.Id, null, loc.Id, null, "CXM"));
        Assert.True(alloc.Succeeded, alloc.Error);
        Assert.Equal($"Location {locationId}", alloc.Value!.Allocation.StorageLocationName);
        var reserved = Assert.Single(await Allocations(read).ListActiveAsync(patientId), row => row.UnitNumber == unitNumber);
        Assert.Equal($"Location {locationId}", reserved.StorageLocationName);
        Assert.DoesNotContain(code, reserved.StorageLocationName!, StringComparison.OrdinalIgnoreCase);
    }
}
