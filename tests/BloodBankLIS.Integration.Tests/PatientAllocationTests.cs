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
            new EfRepository<AntibodyHistory>(c));

    private CompatibilityService Compatibility(BloodBankDbContext c) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            BloodAttrCompat(c), AntibodyScreenCompat(c), c, _factory.Clock, _factory.CurrentUser);

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), _factory.Clock, c);

    private PatientAllocationService Allocations(BloodBankDbContext c) =>
        new(
            Compatibility(c),
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
            new FixedPermissionEvaluator(3),
            _factory.Clock,
            _factory.CurrentUser,
            c);

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
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        var alloc = await Allocations(c).AllocateAsync(patient.Id, new AllocatePatientUnitRequest(
            unit.Id, enc.Id, null, loc.Id, null, "CXM"));

        Assert.True(alloc.Succeeded, alloc.Error);
        Assert.Equal("CXM", alloc.Value!.CrossmatchTestCode);
        Assert.NotNull(alloc.Value.CrossmatchOrderId);
        Assert.Equal(ProductAllocationDisplayStatus.Reserved, alloc.Value.Allocation.DisplayStatus);

        var list = await Allocations(c).ListActiveAsync(patient.Id);
        Assert.Single(list);
        Assert.Equal(unit.UnitNumber, list[0].UnitNumber);
        Assert.Equal(ProductAllocationDisplayStatus.Reserved, list[0].DisplayStatus);
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
            o.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode));
    }

    [Fact]
    public async Task ListCompatibleUnits_ExcludesAntigenIncompatible()
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
        c.BloodUnits.AddRange(okUnit, badUnit);
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
        Assert.Contains(result.Value!, u => u.UnitNumber.StartsWith("U-KNEG-", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Value!, u => u.UnitNumber.StartsWith("U-KPOS-", StringComparison.Ordinal));
    }
}
