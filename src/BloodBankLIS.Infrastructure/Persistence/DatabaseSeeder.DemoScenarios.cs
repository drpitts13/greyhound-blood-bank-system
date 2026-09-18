using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Extended demo data. Each scenario exercises a distinct slice of the system so a fresh
/// development database can demonstrate the rules engine, antibody workups, emergency
/// release, transfusion reactions, product modifications, ISBT 128 component identity,
/// and FDA/AABB-style validation paths without anyone hand-entering data. Every method
/// is independently idempotent.
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
        await SeedIsbtDivideScenarioAsync(context, ct);
        await SeedIsbtScenarioAsync(context, ct);
        await SeedAlloimmunizationSpecimenScenarioAsync(context, ct);
        await SeedAutologousDirectedScenarioAsync(context, ct);
        await SeedLookbackScenarioAsync(context, ct);
        await SeedExpectedInboundScenarioAsync(context, ct);
        await SeedDiscrepancyScenarioAsync(context, ct);
        await SeedComputerXmEligibleScenarioAsync(context, ct);
        await SeedHl7DataLoadScenarioAsync(context, ct);
        await SeedHl7BpamDataLoadScenarioAsync(context, ct);
        await SeedBillingCaptureScenarioAsync(context, ct);
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
    /// Extra facility product types for receive, issue, and catalog testing. Idempotent
    /// so existing databases pick them up on the next seed.
    /// </summary>
    private static async Task EnsureAdditionalProductTypesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await AddMissingByCodeAsync(
            context,
            context.ProductTypes,
            p => p.ProductCode,
            [
                new ProductType
                {
                    ProductCode = "WB-LR",
                    Name = "Whole Blood, Leukoreduced",
                    ComponentClass = ComponentClass.WholeBlood,
                    RequiresCrossmatch = true,
                    RequiresAboMatch = true,
                    RequiresRhMatch = true,
                    RequiresRetype = true,
                    StorageRequirements = "1-6C",
                    DefaultShelfLifeHours = 35 * 24,
                    Isbt128ProductCode = "E0033"
                },
                new ProductType
                {
                    ProductCode = "RBC-APH",
                    Name = "Apheresis Red Blood Cells, Leukoreduced",
                    ComponentClass = ComponentClass.RedBloodCells,
                    RequiresCrossmatch = true,
                    RequiresAboMatch = true,
                    RequiresRhMatch = true,
                    RequiresRetype = true,
                    StorageRequirements = "1-6C",
                    DefaultShelfLifeHours = 42 * 24,
                    Isbt128ProductCode = "E0685"
                },
                new ProductType
                {
                    ProductCode = "RBC-DEG",
                    Name = "Deglycerolized Red Blood Cells",
                    ComponentClass = ComponentClass.RedBloodCells,
                    RequiresCrossmatch = true,
                    RequiresAboMatch = true,
                    RequiresRhMatch = true,
                    RequiresRetype = true,
                    StorageRequirements = "1-6C",
                    DefaultShelfLifeHours = 24,
                    Isbt128ProductCode = "E4520"
                },
                new ProductType
                {
                    ProductCode = "RBC-FROZ",
                    Name = "Frozen Red Blood Cells",
                    ComponentClass = ComponentClass.RedBloodCells,
                    RequiresCrossmatch = true,
                    RequiresAboMatch = true,
                    RequiresRhMatch = true,
                    RequiresRetype = true,
                    StorageRequirements = "<-65C",
                    DefaultShelfLifeHours = 10 * 365 * 24,
                    Isbt128ProductCode = "E5085"
                },
                new ProductType
                {
                    ProductCode = "PF24",
                    Name = "Plasma Frozen Within 24 Hours",
                    ComponentClass = ComponentClass.Plasma,
                    RequiresCrossmatch = false,
                    RequiresAboMatch = true,
                    RequiresRhMatch = false,
                    StorageRequirements = "<=-18C",
                    DefaultShelfLifeHours = 365 * 24,
                    Isbt128ProductCode = "E0833"
                },
                new ProductType
                {
                    ProductCode = "CRP",
                    Name = "Cryoprecipitate-Reduced Plasma",
                    ComponentClass = ComponentClass.Plasma,
                    RequiresCrossmatch = false,
                    RequiresAboMatch = true,
                    RequiresRhMatch = false,
                    StorageRequirements = "<=-18C",
                    DefaultShelfLifeHours = 365 * 24,
                    Isbt128ProductCode = "E2553"
                },
                new ProductType
                {
                    ProductCode = "CRYO",
                    Name = "Cryoprecipitate",
                    ComponentClass = ComponentClass.Cryoprecipitate,
                    RequiresCrossmatch = false,
                    RequiresAboMatch = false,
                    RequiresRhMatch = false,
                    StorageRequirements = "<=-18C",
                    DefaultShelfLifeHours = 365 * 24,
                    Isbt128ProductCode = "E5165"
                },
                new ProductType
                {
                    ProductCode = "CRYO-P",
                    Name = "Pooled Cryoprecipitate",
                    ComponentClass = ComponentClass.Cryoprecipitate,
                    RequiresCrossmatch = false,
                    RequiresAboMatch = false,
                    RequiresRhMatch = false,
                    StorageRequirements = "<=-18C",
                    DefaultShelfLifeHours = 365 * 24,
                    Isbt128ProductCode = "E5166"
                },
                new ProductType
                {
                    ProductCode = "PLT-P",
                    Name = "Pooled Platelets, Leukoreduced",
                    ComponentClass = ComponentClass.Platelets,
                    RequiresCrossmatch = false,
                    RequiresAboMatch = false,
                    RequiresRhMatch = false,
                    StorageRequirements = "20-24C",
                    DefaultShelfLifeHours = 5 * 24,
                    Isbt128ProductCode = "E6001"
                },
                new ProductType
                {
                    ProductCode = "PLT-IRR",
                    Name = "Apheresis Platelets, Irradiated",
                    ComponentClass = ComponentClass.Platelets,
                    RequiresCrossmatch = false,
                    RequiresAboMatch = false,
                    RequiresRhMatch = false,
                    StorageRequirements = "20-24C",
                    DefaultShelfLifeHours = 5 * 24,
                    Isbt128ProductCode = "E3858"
                },
                new ProductType
                {
                    ProductCode = "GRAN",
                    Name = "Apheresis Granulocytes",
                    ComponentClass = ComponentClass.Granulocytes,
                    RequiresCrossmatch = true,
                    RequiresAboMatch = true,
                    RequiresRhMatch = true,
                    RequiresRetype = true,
                    StorageRequirements = "20-24C",
                    DefaultShelfLifeHours = 24,
                    Isbt128ProductCode = "E4253"
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
            ["WB-LR"] = "E0033",
            ["PLT-A"] = "E3077",
            ["PLT-P"] = "E6001",
            ["PLT-IRR"] = "E3858",
            ["CRYO"] = "E5165",
            ["CRYO-P"] = "E5166",
            ["CRP"] = "E2553",
            ["PF24"] = "E0833",
            ["RBC-APH"] = "E0685",
            ["RBC-DEG"] = "E4520",
            ["RBC-FROZ"] = "E5085",
            ["GRAN"] = "E4253"
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
        var products = await context.ProductTypes.ToDictionaryAsync(p => p.ProductCode, ct);
        var codes = await context.ExpirationModificationCodes.ToDictionaryAsync(c => c.Code, ct);
        if (!products.TryGetValue("RBC-LR", out var redCells)
            || !products.TryGetValue("FFP", out var plasma)
            || !products.TryGetValue("WB", out var wholeBlood)
            || !products.TryGetValue(IrradiatedRedCellsCode, out var irradiated)
            || !products.TryGetValue(WashedRedCellsCode, out var washed)
            || !products.TryGetValue(ThawedPlasmaCode, out var thawed)
            || !codes.TryGetValue("28D", out var irradiateExpiry)
            || !codes.TryGetValue("24H", out var washExpiry)
            || !codes.TryGetValue("5D", out var thawExpiry)
            || !codes.TryGetValue("42D", out var divideExpiry))
        {
            return;
        }

        await EnsureModificationRuleAsync(context, "IRR-RBC-LR", redCells.Id, irradiated.Id,
            ModificationType.Irradiate, irradiateExpiry.Id,
            "Irradiate leukoreduced red cells for cellular immunodeficiency or directed donation.", ct);
        await EnsureModificationRuleAsync(context, "WASH-RBC-LR", redCells.Id, washed.Id,
            ModificationType.Wash, washExpiry.Id,
            "Saline wash red cells to remove plasma proteins for IgA deficient recipients.", ct);
        await EnsureModificationRuleAsync(context, "THAW-FFP", plasma.Id, thawed.Id,
            ModificationType.Thaw, thawExpiry.Id,
            "Thaw fresh frozen plasma for transfusion.", ct);
        await EnsureModificationRuleAsync(context, "DIV-RBC-LR", redCells.Id, redCells.Id,
            ModificationType.Divide, divideExpiry.Id,
            "Divide leukoreduced red cells into ISBT aliquots (V00 → V0A / V0B).", ct);
        await EnsureModificationRuleAsync(context, "DIV-WB-RBC", wholeBlood.Id, redCells.Id,
            ModificationType.Divide, divideExpiry.Id,
            "Divide whole blood into leukoreduced red-cell aliquots using the target product code.", ct);
    }

    private static async Task EnsureModificationRuleAsync(
        BloodBankDbContext context,
        string modificationCode,
        long sourceProductTypeId,
        long targetProductTypeId,
        ModificationType type,
        long expirationCodeId,
        string description,
        CancellationToken ct)
    {
        if (await context.ModificationRules.AnyAsync(r => r.ModificationCode == modificationCode, ct))
        {
            return;
        }

        context.ModificationRules.Add(new ModificationRule
        {
            ModificationCode = modificationCode,
            SourceProductTypeId = sourceProductTypeId,
            ModificationType = type,
            TargetProductTypeId = targetProductTypeId,
            ExpirationModificationCodeId = expirationCodeId,
            Description = description,
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
        quarantined.QuarantineReasonCode = UnitQuarantineReason.VisualDefect;

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
    /// Packing-list units so the expected-inbound worklist can confirm arrival on a
    /// fresh demo database (one on time, one overdue).
    /// </summary>
    private static async Task SeedExpectedInboundScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodUnits.AnyAsync(u => u.UnitNumber == "W000123ASN0001", ct))
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
                UnitNumber = "W000123ASN0001",
                ProductTypeId = redCells.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = now.AddDays(28),
                CurrentLocationId = fridge.Id,
                Status = UnitStatus.Expected,
                Volume = 300m,
                CollectionFacility = "Regional Blood Center",
                Supplier = "Regional Blood Center",
                CollectedUtc = now.AddDays(-2),
                ShipmentId = "ASN-DEMO-01",
                ExpectedArrivalDueUtc = now.AddHours(8)
            },
            new BloodUnit
            {
                UnitNumber = "W000123ASN0002",
                ProductTypeId = redCells.Id,
                Abo = AboGroup.A,
                RhD = RhType.Negative,
                ExpiresUtc = now.AddDays(26),
                CurrentLocationId = fridge.Id,
                Status = UnitStatus.Expected,
                Volume = 300m,
                CollectionFacility = "Regional Blood Center",
                Supplier = "Regional Blood Center",
                CollectedUtc = now.AddDays(-3),
                ShipmentId = "ASN-DEMO-02",
                ExpectedArrivalDueUtc = now.AddHours(-6)
            });
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Two Received RBC units so the retype worklist is not empty on a demo database.
    /// Existing Available inventory is left unchanged.
    /// </summary>
    private static async Task SeedRetypeDemoUnitsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (!await context.BloodUnits.AnyAsync(u => u.UnitNumber == "W000123RET0001", ct))
        {
            var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
            var fridge = await context.InventoryLocations.FirstOrDefaultAsync(l => l.Code == "FRIDGE-1", ct);
            if (redCells is null || fridge is null)
            {
                return;
            }

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

        var posTest = await context.TestDefinitions.FirstOrDefaultAsync(t => t.Code == ProductRetypeAssignment.RhPositiveTestCode, ct);
        var negTest = await context.TestDefinitions.FirstOrDefaultAsync(t => t.Code == ProductRetypeAssignment.RhNegativeTestCode, ct);
        var rhPos = await context.BloodUnits.FirstOrDefaultAsync(u => u.UnitNumber == "W000123RET0001", ct);
        var rhNeg = await context.BloodUnits.FirstOrDefaultAsync(u => u.UnitNumber == "W000123RET0002", ct);
        var changed = false;
        if (posTest is not null && rhPos is not null
            && !await context.ProductRetypeResults.AnyAsync(r => r.BloodProductId == rhPos.Id, ct))
        {
            context.ProductRetypeResults.Add(new ProductRetypeResult
            {
                BloodProductId = rhPos.Id,
                TestDefinitionId = posTest.Id,
                TestCode = posTest.Code,
                Value = string.Empty,
                Status = ResultStatus.Pending,
                EnteredBy = "system",
                EnteredUtc = now
            });
            changed = true;
        }

        if (negTest is not null && rhNeg is not null
            && !await context.ProductRetypeResults.AnyAsync(r => r.BloodProductId == rhNeg.Id, ct))
        {
            context.ProductRetypeResults.Add(new ProductRetypeResult
            {
                BloodProductId = rhNeg.Id,
                TestDefinitionId = negTest.Id,
                TestCode = negTest.Code,
                Value = string.Empty,
                Status = ResultStatus.Pending,
                EnteredBy = "system",
                EnteredUtc = now
            });
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
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

        context.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = visit.Patient.Id,
                Abo = AboGroup.A,
                RhD = RhType.Negative,
                Source = BloodTypeSource.HistoricalImport,
                IsCurrent = false
            },
            new PatientBloodTypeHistory
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

        context.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory
            {
                PatientId = visit.Patient.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Source = BloodTypeSource.HistoricalImport,
                IsCurrent = false
            },
            new PatientBloodTypeHistory
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
            CoolerId = "CLR-TRAUMA-1",
            InTransitDueUtc = now.AddHours(-1),
            IssueType = IssueType.EmergencyRelease,
            CrossmatchStatus = CrossmatchClinicalStatus.NotCrossmatchedEmergency,
            EmergencyReleaseDetails = "Released before ABO/Rh confirmation. Retrospective crossmatch pending.",
            TestsIncompleteAtIssue = true,
            RetrospectiveCrossmatchDueUtc = now.AddHours(-2),
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
    /// ISBT divide inventory: an undivided RBC (V00), a first-level aliquot ready to
    /// subdivide (V0A), a whole-blood unit for the product-change rule, and a completed
    /// divide that already produced V0A / V0B children with tracked volumes.
    /// </summary>
    private static async Task SeedIsbtDivideScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        const string availableRbcDin = "W123425000001";
        const string availableAliquotDin = "W123425000002";
        const string availableWbDin = "W123425000003";
        const string completedSourceDin = "W123425000010";

        var products = await context.ProductTypes.ToDictionaryAsync(p => p.ProductCode, ct);
        var fridge = await context.InventoryLocations.FirstOrDefaultAsync(l => l.Code == "FRIDGE-1", ct);
        if (!products.TryGetValue("RBC-LR", out var redCells)
            || !products.TryGetValue("WB", out var wholeBlood)
            || fridge is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var collected = now.AddDays(-7);

        if (!await context.BloodUnits.AnyAsync(u => u.Din == availableRbcDin, ct))
        {
            context.BloodUnits.Add(NewIsbtUnit(
                availableRbcDin, "E0336V00", redCells.Id, AboGroup.O, RhType.Positive,
                fridge.Id, now.AddDays(28), 300m, collected, UnitStatus.Available));
        }

        if (!await context.BloodUnits.AnyAsync(u => u.Din == availableAliquotDin, ct))
        {
            context.BloodUnits.Add(NewIsbtUnit(
                availableAliquotDin, "E0336V0A", redCells.Id, AboGroup.A, RhType.Positive,
                fridge.Id, now.AddDays(28), 150m, collected, UnitStatus.Available));
        }

        if (!await context.BloodUnits.AnyAsync(u => u.Din == availableWbDin, ct))
        {
            context.BloodUnits.Add(NewIsbtUnit(
                availableWbDin, "E0023V00", wholeBlood.Id, AboGroup.O, RhType.Negative,
                fridge.Id, now.AddDays(28), 450m, collected, UnitStatus.Available));
        }

        await context.SaveChangesAsync(ct);

        if (await context.UnitModifications.AnyAsync(m => m.ModificationType == ModificationType.Divide, ct)
            || await context.BloodUnits.AnyAsync(u => u.Din == completedSourceDin, ct))
        {
            return;
        }

        var divideRule = await context.ModificationRules
            .FirstOrDefaultAsync(r => r.ModificationCode == "DIV-RBC-LR" && r.IsActive, ct);
        if (divideRule is null)
        {
            return;
        }

        var expCode = await context.ExpirationModificationCodes.FindAsync([divideRule.ExpirationModificationCodeId], ct);
        var offset = expCode is not null
            ? new ExpirationOffsetCode(expCode.OffsetAmount, expCode.OffsetUnit)
            : new ExpirationOffsetCode(42, ExpirationOffsetUnit.Days);
        var performedUtc = now.AddHours(-2);
        var sourceExpires = now.AddDays(28);
        var resultExpires = Earliest(Apply(offset, collected), sourceExpires);

        var source = NewIsbtUnit(
            completedSourceDin, "E0336V00", redCells.Id, AboGroup.B, RhType.Positive,
            fridge.Id, sourceExpires, 300m, collected, UnitStatus.Modified);
        context.BloodUnits.Add(source);
        await context.SaveChangesAsync(ct);

        var modification = new UnitModification
        {
            ModificationRuleId = divideRule.Id,
            ModificationType = ModificationType.Divide,
            ExpirationOffsetCodeApplied = expCode?.Code ?? "42D",
            ResultExpiresUtc = resultExpires,
            Reason = "Pediatric split; ISBT V00 divided to V0A and V0B.",
            PerformedBy = "tech1",
            PerformedUtc = performedUtc
        };
        context.UnitModifications.Add(modification);
        await context.SaveChangesAsync(ct);

        var childA = NewIsbtUnit(
            completedSourceDin, "E0336V0A", redCells.Id, AboGroup.B, RhType.Positive,
            fridge.Id, resultExpires, 150m, collected, UnitStatus.Quarantine);
        childA.DerivedFromModificationId = modification.Id;
        childA.QuarantineReasonCode = UnitQuarantineReason.PendingRelease;
        childA.QuarantineReason = "Created by Divide modification";

        var childB = NewIsbtUnit(
            completedSourceDin, "E0336V0B", redCells.Id, AboGroup.B, RhType.Positive,
            fridge.Id, resultExpires, 150m, collected, UnitStatus.Quarantine);
        childB.DerivedFromModificationId = modification.Id;
        childB.QuarantineReasonCode = UnitQuarantineReason.PendingRelease;
        childB.QuarantineReason = "Created by Divide modification";

        context.BloodUnits.AddRange(childA, childB);
        await context.SaveChangesAsync(ct);

        context.UnitModificationUnits.AddRange(
            new UnitModificationUnit { UnitModificationId = modification.Id, BloodProductId = source.Id, Role = ModificationUnitRole.Source, SortOrder = 0 },
            new UnitModificationUnit { UnitModificationId = modification.Id, BloodProductId = childA.Id, Role = ModificationUnitRole.Result, SortOrder = 0 },
            new UnitModificationUnit { UnitModificationId = modification.Id, BloodProductId = childB.Id, Role = ModificationUnitRole.Result, SortOrder = 1 });
        await context.SaveChangesAsync(ct);
    }

    private static BloodUnit NewIsbtUnit(
        string din,
        string productCodeData,
        long productTypeId,
        AboGroup abo,
        RhType rh,
        long locationId,
        DateTime expiresUtc,
        decimal volume,
        DateTime collectedUtc,
        UnitStatus status)
    {
        var pdc = productCodeData[..5];
        var collection = productCodeData[5..6];
        var division = productCodeData[6..8];
        var identity = ComponentIdentityBuilder.Build(din, productCodeData);
        return new BloodUnit
        {
            UnitNumber = identity,
            ComponentIdentity = identity,
            ComponentIdentityKey = ComponentIdentityBuilder.BuildUniquenessKey(din, productCodeData, null),
            ProductTypeId = productTypeId,
            Abo = abo,
            RhD = rh,
            ExpiresUtc = expiresUtc,
            CurrentLocationId = locationId,
            Status = status,
            Volume = volume,
            CollectionFacility = "Regional Blood Center",
            Supplier = "Regional Blood Center",
            CollectedUtc = collectedUtc,
            CollectionDateTime = collectedUtc,
            Source = ComponentEntrySource.Manual,
            Din = din,
            Isbt128DonationId = din,
            ProductCodeData = productCodeData,
            ProductDescriptionCode = pdc,
            CollectionTypeCode = collection,
            DivisionCode = division,
            Isbt128ProductCode = productCodeData,
            Fin = din.Length >= 5 ? din[..5] : din,
            NominalYear = din.Length >= 7 ? din[5..7] : null,
            DonationSequence = din.Length >= 13 ? din[7..13] : null
        };
    }

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
    // FDA / AABB validation scenarios
    // ---------------------------------------------------------------------

    /// <summary>
    /// AABB-style 3-day specimen window: recent pregnancy in the lookback period
    /// forces the 72-hour alloimmunization expiry, not the 168-hour standard window.
    /// </summary>
    private static async Task SeedAlloimmunizationSpecimenScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0007", ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var collectedUtc = now.AddHours(-2);
        var visit = await AddPatientVisitAsync(
            context,
            mrn: "MRN0007",
            last: "Gravid",
            first: "Elena",
            dateOfBirth: new DateOnly(1994, 3, 8),
            sex: Sex.Female,
            visitNumber: "VIS-2026-017",
            encounterType: EncounterType.Inpatient,
            currentLocation: "L&D 2",
            accession: "ACC0017",
            specimenType: "EDTA",
            collectedUtc: collectedUtc,
            ct);

        visit.Patient.RecentPregnancyUtc = now.AddDays(-30);
        visit.Specimen.ExpiresUtc = SpecimenValidityPolicy.ComputeExpiresUtc(collectedUtc, alloimmunizationRisk: true);
        visit.Specimen.Identifier1Type = IdentityTokenType.MedicalRecordNumber;
        visit.Specimen.Identifier1Value = "MRN0007";
        visit.Specimen.Identifier2Type = IdentityTokenType.DateOfBirth;
        visit.Specimen.Identifier2Value = "1994-03-08";
        await context.SaveChangesAsync(ct);

        var orderingLocation = await context.OrderingLocations.FirstAsync(l => l.Code == "OR", ct);
        var provider = await context.OrderingProviders.FirstOrDefaultAsync(p => p.ProviderId == "PROV-JONES", ct);

        var order = new Order
        {
            OrderNumber = "ORD0017",
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderingLocationId = orderingLocation.Id,
            OrderCategory = OrderCategory.Test,
            OrderName = "Type and Screen",
            OrderType = OrderType.TypeAndScreen,
            TestCode = "TNS",
            Priority = OrderPriority.Routine,
            Status = OrderStatus.InProcess,
            Source = OrderSource.Manual,
            OrderingProviderId = provider?.Id,
            OrderingProvider = provider?.Name,
            OrderedUtc = collectedUtc.AddMinutes(10),
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
    /// Autologous and unused directed red cells reserved to one recipient so issue-gate
    /// and directed-to-allogeneic conversion can be demonstrated without hand entry.
    /// </summary>
    private static async Task SeedAutologousDirectedScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0008", ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var visit = await AddPatientVisitAsync(
            context,
            mrn: "MRN0008",
            last: "Autologous",
            first: "Clara",
            dateOfBirth: new DateOnly(1971, 8, 19),
            sex: Sex.Female,
            visitNumber: "VIS-2026-018",
            encounterType: EncounterType.Inpatient,
            currentLocation: "OR Holding",
            accession: "ACC0018",
            specimenType: "EDTA",
            collectedUtc: now.AddHours(-4),
            ct);

        var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
        var fridge = await context.InventoryLocations.FirstOrDefaultAsync(l => l.Code == "FRIDGE-1", ct);
        if (redCells is null || fridge is null)
        {
            return;
        }

        var autologous = NewUnit(900001, redCells.Id, AboGroup.O, RhType.Positive, fridge.Id, now.AddDays(21), 300m);
        autologous.UnitNumber = "W000123AUTO001";
        autologous.DonationRestriction = DonationRestriction.Autologous;
        autologous.ReservedPatientId = visit.Patient.Id;
        autologous.CollectionFacility = "Hospital Autologous Program";
        autologous.Supplier = "Hospital Autologous Program";

        var directed = NewUnit(900002, redCells.Id, AboGroup.O, RhType.Positive, fridge.Id, now.AddDays(24), 300m);
        directed.UnitNumber = "W000123DIR0001";
        directed.DonationRestriction = DonationRestriction.Directed;
        directed.ReservedPatientId = visit.Patient.Id;
        directed.CollectionFacility = "Directed Donor Program";
        directed.Supplier = "Directed Donor Program";

        context.BloodUnits.AddRange(autologous, directed);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 21 CFR 610.46–47 / 606.165 lookback: a transfused DIN plus a sibling component
    /// still in inventory, with a pending recipient notification. The LIS does not
    /// auto-notify patients.
    /// </summary>
    private static async Task SeedLookbackScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodUnits.AnyAsync(u => u.UnitNumber == "W000123LSIB001", ct))
        {
            return;
        }

        const string din = "W123426000001";
        var now = DateTime.UtcNow;
        var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
        var fridge = await context.InventoryLocations.FirstOrDefaultAsync(l => l.Code == "FRIDGE-1", ct);
        if (redCells is null || fridge is null)
        {
            return;
        }

        var transfused = await context.BloodUnits.FirstOrDefaultAsync(u => u.UnitNumber == "W0001230000099", ct);
        var recipient = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "MRN0001", ct);
        if (transfused is null || recipient is null)
        {
            return;
        }

        transfused.Din = din;
        transfused.Isbt128DonationId = din;

        var sibling = NewUnit(900003, redCells.Id, AboGroup.O, RhType.Positive, fridge.Id, now.AddDays(26), 300m);
        sibling.UnitNumber = "W000123LSIB001";
        sibling.Din = din;
        sibling.Isbt128DonationId = din;
        sibling.Status = UnitStatus.Available;
        context.BloodUnits.Add(sibling);
        await context.SaveChangesAsync(ct);

        var issue = await context.Issues.FirstOrDefaultAsync(i => i.BloodProductId == transfused.Id, ct);
        if (!await context.LookbackNotifications.AnyAsync(n => n.Din == din, ct))
        {
            context.LookbackNotifications.Add(new LookbackNotification
            {
                Din = din,
                BloodProductId = transfused.Id,
                PatientId = recipient.Id,
                IssueId = issue?.Id,
                Status = LookbackNotificationStatus.Pending,
                PhysicianOfRecord = "Dr. Smith",
                Reason = "Subsequent donor infectious-disease testing; recipient notification pending facility SOP."
            });
            await context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// 21 CFR 606.165 discrepancy worklist: one missing and one damaged unit.
    /// Neither status is issuable.
    /// </summary>
    private static async Task SeedDiscrepancyScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodUnits.AnyAsync(u => u.UnitNumber == "W000123MISS001", ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var redCells = await context.ProductTypes.FirstOrDefaultAsync(p => p.ProductCode == "RBC-LR", ct);
        var fridge = await context.InventoryLocations.FirstOrDefaultAsync(l => l.Code == "FRIDGE-1", ct);
        if (redCells is null || fridge is null)
        {
            return;
        }

        var missing = NewUnit(900004, redCells.Id, AboGroup.A, RhType.Positive, fridge.Id, now.AddDays(18), 300m);
        missing.UnitNumber = "W000123MISS001";
        missing.Status = UnitStatus.Missing;
        missing.MissingReason = "Not found during physical inventory count.";

        var damaged = NewUnit(900005, redCells.Id, AboGroup.B, RhType.Negative, fridge.Id, now.AddDays(16), 300m);
        damaged.UnitNumber = "W000123DMG0001";
        damaged.Status = UnitStatus.Damaged;
        damaged.DamagedReason = "Bag seam leak discovered on storage inspection.";

        context.BloodUnits.AddRange(missing, damaged);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// AABB 5.16-style computer XM preconditions on Patricia Demo: two concordant
    /// ABO/Rh determinations already exist; a verified negative antibody screen is
    /// added so eligibility can pass when the facility allow-EXM policy is on.
    /// Production use remains gated by OCD-006.
    /// </summary>
    private static async Task SeedComputerXmEligibleScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var patient = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "MRN0001", ct);
        if (patient is null)
        {
            return;
        }

        if (await context.TestResults.AnyAsync(r => r.PatientId == patient.Id && r.TestCode == "ABSC", ct))
        {
            return;
        }

        var order = await context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == "ORD0001", ct);
        var specimen = await context.Specimens.FirstOrDefaultAsync(s => s.AccessionNumber == "ACC0001", ct);
        if (order is null || specimen is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        context.TestResults.Add(new TestResult
        {
            SpecimenId = specimen.Id,
            PatientId = patient.Id,
            OrderId = order.Id,
            TestCode = "ABSC",
            Version = 1,
            Value = "Negative",
            Status = ResultStatus.Verified,
            EnteredBy = "tech1",
            EnteredUtc = now.AddMinutes(-28),
            VerifiedBy = "tech2",
            VerifiedUtc = now.AddMinutes(-18)
        });

        var abscLine = await context.OrderLines.FirstOrDefaultAsync(
            l => l.OrderId == order.Id && l.TestCode == "ABSC", ct);
        if (abscLine is not null)
        {
            abscLine.ResultStatus = ResultStatus.Verified;
        }

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// End-to-end inbound load: ADT patient/visit, ORM type-and-screen, accepted
    /// specimen, and ORU antibody screen waiting for verification. Message logs
    /// are stored so /hl7 can show the accepted load without re-keying.
    /// </summary>
    private static async Task SeedHl7DataLoadScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0009", ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var visit = await AddPatientVisitAsync(
            context,
            mrn: "MRN0009",
            last: "Interface",
            first: "Helen",
            dateOfBirth: new DateOnly(1988, 4, 17),
            sex: Sex.Female,
            visitNumber: "VIS-2026-HL7",
            encounterType: EncounterType.Inpatient,
            currentLocation: "4W Oncology",
            accession: "ACC-HL7-0009",
            specimenType: "EDTA",
            collectedUtc: now.AddHours(-3),
            ct);

        var orderingLocation = await context.OrderingLocations.FirstAsync(l => l.Code == "ED", ct);
        var order = new Order
        {
            OrderNumber = "PLACER-HL7-0009",
            PatientId = visit.Patient.Id,
            EncounterId = visit.Encounter.Id,
            OrderingLocationId = orderingLocation.Id,
            OrderCategory = OrderCategory.Test,
            OrderName = "Type and Screen",
            OrderType = OrderType.TypeAndScreen,
            TestCode = "TNS",
            Priority = OrderPriority.Stat,
            Status = OrderStatus.InProcess,
            Source = OrderSource.Hl7,
            OrderedUtc = now.AddHours(-2),
            ResultStatus = ResultStatus.PendingVerification
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync(ct);

        context.OrderLines.Add(new OrderLine
        {
            OrderId = order.Id,
            LineNumber = 1,
            LineCategory = OrderCategory.Test,
            LineName = "Antibody Screen",
            TestCode = "ABSC",
            OrderType = OrderType.AntibodyScreen,
            ResultStatus = ResultStatus.PendingVerification
        });
        context.OrderSpecimens.Add(new OrderSpecimen
        {
            OrderId = order.Id,
            SpecimenId = visit.Specimen.Id,
            IsPrimary = true
        });
        context.TestResults.Add(new TestResult
        {
            SpecimenId = visit.Specimen.Id,
            PatientId = visit.Patient.Id,
            OrderId = order.Id,
            TestCode = "ABSC",
            Version = 1,
            Value = "Negative",
            Status = ResultStatus.PendingVerification,
            Source = ResultSource.Interface,
            SourceReference = "CTRL-HL7-ORU-0009",
            EnteredBy = "hl7",
            EnteredUtc = now.AddMinutes(-20)
        });
        context.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = visit.Patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Source = BloodTypeSource.HistoricalImport,
            IsCurrent = true
        });

        context.Hl7Messages.AddRange(
            new Hl7MessageLog
            {
                Direction = Hl7Direction.Inbound,
                MessageType = "ADT",
                TriggerEvent = "A04",
                MessageControlId = "CTRL-HL7-ADT-0009",
                RawMessage =
                    "MSH|^~\\&|ADT|HOSP|LIS|LAB|20260101120000||ADT^A04|CTRL-HL7-ADT-0009|P|2.5\r"
                    + "PID|1||MRN0009^^^HOSP^MR||INTERFACE^HELEN||19880417|F",
                Status = Hl7MessageStatus.Processed,
                ReceivedUtc = now.AddHours(-3),
                ProcessedUtc = now.AddHours(-3),
                AckCode = "AA",
                CreatedBy = "hl7"
            },
            new Hl7MessageLog
            {
                Direction = Hl7Direction.Inbound,
                MessageType = "ORM",
                TriggerEvent = "O01",
                MessageControlId = "CTRL-HL7-ORM-0009",
                RawMessage =
                    "MSH|^~\\&|EHR|HOSP|BBLIS|LAB|20260101130000||ORM^O01|CTRL-HL7-ORM-0009|P|2.5\r"
                    + "PID|1||MRN0009^^^HOSP^MR||INTERFACE^HELEN\r"
                    + "ORC|NW|PLACER-HL7-0009\r"
                    + "OBR|1|PLACER-HL7-0009||TNS^Type and Screen",
                Status = Hl7MessageStatus.Processed,
                ReceivedUtc = now.AddHours(-2),
                ProcessedUtc = now.AddHours(-2),
                AckCode = "AA",
                CreatedBy = "hl7"
            },
            new Hl7MessageLog
            {
                Direction = Hl7Direction.Inbound,
                MessageType = "ORU",
                TriggerEvent = "R01",
                MessageControlId = "CTRL-HL7-ORU-0009",
                RawMessage =
                    "MSH|^~\\&|ANALYZER|LAB|BBLIS|BB|20260101140000||ORU^R01|CTRL-HL7-ORU-0009|P|2.5\r"
                    + "PID|1||MRN0009^^^HOSP^MR||INTERFACE^HELEN\r"
                    + "ORC|RE|PLACER-HL7-0009\r"
                    + "OBR|1|PLACER-HL7-0009||ABSC^Antibody Screen\r"
                    + "OBX|1|ST|ABSC||Negative||||||F",
                Status = Hl7MessageStatus.Processed,
                ReceivedUtc = now.AddMinutes(-20),
                ProcessedUtc = now.AddMinutes(-20),
                AckCode = "AA",
                CreatedBy = "hl7"
            });

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Completes the Helen Interface load with an issued unit and a processed RAS so
    /// product history shows a transfusion without a second /issuing Document.
    /// </summary>
    private static async Task SeedHl7BpamDataLoadScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodUnits.AnyAsync(u => u.UnitNumber == "W000123BPAM001", ct))
        {
            return;
        }

        var patient = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "MRN0009", ct);
        if (patient is null)
        {
            return;
        }

        var encounter = await context.Encounters.FirstOrDefaultAsync(e => e.PatientId == patient.Id, ct);
        if (encounter is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var orderingLocation = await context.OrderingLocations.FirstAsync(l => l.Code == "ED", ct);
        var redCells = await context.ProductTypes.FirstAsync(p => p.ProductCode == "RBC-LR", ct);
        var location = await context.InventoryLocations.FirstAsync(ct);

        var unit = new BloodUnit
        {
            UnitNumber = "W000123BPAM001",
            ProductTypeId = redCells.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = now.AddDays(20),
            CurrentLocationId = location.Id,
            Status = UnitStatus.Transfused,
            Volume = 300m,
            CollectionFacility = "Regional Blood Center",
            Supplier = "Regional Blood Center",
            CollectedUtc = now.AddDays(-6)
        };
        context.BloodUnits.Add(unit);
        await context.SaveChangesAsync(ct);

        var productOrder = new Order
        {
            OrderNumber = "PLACER-HL7-BPAM-0009",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderingLocationId = orderingLocation.Id,
            OrderCategory = OrderCategory.Product,
            OrderName = "Red Blood Cells",
            OrderType = OrderType.Other,
            ProductTypeId = redCells.Id,
            Priority = OrderPriority.Routine,
            Status = OrderStatus.Completed,
            Source = OrderSource.Hl7,
            OrderedUtc = now.AddHours(-90),
            FulfillmentStatus = FulfillmentStatus.Complete
        };
        context.Orders.Add(productOrder);
        await context.SaveChangesAsync(ct);

        context.OrderLines.Add(new OrderLine
        {
            OrderId = productOrder.Id,
            LineNumber = 1,
            LineCategory = OrderCategory.Product,
            LineName = "Red Blood Cells",
            ProductTypeId = redCells.Id,
            OrderType = OrderType.Other,
            FulfillmentStatus = FulfillmentStatus.Complete
        });

        var issue = new Issue
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            OrderId = productOrder.Id,
            IssuedUtc = now.AddHours(-80),
            IssuedBy = "tech2",
            IssuedTo = "RN Chen",
            IssuedToLocation = "4W Oncology",
            CrossmatchStatus = CrossmatchClinicalStatus.Compatible,
            Status = IssueStatus.Transfused,
            UnitExpirationAtIssueUtc = unit.ExpiresUtc,
            WardReceivedUtc = now.AddHours(-75),
            WardReceivedBy = "HL7-BPAM",
            WardVisualAcceptable = true
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync(ct);

        context.TransfusionEvents.Add(new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            StartUtc = now.AddHours(-74),
            StopUtc = now.AddHours(-73),
            VolumeTransfused = 300m,
            Transfusionist = "Nurse, Pat",
            Location = "4W Oncology",
            ReactionSuspected = false,
            FinalDisposition = TransfusionDisposition.Completed,
            DocumentedBy = "hl7",
            PatientIdentificationMethod = "HL7-BPAM",
            UnitIdentificationMethod = "HL7-BPAM"
        });

        context.Hl7Messages.Add(new Hl7MessageLog
        {
            Direction = Hl7Direction.Inbound,
            MessageType = "RAS",
            TriggerEvent = "O17",
            MessageControlId = "CTRL-HL7-RAS-0009",
            RawMessage =
                "MSH|^~\\&|EPIC|HOSP|BBLIS|LAB|20260101150000||RAS^O17|CTRL-HL7-RAS-0009|P|2.5\r"
                + "PID|1||MRN0009^^^HOSP^MR||INTERFACE^HELEN\r"
                + "PV1||I|4W Oncology\r"
                + "RXA|0|1|20260101140000|20260101150000|CODE^RBC|300||||12345^Nurse^Pat|||||W000123BPAM001",
            Status = Hl7MessageStatus.Processed,
            ReceivedUtc = now.AddHours(-73),
            ProcessedUtc = now.AddHours(-73),
            AckCode = "AA",
            CreatedBy = "hl7"
        });

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Patricia Demo already has a verified type/screen, an issued RBC-LR unit, and a
    /// completed transfusion. This captures those charges and queues outbound DFT
    /// rows so /billing is not empty and staff do not POST a second capture.
    /// </summary>
    private static async Task SeedBillingCaptureScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Hl7Messages.AnyAsync(m => m.MessageControlId.StartsWith("CTRL-DFT-"), ct))
        {
            return;
        }

        var patient = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "MRN0001", ct);
        if (patient is null)
        {
            return;
        }

        var aboRh = await context.TestResults.FirstOrDefaultAsync(
            r => r.PatientId == patient.Id && r.TestCode == "ABORH" && r.Status == ResultStatus.Verified, ct);
        var absc = await context.TestResults.FirstOrDefaultAsync(
            r => r.PatientId == patient.Id && r.TestCode == "ABSC" && r.Status == ResultStatus.Verified, ct);
        var unit = await context.BloodUnits.FirstOrDefaultAsync(u => u.UnitNumber == "W0001230000099", ct);
        var issue = unit is null
            ? null
            : await context.Issues.FirstOrDefaultAsync(i => i.BloodProductId == unit.Id && i.PatientId == patient.Id, ct);

        var aboRhBill = await context.TestServiceBillings.FirstOrDefaultAsync(
            b => b.TestCode == "ABORH" && b.Trigger == BillingTriggerType.TestVerified, ct);
        var abscBill = await context.TestServiceBillings.FirstOrDefaultAsync(
            b => b.TestCode == "ABSC" && b.Trigger == BillingTriggerType.TestVerified, ct);
        var issueBill = await context.ProductBillings.FirstOrDefaultAsync(
            b => b.IsbtProductCode == "E0336" && b.Trigger == BillingTriggerType.UnitIssued, ct);
        var txBill = await context.ProductBillings.FirstOrDefaultAsync(
            b => b.IsbtProductCode == "E0336" && b.Trigger == BillingTriggerType.UnitTransfused, ct);

        if (aboRh is not null && aboRhBill is not null)
        {
            await SeedCapturedChargeAsync(
                context, BillingTriggerType.TestVerified, nameof(TestResult), aboRh.Id, patient.Id,
                aboRh.VerifiedUtc ?? aboRh.EnteredUtc ?? DateTime.UtcNow, "BB-ABORH", BillingChargeSourceKind.TestService,
                aboRhBill.Id, performingLocation: null, ct);
        }

        if (absc is not null && abscBill is not null)
        {
            await SeedCapturedChargeAsync(
                context, BillingTriggerType.TestVerified, nameof(TestResult), absc.Id, patient.Id,
                absc.VerifiedUtc ?? absc.EnteredUtc ?? DateTime.UtcNow, "BB-SCREEN", BillingChargeSourceKind.TestService,
                abscBill.Id, performingLocation: null, ct);
        }

        if (issue is not null && issueBill is not null)
        {
            await SeedCapturedChargeAsync(
                context, BillingTriggerType.UnitIssued, nameof(Issue), issue.Id, patient.Id,
                issue.IssuedUtc, "BB-RBC-ISSUE", BillingChargeSourceKind.Product,
                issueBill.Id, issue.IssuedToLocation, ct);
        }

        if (issue is not null && txBill is not null)
        {
            await SeedCapturedChargeAsync(
                context, BillingTriggerType.UnitTransfused, nameof(TransfusionEvent), issue.Id, patient.Id,
                DateTime.UtcNow, "BB-RBC-TX", BillingChargeSourceKind.Product,
                txBill.Id, issue.IssuedToLocation, ct);
        }

        await SeedHl7BpamBillingScenarioAsync(context, ct);
    }

    /// <summary>
    /// Helen Interface RAS transfusion gets the same issue/transfusion charges and DFT
    /// stubs as Patricia so /billing shows the interface load without a second capture.
    /// </summary>
    private static async Task SeedHl7BpamBillingScenarioAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Hl7Messages.AnyAsync(m => m.MessageControlId.StartsWith("CTRL-DFT-BPAM-"), ct))
        {
            return;
        }

        var patient = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == "MRN0009", ct);
        var unit = await context.BloodUnits.FirstOrDefaultAsync(u => u.UnitNumber == "W000123BPAM001", ct);
        if (patient is null || unit is null)
        {
            return;
        }

        var issue = await context.Issues.FirstOrDefaultAsync(
            i => i.BloodProductId == unit.Id && i.PatientId == patient.Id, ct);
        var issueBill = await context.ProductBillings.FirstOrDefaultAsync(
            b => b.IsbtProductCode == "E0336" && b.Trigger == BillingTriggerType.UnitIssued, ct);
        var txBill = await context.ProductBillings.FirstOrDefaultAsync(
            b => b.IsbtProductCode == "E0336" && b.Trigger == BillingTriggerType.UnitTransfused, ct);
        if (issue is null)
        {
            return;
        }

        if (issueBill is not null)
        {
            await SeedCapturedChargeAsync(
                context, BillingTriggerType.UnitIssued, nameof(Issue), issue.Id, patient.Id,
                issue.IssuedUtc, "BB-RBC-ISSUE", BillingChargeSourceKind.Product,
                issueBill.Id, issue.IssuedToLocation, ct,
                controlPrefix: "CTRL-DFT-BPAM",
                pidMrn: "MRN0009",
                pidName: "INTERFACE^HELEN");
        }

        if (txBill is not null)
        {
            await SeedCapturedChargeAsync(
                context, BillingTriggerType.UnitTransfused, nameof(TransfusionEvent), issue.Id, patient.Id,
                DateTime.UtcNow, "BB-RBC-TX", BillingChargeSourceKind.Product,
                txBill.Id, issue.IssuedToLocation, ct,
                controlPrefix: "CTRL-DFT-BPAM",
                pidMrn: "MRN0009",
                pidName: "INTERFACE^HELEN");
        }
    }

    private static async Task SeedCapturedChargeAsync(
        BloodBankDbContext context,
        BillingTriggerType triggerType,
        string triggerEntityType,
        long triggerEntityId,
        long patientId,
        DateTime serviceDateUtc,
        string billingCode,
        BillingChargeSourceKind sourceKind,
        long sourceId,
        string? performingLocation,
        CancellationToken ct,
        string controlPrefix = "CTRL-DFT",
        string pidMrn = "MRN0001",
        string pidName = "DEMO^PATRICIA")
    {
        var dedupeKey = $"{triggerType}|{triggerEntityType}|{triggerEntityId}|{sourceKind}|{sourceId}|{serviceDateUtc:yyyyMMdd}";
        if (await context.BillingEvents.AnyAsync(e => e.DedupeKey == dedupeKey, ct))
        {
            return;
        }

        var code = await context.ChargeCodes.FirstOrDefaultAsync(c => c.Code == billingCode, ct);
        if (code is null)
        {
            return;
        }

        var billingEvent = new BillingEvent
        {
            ChargeCodeId = code.Id,
            BillingCode = code.Code,
            TriggerType = triggerType,
            TriggerEntityType = triggerEntityType,
            TriggerEntityId = triggerEntityId,
            PatientId = patientId,
            ServiceDateUtc = serviceDateUtc,
            Amount = code.DefaultAmount,
            SourceKind = sourceKind,
            SourceId = sourceId,
            DedupeKey = dedupeKey,
            Status = BillingEventStatus.Pending,
            ProcedureCode = code.CptCode,
            RevenueCode = code.RevenueCode,
            Modifier = code.Modifier,
            Description = code.Description,
            PerformingLocationCode = string.IsNullOrWhiteSpace(performingLocation) ? null : performingLocation.Trim()
        };
        context.BillingEvents.Add(billingEvent);
        await context.SaveChangesAsync(ct);

        var controlId = $"{controlPrefix}-{billingCode}-{triggerEntityId}";
        if (!await context.Hl7Messages.AnyAsync(m => m.MessageControlId == controlId, ct))
        {
            context.Hl7Messages.Add(new Hl7MessageLog
            {
                Direction = Hl7Direction.Outbound,
                MessageType = "DFT",
                TriggerEvent = "P03",
                MessageControlId = controlId,
                RawMessage =
                    "MSH|^~\\&|BBLIS|LAB|BILL|HOSP|20260101150000||DFT^P03|" + controlId + "|P|2.5\r"
                    + "EVN|P03|20260101150000\r"
                    + "PID|1||" + pidMrn + "^^^HOSP^MR||" + pidName + "\r"
                    + "FT1|1|||" + serviceDateUtc.ToString("yyyyMMdd") + "||CG|" + billingCode,
                Status = Hl7MessageStatus.Received,
                ReceivedUtc = serviceDateUtc,
                CreatedBy = "billing"
            });
            await context.SaveChangesAsync(ct);
        }

        var log = await context.Hl7Messages.SingleAsync(m => m.MessageControlId == controlId, ct);
        billingEvent.Hl7MessageId = log.Id;
        await context.SaveChangesAsync(ct);
    }

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
