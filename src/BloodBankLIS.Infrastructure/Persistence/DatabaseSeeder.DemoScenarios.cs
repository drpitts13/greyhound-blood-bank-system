using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Extended demo data. Each scenario exercises a distinct slice of the system so a fresh
/// development database can demonstrate the rules engine, antibody workups, emergency
/// release, transfusion reactions, product modifications, and ISBT 128 component identity
/// without anyone hand-entering data. Every method is independently idempotent.
/// </summary>
public static partial class DatabaseSeeder
{
    private static readonly (AboGroup Abo, RhType Rh)[] AllBloodTypes =
    [
        (AboGroup.O, RhType.Positive),
        (AboGroup.O, RhType.Negative),
        (AboGroup.A, RhType.Positive),
        (AboGroup.A, RhType.Negative),
        (AboGroup.B, RhType.Positive),
        (AboGroup.B, RhType.Negative),
        (AboGroup.AB, RhType.Positive),
        (AboGroup.AB, RhType.Negative)
    ];

    /// <summary>Product codes produced by a modification, kept out of the base catalog seed.</summary>
    private const string IrradiatedRedCellsCode = "RBC-IRR";
    private const string WashedRedCellsCode = "RBC-WASH";
    private const string ThawedPlasmaCode = "FFP-THAW";

    private static async Task SeedExtendedDemoScenariosAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await SeedExtendedInventoryAsync(context, ct);
        await SeedRetypeDemoUnitsAsync(context, ct);
        await SeedNeonatalScenarioAsync(context, ct);
        await SeedRhNegativeScenarioAsync(context, ct);
        await SeedAntibodyScenarioAsync(context, ct);
        await SeedTraumaScenarioAsync(context, ct);
        await SeedTransfusionReactionScenarioAsync(context, ct);
        await SeedModificationScenarioAsync(context, ct);
        await SeedIsbtScenarioAsync(context, ct);
    }

    // ---------------------------------------------------------------------
    // Reference data the scenarios depend on
    // ---------------------------------------------------------------------

    /// <summary>
    /// The seeded <c>NEO-TYPE-AND-SCREEN</c> rule adds <c>TSNEO</c>. Without a matching
    /// definition the rule would add an order line for a test the catalog does not know.
    /// </summary>
    private static async Task EnsureNeonatalTypeAndScreenTestAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.TestDefinitions.AnyAsync(t => t.Code == "TSNEO", ct))
        {
            return;
        }

        context.TestDefinitions.Add(new TestDefinition
        {
            Code = "TSNEO",
            Name = "Neonatal Type and Screen",
            Category = TestCategory.AboRh,
            ResultValueType = ResultValueType.AboRh,
            VerificationRequired = true,
            ContributesToCompatibility = true,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = DateTime.UtcNow,
            Version = 1
        });

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Target products for the modification paths. Added separately from the base catalog so
    /// databases seeded before modifications existed pick them up on the next startup.
    /// </summary>
    private static async Task EnsureModificationProductTypesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await AddMissingByCodeAsync(
            context,
            context.ProductTypes,
            p => p.ProductCode,
            [
                new ProductType
                {
                    ProductCode = IrradiatedRedCellsCode,
                    Name = "Red Blood Cells, Irradiated",
                    ComponentClass = ComponentClass.RedBloodCells,
                    RequiresCrossmatch = true,
                    RequiresAboMatch = true,
                    RequiresRhMatch = true,
                    StorageRequirements = "1-6C",
                    DefaultShelfLifeHours = 28 * 24,
                    Isbt128ProductCode = "E0332"
                },
                new ProductType
                {
                    ProductCode = WashedRedCellsCode,
                    Name = "Red Blood Cells, Washed",
                    ComponentClass = ComponentClass.RedBloodCells,
                    RequiresCrossmatch = true,
                    RequiresAboMatch = true,
                    RequiresRhMatch = true,
                    StorageRequirements = "1-6C",
                    DefaultShelfLifeHours = 24,
                    Isbt128ProductCode = "E5169"
                },
                new ProductType
                {
                    ProductCode = ThawedPlasmaCode,
                    Name = "Thawed Plasma",
                    ComponentClass = ComponentClass.Plasma,
                    RequiresCrossmatch = false,
                    StorageRequirements = "1-6C",
                    DefaultShelfLifeHours = 5 * 24,
                    Isbt128ProductCode = "E0701"
                }
            ],
            ct);
    }

    /// <summary>
    /// Stamps ISBT product description codes on facility product types so modification
    /// rules and inventory show E-codes (e.g. E0336) instead of internal aliases.
    /// </summary>
    private static async Task EnsureProductTypeIsbtCodesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var assigned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RBC-LR"] = "E0336",
            [IrradiatedRedCellsCode] = "E0332",
            [WashedRedCellsCode] = "E5169",
            ["FFP"] = "E0701",
            [ThawedPlasmaCode] = "E0701",
            ["WB"] = "E0023",
            ["PLT-A"] = "E3077"
        };

        var products = await context.ProductTypes.ToListAsync(ct);
        var changed = false;
        foreach (var product in products)
        {
            if (!assigned.TryGetValue(product.ProductCode, out var pdc)
                || string.Equals(product.Isbt128ProductCode, pdc, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            product.Isbt128ProductCode = pdc;
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Catalog of expiration offsets used by modification rules. 24H / 5D / 28D are
    /// relative to modification time; 42D is relative to collection.
    /// </summary>
    private static async Task SeedExpirationModificationCodesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await AddMissingByCodeAsync(
            context,
            context.ExpirationModificationCodes,
            c => c.Code,
            [
                new ExpirationModificationCode
                {
                    Code = "24H",
                    OffsetAmount = 24,
                    OffsetUnit = ExpirationOffsetUnit.Hours,
                    RelativeTo = ExpirationRelativeTo.ModificationDateTime,
                    Description = "24 hours from modification",
                    IsActive = true,
                    Version = 1
                },
                new ExpirationModificationCode
                {
                    Code = "5D",
                    OffsetAmount = 5,
                    OffsetUnit = ExpirationOffsetUnit.Days,
                    RelativeTo = ExpirationRelativeTo.ModificationDateTime,
                    Description = "5 days from modification",
                    IsActive = true,
                    Version = 1
                },
                new ExpirationModificationCode
                {
                    Code = "28D",
                    OffsetAmount = 28,
                    OffsetUnit = ExpirationOffsetUnit.Days,
                    RelativeTo = ExpirationRelativeTo.ModificationDateTime,
                    Description = "28 days from modification",
                    IsActive = true,
                    Version = 1
                },
                new ExpirationModificationCode
                {
                    Code = "42D",
                    OffsetAmount = 42,
                    OffsetUnit = ExpirationOffsetUnit.Days,
                    RelativeTo = ExpirationRelativeTo.CollectionDateTime,
                    Description = "42 days from collection",
                    IsActive = true,
                    Version = 1
                }
            ],
            ct);
    }

    /// <summary>
    /// Active modification paths so the modifications workspace has something to run.
    /// Expiration offsets follow AABB practice: irradiation caps at 28 days, washing at
    /// 24 hours, thawed plasma at 5 days. Each is still capped at the source expiration.
    /// </summary>
    private static async Task SeedModificationRulesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.ModificationRules.AnyAsync(ct))
        {
            return;
        }

        var products = await context.ProductTypes.ToDictionaryAsync(p => p.ProductCode, ct);
        var codes = await context.ExpirationModificationCodes.ToDictionaryAsync(c => c.Code, ct);
        if (!products.TryGetValue("RBC-LR", out var redCells)
            || !products.TryGetValue("FFP", out var plasma)
            || !products.TryGetValue(IrradiatedRedCellsCode, out var irradiated)
            || !products.TryGetValue(WashedRedCellsCode, out var washed)
            || !products.TryGetValue(ThawedPlasmaCode, out var thawed)
            || !codes.TryGetValue("28D", out var irradiateExpiry)
            || !codes.TryGetValue("24H", out var washExpiry)
            || !codes.TryGetValue("5D", out var thawExpiry))
        {
            return;
        }

        context.ModificationRules.AddRange(
            new ModificationRule
            {
                ModificationCode = "IRR-RBC-LR",
                SourceProductTypeId = redCells.Id,
                ModificationType = ModificationType.Irradiate,
                TargetProductTypeId = irradiated.Id,
                ExpirationModificationCodeId = irradiateExpiry.Id,
                Description = "Irradiate leukoreduced red cells for cellular immunodeficiency or directed donation.",
                IsActive = true,
                Version = 1
            },
            new ModificationRule
            {
                ModificationCode = "WASH-RBC-LR",
                SourceProductTypeId = redCells.Id,
                ModificationType = ModificationType.Wash,
                TargetProductTypeId = washed.Id,
                ExpirationModificationCodeId = washExpiry.Id,
                Description = "Saline wash red cells to remove plasma proteins for IgA deficient recipients.",
                IsActive = true,
                Version = 1
            },
            new ModificationRule
            {
                ModificationCode = "THAW-FFP",
                SourceProductTypeId = plasma.Id,
                ModificationType = ModificationType.Thaw,
                TargetProductTypeId = thawed.Id,
                ExpirationModificationCodeId = thawExpiry.Id,
                Description = "Thaw fresh frozen plasma for transfusion.",
                IsActive = true,
                Version = 1
            });

        await context.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------
    // Inventory
    // ---------------------------------------------------------------------

    /// <summary>
    /// Stocks every ABO/Rh combination across red cells, plasma, and platelets so
    /// compatibility and inventory screens have realistic depth, plus a handful of units in
    /// non-available states.
    /// </summary>
    private static async Task SeedExtendedInventoryAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodUnits.AnyAsync(u => u.UnitNumber == "W0001230001001", ct))
        {
            return;
        }

        var products = await context.ProductTypes.ToDictionaryAsync(p => p.ProductCode, ct);
        var locations = await context.InventoryLocations.ToDictionaryAsync(l => l.Code, ct);
        if (!products.TryGetValue("RBC-LR", out var redCells)
            || !products.TryGetValue("FFP", out var plasma)
            || !products.TryGetValue("PLT-A", out var platelets)
            || !locations.TryGetValue("FRIDGE-1", out var fridge)
            || !locations.TryGetValue("FREEZER-1", out var freezer))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var serial = 1001;
        var units = new List<BloodUnit>();

        foreach (var (abo, rh) in AllBloodTypes)
        {
            units.Add(NewUnit(serial++, redCells.Id, abo, rh, fridge.Id, now.AddDays(28), 300m));
            units.Add(NewUnit(serial++, plasma.Id, abo, rh, freezer.Id, now.AddDays(300), 250m));

            // Platelets belong at room temperature with agitation; the location catalog has no
            // such type, so the main refrigerator stands in for the demo.
            units.Add(NewUnit(serial++, platelets.Id, abo, rh, fridge.Id, now.AddDays(4), 300m));
        }

        // A few units that are not simply available, so the worklists show real variety.
        var expiringSoon = NewUnit(serial++, redCells.Id, AboGroup.A, RhType.Positive, fridge.Id, now.AddHours(18), 300m);

        var quarantined = NewUnit(serial++, redCells.Id, AboGroup.B, RhType.Positive, fridge.Id, now.AddDays(20), 300m);
        quarantined.Status = UnitStatus.Quarantine;
        quarantined.QuarantineReason = "Segment tubing damaged on receipt; pending supervisor review.";

        var discarded = NewUnit(serial++, platelets.Id, AboGroup.O, RhType.Positive, fridge.Id, now.AddDays(-1), 300m);
        discarded.Status = UnitStatus.Discarded;
        discarded.DiscardReason = "Expired before issue.";

        var expired = NewUnit(serial++, redCells.Id, AboGroup.AB, RhType.Negative, fridge.Id, now.AddDays(-3), 300m);
        expired.Status = UnitStatus.Expired;

        var held = NewUnit(serial, redCells.Id, AboGroup.O, RhType.Negative, fridge.Id, now.AddDays(22), 300m);
        held.Status = UnitStatus.OnHold;
        held.HoldReason = "Pending supplier packing slip; operational hold, not a quality quarantine.";

        units.AddRange([expiringSoon, quarantined, discarded, expired, held]);

        context.BloodUnits.AddRange(units);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Two Received RBC units so the retype worklist is not empty on a demo database.
    /// Existing Available inventory is left unchanged.
    /// </summary>
    private static async Task SeedRetypeDemoUnitsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodUnits.AnyAsync(u => u.UnitNumber == "W000123RET0001", ct))
        {
            return;
        }

        var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
        var fridge = await context.InventoryLocations.FirstOrDefaultAsync(l => l.Code == "FRIDGE-1", ct);
        if (redCells is null || fridge is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        context.BloodUnits.AddRange(
            new BloodUnit
            {
                UnitNumber = "W000123RET0001",
                ProductTypeId = redCells.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = now.AddDays(28),
                CurrentLocationId = fridge.Id,
                Status = UnitStatus.Received,
                Volume = 300m,
                CollectionFacility = "Regional Blood Center",
                Supplier = "Regional Blood Center",
                CollectedUtc = now.AddDays(-1)
            },
            new BloodUnit
            {
                UnitNumber = "W000123RET0002",
                ProductTypeId = redCells.Id,
                Abo = AboGroup.A,
                RhD = RhType.Negative,
                ExpiresUtc = now.AddDays(26),
                CurrentLocationId = fridge.Id,
                Status = UnitStatus.Received,
                Volume = 300m,
                CollectionFacility = "Regional Blood Center",
                Supplier = "Regional Blood Center",
                CollectedUtc = now.AddDays(-1)
            });

        await context.SaveChangesAsync(ct);
    }

    private static BloodUnit NewUnit(
        int serial,
        long productTypeId,
        AboGroup abo,
        RhType rh,
        long locationId,
        DateTime expiresUtc,
        decimal volume) => new()
        {
            UnitNumber = $"W000123{serial:D7}",
            ProductTypeId = productTypeId,
            Abo = abo,
            RhD = rh,
            ExpiresUtc = expiresUtc,
            CurrentLocationId = locationId,
            Status = UnitStatus.Available,
            Volume = volume,
            CollectionFacility = "Regional Blood Center",
            Supplier = "Regional Blood Center",
            CollectedUtc = DateTime.UtcNow.AddDays(-7)
        };

    // ---------------------------------------------------------------------
    // Patient scenarios
    // ---------------------------------------------------------------------

    /// <summary>
    /// A patient under one day old with a standard type and screen on the order. Activating
    /// the <c>NEO-TYPE-AND-SCREEN</c> rule and re-ordering demonstrates the swap to TSNEO.
    /// </summary>
    private static async Task SeedNeonatalScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0002", ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var visit = await AddPatientVisitAsync(
            context,
            mrn: "MRN0002",
            last: "Newborn",
            first: "Baby Boy",
            dateOfBirth: DateOnly.FromDateTime(now),
            sex: Sex.Male,
            visitNumber: "VIS-2026-010",
            encounterType: EncounterType.Inpatient,
            currentLocation: "NICU",
            accession: "ACC0010",
            specimenType: "EDTA",
            collectedUtc: now.AddMinutes(-90),
            ct);

        var orderingLocation = await context.OrderingLocations.FirstAsync(l => l.Code == "ICU", ct);
        var provider = await context.OrderingProviders.FirstOrDefaultAsync(p => p.ProviderId == "PROV-LEE", ct);

        var order = new Order
        {
            OrderNumber = "ORD0010",
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderingLocationId = orderingLocation.Id,
            OrderCategory = OrderCategory.Test,
            OrderName = "Type and Screen",
            OrderType = OrderType.TypeAndScreen,
            TestCode = "TNS",
            Priority = OrderPriority.Stat,
            Status = OrderStatus.InProcess,
            Source = OrderSource.Manual,
            OrderingProviderId = provider?.Id,
            OrderingProvider = provider?.Name,
            OrderedUtc = now.AddMinutes(-80),
            ResultStatus = ResultStatus.Pending
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync(ct);

        context.OrderLines.AddRange(
            new OrderLine { OrderId = order.Id, LineNumber = 1, LineCategory = OrderCategory.Test, LineName = "ABO/Rh Type", TestCode = "ABORH", OrderType = OrderType.AboRh },
            new OrderLine { OrderId = order.Id, LineNumber = 2, LineCategory = OrderCategory.Test, LineName = "Antibody Screen", TestCode = "ABSC", OrderType = OrderType.AntibodyScreen });

        context.OrderSpecimens.Add(new OrderSpecimen { OrderId = order.Id, SpecimenId = visit.Specimen.Id, IsPrimary = true });

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// An Rh negative type. With the <c>ABORH-RHNEG-WEAKD</c> rule active, verifying an
    /// A Negative ABO/Rh reflexes a Weak D test; the verified result here shows the outcome.
    /// </summary>
    private static async Task SeedRhNegativeScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0003", ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var visit = await AddPatientVisitAsync(
            context,
            mrn: "MRN0003",
            last: "Rhesus",
            first: "Nora",
            dateOfBirth: new DateOnly(1992, 9, 3),
            sex: Sex.Female,
            visitNumber: "VIS-2026-011",
            encounterType: EncounterType.Outpatient,
            currentLocation: "Outpatient Clinic",
            accession: "ACC0011",
            specimenType: "EDTA",
            collectedUtc: now.AddHours(-5),
            ct);

        var orderingLocation = await context.OrderingLocations.FirstAsync(l => l.Code == "OPLAB", ct);
        var provider = await context.OrderingProviders.FirstOrDefaultAsync(p => p.ProviderId == "PROV-JONES", ct);

        var order = new Order
        {
            OrderNumber = "ORD0011",
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderingLocationId = orderingLocation.Id,
            OrderCategory = OrderCategory.Test,
            OrderName = "Type and Screen",
            OrderType = OrderType.TypeAndScreen,
            TestCode = "TNS",
            Priority = OrderPriority.Routine,
            Status = OrderStatus.Completed,
            Source = OrderSource.Manual,
            OrderingProviderId = provider?.Id,
            OrderingProvider = provider?.Name,
            OrderedUtc = now.AddHours(-5),
            ResultStatus = ResultStatus.Verified
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync(ct);

        context.OrderLines.AddRange(
            new OrderLine { OrderId = order.Id, LineNumber = 1, LineCategory = OrderCategory.Test, LineName = "ABO/Rh Type", TestCode = "ABORH", OrderType = OrderType.AboRh, ResultStatus = ResultStatus.Verified },
            new OrderLine { OrderId = order.Id, LineNumber = 2, LineCategory = OrderCategory.Test, LineName = "Antibody Screen", TestCode = "ABSC", OrderType = OrderType.AntibodyScreen, ResultStatus = ResultStatus.Verified },
            new OrderLine { OrderId = order.Id, LineNumber = 3, LineCategory = OrderCategory.Test, LineName = "Weak D Test", TestCode = "WEAKD", OrderType = OrderType.Other, ResultStatus = ResultStatus.Verified });

        context.OrderSpecimens.Add(new OrderSpecimen { OrderId = order.Id, SpecimenId = visit.Specimen.Id, IsPrimary = true });

        context.TestResults.AddRange(
            VerifiedResult(visit, order.Id, "ABORH", AboRhResultValue.Format(AboGroup.A, RhType.Negative), now.AddHours(-4)),
            VerifiedResult(visit, order.Id, "ABSC", "Negative", now.AddHours(-4)),
            VerifiedResult(visit, order.Id, "WEAKD", "Negative", now.AddHours(-3)));

        context.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = visit.Patient.Id,
            Abo = AboGroup.A,
            RhD = RhType.Negative,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A patient with a clinically significant anti-K. The antibody history, the patient's
    /// own K negative phenotype, and K negative units make the antigen-negative selection
    /// path in compatibility testing demonstrable.
    /// </summary>
    private static async Task SeedAntibodyScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0004", ct))
        {
            return;
        }

        var kell = await context.BloodAttributeDefinitions.FirstOrDefaultAsync(a => a.Code == "K", ct);
        if (kell is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var visit = await AddPatientVisitAsync(
            context,
            mrn: "MRN0004",
            last: "Kellerman",
            first: "Diane",
            dateOfBirth: new DateOnly(1957, 2, 18),
            sex: Sex.Female,
            visitNumber: "VIS-2026-012",
            encounterType: EncounterType.Inpatient,
            currentLocation: "6E Oncology",
            accession: "ACC0012",
            specimenType: "EDTA",
            collectedUtc: now.AddHours(-6),
            ct);

        var orderingLocation = await context.OrderingLocations.FirstAsync(l => l.Code == "ICU", ct);
        var provider = await context.OrderingProviders.FirstOrDefaultAsync(p => p.ProviderId == "PROV-SMITH", ct);

        var screenOrder = new Order
        {
            OrderNumber = "ORD0012",
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderingLocationId = orderingLocation.Id,
            OrderCategory = OrderCategory.Test,
            OrderName = "Type and Screen",
            OrderType = OrderType.TypeAndScreen,
            TestCode = "TNS",
            Priority = OrderPriority.Routine,
            Status = OrderStatus.Completed,
            Source = OrderSource.Manual,
            OrderingProviderId = provider?.Id,
            OrderingProvider = provider?.Name,
            OrderedUtc = now.AddHours(-6),
            ResultStatus = ResultStatus.Verified
        };
        var idOrder = new Order
        {
            OrderNumber = "ORD0013",
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderingLocationId = orderingLocation.Id,
            OrderCategory = OrderCategory.Test,
            OrderName = "Antibody Identification",
            OrderType = OrderType.AntibodyIdentification,
            TestCode = "ABID",
            Priority = OrderPriority.Routine,
            Status = OrderStatus.Completed,
            Source = OrderSource.Manual,
            OrderingProviderId = provider?.Id,
            OrderingProvider = provider?.Name,
            OrderedUtc = now.AddHours(-5),
            ResultStatus = ResultStatus.Verified
        };
        context.Orders.AddRange(screenOrder, idOrder);
        await context.SaveChangesAsync(ct);

        context.OrderLines.AddRange(
            new OrderLine { OrderId = screenOrder.Id, LineNumber = 1, LineCategory = OrderCategory.Test, LineName = "ABO/Rh Type", TestCode = "ABORH", OrderType = OrderType.AboRh, ResultStatus = ResultStatus.Verified },
            new OrderLine { OrderId = screenOrder.Id, LineNumber = 2, LineCategory = OrderCategory.Test, LineName = "Antibody Screen", TestCode = "ABSC", OrderType = OrderType.AntibodyScreen, ResultStatus = ResultStatus.Verified },
            new OrderLine { OrderId = idOrder.Id, LineNumber = 1, LineCategory = OrderCategory.Test, LineName = "Antibody Identification", TestCode = "ABID", OrderType = OrderType.AntibodyIdentification, ResultStatus = ResultStatus.Verified });

        context.OrderSpecimens.AddRange(
            new OrderSpecimen { OrderId = screenOrder.Id, SpecimenId = visit.Specimen.Id, IsPrimary = true },
            new OrderSpecimen { OrderId = idOrder.Id, SpecimenId = visit.Specimen.Id, IsPrimary = true });

        context.TestResults.AddRange(
            VerifiedResult(visit, screenOrder.Id, "ABORH", AboRhResultValue.Format(AboGroup.O, RhType.Positive), now.AddHours(-5)),
            VerifiedResult(visit, screenOrder.Id, "ABSC", "Positive", now.AddHours(-5)),
            VerifiedResult(visit, idOrder.Id, "ABID", "anti-K", now.AddHours(-4)));

        context.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = visit.Patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

        context.AntibodyHistory.Add(new AntibodyHistory
        {
            PatientId = visit.Patient.Id,
            BloodAttributeDefinitionId = kell.Id,
            AntibodySpecificity = "anti-K",
            Status = AntibodyStatus.Identified,
            IsActive = true,
            Comment = "Identified on a three-cell screen with a full eleven-cell panel."
        });

        context.AntigenProfiles.Add(new AntigenProfile
        {
            PatientId = visit.Patient.Id,
            BloodAttributeDefinitionId = kell.Id,
            Result = AntigenResult.Negative,
            Method = "Serologic",
            TestedUtc = now.AddHours(-4),
            TestedBy = "tech1"
        });

        await context.SaveChangesAsync(ct);

        // Two O positive red cell units typed K negative, suitable for this patient.
        var candidates = await context.BloodUnits
            .Where(u => u.Abo == AboGroup.O && u.RhD == RhType.Positive && u.Status == UnitStatus.Available)
            .OrderBy(u => u.UnitNumber)
            .Take(2)
            .ToListAsync(ct);

        foreach (var unit in candidates)
        {
            context.UnitBloodAttributes.Add(new UnitBloodAttribute
            {
                BloodProductId = unit.Id,
                BloodAttributeDefinitionId = kell.Id,
                AttributeKind = BloodAttributeKind.Antigen,
                Result = AntigenResult.Negative
            });
        }

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Uncrossmatched O negative red cells released to a trauma bay, with the override and
    /// emergency issue records the release path is required to produce.
    /// </summary>
    private static async Task SeedTraumaScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0005", ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var visit = await AddPatientVisitAsync(
            context,
            mrn: "MRN0005",
            last: "Trauma",
            first: "John Doe",
            dateOfBirth: new DateOnly(1988, 6, 30),
            sex: Sex.Male,
            visitNumber: "VIS-2026-013",
            encounterType: EncounterType.Emergency,
            currentLocation: "Trauma Bay 2",
            accession: "ACC0013",
            specimenType: "EDTA",
            collectedUtc: now.AddMinutes(-25),
            ct);

        var edLocation = await context.OrderingLocations.FirstAsync(l => l.Code == "ED", ct);
        var redCells = await context.ProductTypes.FirstAsync(p => p.ProductCode == "RBC-LR", ct);

        var order = new Order
        {
            OrderNumber = "ORD0014",
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderingLocationId = edLocation.Id,
            OrderCategory = OrderCategory.Product,
            OrderName = "Emergency Release Red Blood Cells",
            OrderType = OrderType.Other,
            ProductTypeId = redCells.Id,
            Priority = OrderPriority.EmergencyRelease,
            Status = OrderStatus.InProcess,
            Source = OrderSource.Manual,
            OrderedUtc = now.AddMinutes(-20),
            FulfillmentStatus = FulfillmentStatus.PartiallyFulfilled
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync(ct);

        context.OrderLines.Add(new OrderLine
        {
            OrderId = order.Id,
            LineNumber = 1,
            LineCategory = OrderCategory.Product,
            LineName = "Red Blood Cells",
            ProductTypeId = redCells.Id,
            OrderType = OrderType.Other,
            FulfillmentStatus = FulfillmentStatus.PartiallyFulfilled
        });
        await context.SaveChangesAsync(ct);

        var unit = await UncommittedUnits(context)
            .Where(u => u.Abo == AboGroup.O
                && u.RhD == RhType.Negative
                && u.ProductTypeId == redCells.Id)
            .OrderBy(u => u.UnitNumber)
            .FirstOrDefaultAsync(ct);
        if (unit is null)
        {
            return;
        }

        unit.Status = UnitStatus.Issued;

        var release = new Override
        {
            Action = OverrideAction.EmergencyRelease,
            ContextType = nameof(Issue),
            ContextId = 0,
            RuleCode = "ISS-XM-REQUIRED",
            Reason = "Massive haemorrhage; physician requested uncrossmatched O negative.",
            AuthorizedBy = "Dr. Amy Lee",
            OverriddenUtc = now.AddMinutes(-18)
        };
        context.Overrides.Add(release);
        await context.SaveChangesAsync(ct);

        var allocation = new Allocation
        {
            BloodProductId = unit.Id,
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderId = order.Id,
            SpecimenId = visit.Specimen.Id,
            Status = AllocationStatus.Consumed,
            AssignmentType = AssignmentType.EmergencyRelease,
            AllocatedUtc = now.AddMinutes(-18),
            AllocatedBy = "tech2"
        };
        context.Allocations.Add(allocation);
        await context.SaveChangesAsync(ct);

        var issue = new Issue
        {
            AllocationId = allocation.Id,
            BloodProductId = unit.Id,
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderId = order.Id,
            IssuedUtc = now.AddMinutes(-17),
            IssuedBy = "tech2",
            IssuedTo = "RN Alvarez",
            IssuedToLocation = "Trauma Bay 2",
            IssueType = IssueType.EmergencyRelease,
            CrossmatchStatus = CrossmatchClinicalStatus.NotCrossmatchedEmergency,
            EmergencyReleaseDetails = "Released before ABO/Rh confirmation. Retrospective crossmatch pending.",
            OverrideId = release.Id,
            Status = IssueStatus.Issued,
            UnitExpirationAtIssueUtc = unit.ExpiresUtc
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync(ct);

        release.ContextId = issue.Id;
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A transfusion stopped for a suspected reaction, with the investigation order and the
    /// direct antiglobulin test the workup begins with.
    /// </summary>
    private static async Task SeedTransfusionReactionScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0006", ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var visit = await AddPatientVisitAsync(
            context,
            mrn: "MRN0006",
            last: "Febrile",
            first: "Marcus",
            dateOfBirth: new DateOnly(1974, 11, 22),
            sex: Sex.Male,
            visitNumber: "VIS-2026-014",
            encounterType: EncounterType.Inpatient,
            currentLocation: "3N Medical",
            accession: "ACC0014",
            specimenType: "EDTA",
            collectedUtc: now.AddHours(-8),
            ct);

        var orderingLocation = await context.OrderingLocations.FirstAsync(l => l.Code == "ICU", ct);
        var redCells = await context.ProductTypes.FirstAsync(p => p.ProductCode == "RBC-LR", ct);

        var unit = await UncommittedUnits(context)
            .Where(u => u.Abo == AboGroup.A
                && u.RhD == RhType.Positive
                && u.ProductTypeId == redCells.Id)
            .OrderBy(u => u.UnitNumber)
            .FirstOrDefaultAsync(ct);
        if (unit is null)
        {
            return;
        }

        unit.Status = UnitStatus.Transfused;

        var productOrder = new Order
        {
            OrderNumber = "ORD0015",
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderingLocationId = orderingLocation.Id,
            OrderCategory = OrderCategory.Product,
            OrderName = "Red Blood Cells",
            OrderType = OrderType.Other,
            ProductTypeId = redCells.Id,
            Priority = OrderPriority.Routine,
            Status = OrderStatus.Completed,
            Source = OrderSource.Manual,
            OrderedUtc = now.AddHours(-8),
            FulfillmentStatus = FulfillmentStatus.Complete
        };
        var workupOrder = new Order
        {
            OrderNumber = "ORD0016",
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderingLocationId = orderingLocation.Id,
            OrderCategory = OrderCategory.Test,
            OrderName = "Transfusion Reaction Workup",
            OrderType = OrderType.TransfusionReactionWorkup,
            TestCode = "DAT",
            Priority = OrderPriority.Stat,
            Status = OrderStatus.InProcess,
            Source = OrderSource.Manual,
            OrderedUtc = now.AddHours(-2),
            ResultStatus = ResultStatus.Entered
        };
        context.Orders.AddRange(productOrder, workupOrder);
        await context.SaveChangesAsync(ct);

        context.OrderLines.AddRange(
            new OrderLine { OrderId = productOrder.Id, LineNumber = 1, LineCategory = OrderCategory.Product, LineName = "Red Blood Cells", ProductTypeId = redCells.Id, OrderType = OrderType.Other, FulfillmentStatus = FulfillmentStatus.Complete },
            new OrderLine { OrderId = workupOrder.Id, LineNumber = 1, LineCategory = OrderCategory.Test, LineName = "Direct Antiglobulin Test", TestCode = "DAT", OrderType = OrderType.DirectAntiglobulinTest, ResultStatus = ResultStatus.Entered });

        context.OrderSpecimens.Add(new OrderSpecimen { OrderId = workupOrder.Id, SpecimenId = visit.Specimen.Id, IsPrimary = true });

        context.TestResults.Add(new TestResult
        {
            SpecimenId = visit.Specimen.Id,
            PatientId = visit.Patient.Id,
            OrderId = workupOrder.Id,
            TestCode = "DAT",
            Version = 1,
            Value = "Negative",
            Status = ResultStatus.Entered,
            EnteredBy = "tech1",
            EnteredUtc = now.AddHours(-1)
        });

        await context.SaveChangesAsync(ct);

        var issue = new Issue
        {
            BloodProductId = unit.Id,
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderId = productOrder.Id,
            IssuedUtc = now.AddHours(-4),
            IssuedBy = "tech2",
            IssuedTo = "Marcus Febrile",
            IssuedToLocation = "3N Medical",
            CrossmatchStatus = CrossmatchClinicalStatus.Compatible,
            Status = IssueStatus.Transfused,
            UnitExpirationAtIssueUtc = unit.ExpiresUtc
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync(ct);

        context.TransfusionEvents.Add(new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            PatientId = visit.Patient.Id,
            StartUtc = now.AddHours(-3),
            StopUtc = now.AddHours(-3).AddMinutes(12),
            VolumeTransfused = 60m,
            Transfusionist = "RN Patel",
            Location = "3N Medical",
            ReactionSuspected = true,
            FinalDisposition = TransfusionDisposition.Stopped,
            ReactionActions =
                "Transfusion stopped at 12 minutes. Temperature rose 1.8C with rigors. "
                + "Line kept open with saline, unit and administration set returned to the blood bank, "
                + "clerical check performed, post-transfusion specimen drawn.",
            PostTransfusionObservations = "Febrile non-haemolytic reaction suspected; no visible haemoglobinuria.",
            DocumentedBy = "RN Patel"
        });

        await context.SaveChangesAsync(ct);

        var transfusion = await context.TransfusionEvents.SingleAsync(t => t.IssueId == issue.Id, ct);
        context.ReactionInvestigations.Add(new ReactionInvestigation
        {
            TransfusionEventId = transfusion.Id,
            PatientId = visit.Patient.Id,
            BloodProductId = unit.Id,
            ReportedUtc = now.AddHours(-2),
            ReportedBy = "tech1",
            ReactionType = "Febrile non-hemolytic",
            Severity = ReactionSeverity.Mild,
            ClericalCheckCompleted = true,
            ClericalCheckNotes = "Patient identifiers and unit ABO/Rh concordant.",
            VisualInspectionCompleted = true,
            VisualInspectionAcceptable = true,
            RepeatPatientAboRh = "A Positive",
            RepeatUnitAboRh = "A Positive",
            DatResult = DatWorkupResult.Negative,
            RemainderQuarantined = true,
            Status = ReactionInvestigationStatus.UnderReview,
            Findings = "Temperature rose 1.8C with rigors; DAT negative. Clerical check clear."
        });
        await context.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------
    // Product modifications and ISBT 128
    // ---------------------------------------------------------------------

    /// <summary>
    /// One irradiated and one washed unit, each recorded as a modification linking the
    /// consumed source unit to the resulting product.
    /// </summary>
    private static async Task SeedModificationScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.UnitModifications.AnyAsync(ct))
        {
            return;
        }

        var rules = await context.ModificationRules
            .Where(r => r.IsActive)
            .ToListAsync(ct);
        var irradiateRule = rules.FirstOrDefault(r => r.ModificationType == ModificationType.Irradiate);
        var washRule = rules.FirstOrDefault(r => r.ModificationType == ModificationType.Wash);
        if (irradiateRule is null || washRule is null)
        {
            return;
        }

        var fridge = await context.InventoryLocations.FirstAsync(l => l.Code == "FRIDGE-1", ct);
        var now = DateTime.UtcNow;

        await AddModificationAsync(context, irradiateRule, fridge.Id, 2001, AboGroup.O, RhType.Positive,
            now.AddHours(-6), "Cellular immunodeficiency; irradiation required.", ct);

        await AddModificationAsync(context, washRule, fridge.Id, 2002, AboGroup.A, RhType.Positive,
            now.AddHours(-3), "IgA deficient recipient; plasma proteins removed.", ct);
    }

    private static async Task AddModificationAsync(
        BloodBankDbContext context,
        ModificationRule rule,
        long locationId,
        int resultSerial,
        AboGroup abo,
        RhType rh,
        DateTime performedUtc,
        string reason,
        CancellationToken ct)
    {
        var source = await UncommittedUnits(context)
            .Where(u => u.ProductTypeId == rule.SourceProductTypeId
                && u.Abo == abo
                && u.RhD == rh)
            .OrderBy(u => u.UnitNumber)
            .FirstOrDefaultAsync(ct);
        if (source is null)
        {
            return;
        }

        var expCode = await context.ExpirationModificationCodes.FindAsync([rule.ExpirationModificationCodeId], ct);
        var offset = expCode is not null
            ? new ExpirationOffsetCode(expCode.OffsetAmount, expCode.OffsetUnit)
            : new ExpirationOffsetCode(24, ExpirationOffsetUnit.Hours);
        var resultExpires = Earliest(Apply(offset, performedUtc), source.ExpiresUtc);

        source.Status = UnitStatus.Modified;

        var modification = new UnitModification
        {
            ModificationRuleId = rule.Id,
            ModificationType = rule.ModificationType,
            ExpirationOffsetCodeApplied = expCode?.Code ?? "24H",
            ResultExpiresUtc = resultExpires,
            Reason = reason,
            PerformedBy = "tech1",
            PerformedUtc = performedUtc
        };
        context.UnitModifications.Add(modification);
        await context.SaveChangesAsync(ct);

        var result = NewUnit(resultSerial, rule.TargetProductTypeId, abo, rh, locationId, resultExpires, source.Volume ?? 300m);
        result.DerivedFromModificationId = modification.Id;
        context.BloodUnits.Add(result);
        await context.SaveChangesAsync(ct);

        context.UnitModificationUnits.AddRange(
            new UnitModificationUnit { UnitModificationId = modification.Id, BloodProductId = source.Id, Role = ModificationUnitRole.Source, SortOrder = 0 },
            new UnitModificationUnit { UnitModificationId = modification.Id, BloodProductId = result.Id, Role = ModificationUnitRole.Result, SortOrder = 0 });

        await context.SaveChangesAsync(ct);
    }

    private static DateTime Apply(ExpirationOffsetCode offset, DateTime from) =>
        offset.Unit == ExpirationOffsetUnit.Hours
            ? from.AddHours(offset.Amount)
            : from.AddDays(offset.Amount);

    private static DateTime Earliest(DateTime left, DateTime right) => left < right ? left : right;

    /// <summary>
    /// A unit received by scanning ISBT 128 labels, keeping the raw scans and the completed
    /// scan session so the receive workflow has a worked example to inspect.
    /// </summary>
    private static async Task SeedIsbtScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodComponentScanSessions.AnyAsync(ct))
        {
            return;
        }

        var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
        var fridge = await context.InventoryLocations.FirstOrDefaultAsync(l => l.Code == "FRIDGE-1", ct);
        if (redCells is null || fridge is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        const string donationId = "W123422123456";
        const string productCode = "E0336";
        var expires = now.AddDays(35);

        // ISBT 128 encodes expiration as two-digit year plus day of year.
        var encodedExpiration = $"{expires:yy}{expires.DayOfYear:D3}";

        var unit = new BloodUnit
        {
            UnitNumber = donationId,
            ProductTypeId = redCells.Id,
            Abo = AboGroup.O,
            RhD = RhType.Negative,
            ExpiresUtc = expires,
            CurrentLocationId = fridge.Id,
            Status = UnitStatus.Available,
            Volume = 300m,
            CollectionFacility = "Regional Blood Center",
            Supplier = "Regional Blood Center",
            CollectedUtc = now.AddDays(-7),
            Source = ComponentEntrySource.Scanner,
            Din = donationId,
            Isbt128DonationId = donationId,
            Isbt128ProductCode = productCode,
            ProductDescriptionCode = productCode,
            NominalYear = "22",
            DonationSequence = "123456",
            AboRhdCode = "61",
            ComponentIdentity = $"{donationId}|{productCode}|00",
            ComponentIdentityKey = $"{donationId}:{productCode}:00",
            DivisionCode = "00",
            ExpirationEncoded = $"={encodedExpiration}",
            ExpirationLocal = expires,
            ExpirationHasExplicitTime = false
        };
        context.BloodUnits.Add(unit);
        await context.SaveChangesAsync(ct);

        context.BloodComponentRawScans.AddRange(
            RawScan(unit.Id, IsbtDataStructureKind.DonationIdentificationNumber, "=W1234 22 123456", donationId, now.AddMinutes(-12)),
            RawScan(unit.Id, IsbtDataStructureKind.ProductCode, $"={productCode}00", $"{productCode}00", now.AddMinutes(-11)),
            RawScan(unit.Id, IsbtDataStructureKind.AboRhd, "=%61", "61", now.AddMinutes(-10)),
            RawScan(unit.Id, IsbtDataStructureKind.ExpirationDate, $"&>{encodedExpiration}", encodedExpiration, now.AddMinutes(-9)));

        var session = new BloodComponentScanSession
        {
            SessionKey = Guid.NewGuid(),
            ExpectedStructuresJson = """["DonationIdentificationNumber","ProductCode","AboRhd","ExpirationDate"]""",
            ReceivedStructuresJson = """["DonationIdentificationNumber","ProductCode","AboRhd","ExpirationDate"]""",
            DraftJson = $$"""{"din":"{{donationId}}","productCode":"{{productCode}}","aboRhd":"61"}""",
            StartedAt = now.AddMinutes(-12),
            LastScanAt = now.AddMinutes(-9),
            IsCompleted = true,
            StartedBy = "tech1",
            CompletedComponentIdentity = unit.ComponentIdentity
        };
        context.BloodComponentScanSessions.Add(session);
        await context.SaveChangesAsync(ct);

        context.BloodComponentScanSessionLines.AddRange(
            SessionLine(session.Id, IsbtDataStructureKind.DonationIdentificationNumber, "=W1234 22 123456", donationId, now.AddMinutes(-12)),
            SessionLine(session.Id, IsbtDataStructureKind.ProductCode, $"={productCode}00", $"{productCode}00", now.AddMinutes(-11)),
            SessionLine(session.Id, IsbtDataStructureKind.AboRhd, "=%61", "61", now.AddMinutes(-10)),
            SessionLine(session.Id, IsbtDataStructureKind.ExpirationDate, $"&>{encodedExpiration}", encodedExpiration, now.AddMinutes(-9)));

        context.BloodComponentSpecialTests.Add(new BloodComponentSpecialTest
        {
            BloodProductId = unit.Id,
            TestCode = "CMV",
            Result = "Negative"
        });

        await context.SaveChangesAsync(ct);
    }

    private static BloodComponentRawScan RawScan(
        long unitId,
        IsbtDataStructureKind kind,
        string original,
        string normalized,
        DateTime enteredAt) => new()
        {
            BloodProductId = unitId,
            StructureKind = kind,
            OriginalValue = original,
            SanitizedValue = original.Trim(),
            NormalizedValue = normalized,
            Source = ComponentEntrySource.Scanner,
            EnteredBy = "tech1",
            EnteredAt = enteredAt
        };

    private static BloodComponentScanSessionLine SessionLine(
        long sessionId,
        IsbtDataStructureKind kind,
        string original,
        string sanitized,
        DateTime scannedAt) => new()
        {
            ScanSessionId = sessionId,
            StructureKind = kind,
            OriginalValue = original,
            SanitizedValue = sanitized,
            ScannedAt = scannedAt
        };

    // ---------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------

    private sealed record DemoVisit(Patient Patient, Encounter Encounter, Specimen Specimen);

    /// <summary>
    /// Available units that no scenario has already committed. Without the allocation and
    /// issue checks a scenario could consume the unit another one crossmatched.
    /// </summary>
    private static IQueryable<BloodUnit> UncommittedUnits(BloodBankDbContext context) =>
        context.BloodUnits.Where(u =>
            u.Status == UnitStatus.Available
            && !context.Allocations.Any(a => a.BloodProductId == u.Id)
            && !context.Issues.Any(i => i.BloodProductId == u.Id));

    private static async Task<DemoVisit> AddPatientVisitAsync(
        BloodBankDbContext context,
        string mrn,
        string last,
        string first,
        DateOnly dateOfBirth,
        Sex sex,
        string visitNumber,
        EncounterType encounterType,
        string currentLocation,
        string accession,
        string specimenType,
        DateTime collectedUtc,
        CancellationToken ct)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = mrn,
            LastName = last,
            FirstName = first,
            DateOfBirth = dateOfBirth,
            Sex = sex,
            Status = PatientStatus.Active
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync(ct);

        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = visitNumber,
            EncounterType = encounterType,
            Status = EncounterStatus.Active,
            AdmitUtc = collectedUtc.AddHours(-1),
            CurrentLocation = currentLocation
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(ct);

        var specimen = new Specimen
        {
            AccessionNumber = accession,
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            SpecimenType = specimenType,
            Barcode = $"SPC-{accession}",
            CollectedUtc = collectedUtc,
            ReceivedUtc = collectedUtc.AddMinutes(20),
            ExpiresUtc = collectedUtc.AddDays(3),
            Status = SpecimenStatus.Accepted
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync(ct);

        return new DemoVisit(patient, encounter, specimen);
    }

    private static TestResult VerifiedResult(
        DemoVisit visit,
        long orderId,
        string testCode,
        string value,
        DateTime enteredUtc) => new()
        {
            SpecimenId = visit.Specimen.Id,
            PatientId = visit.Patient.Id,
            OrderId = orderId,
            TestCode = testCode,
            Version = 1,
            Value = value,
            Status = ResultStatus.Verified,
            EnteredBy = "tech1",
            EnteredUtc = enteredUtc,
            VerifiedBy = "tech2",
            VerifiedUtc = enteredUtc.AddMinutes(10)
        };
}
