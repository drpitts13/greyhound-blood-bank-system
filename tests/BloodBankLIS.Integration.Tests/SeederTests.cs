using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.HL7.Parsing;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class SeederTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SeederTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task Seed_PopulatesReferenceAndDemoData_AndIsIdempotent()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context); // second run should not duplicate
        }

        await using (var verify = _factory.Create())
        {
            // Four base products, three modification targets, and eleven extra test products.
            Assert.Equal(18, await verify.ProductTypes.CountAsync());
            Assert.True(await verify.ProductTypes.AnyAsync(p => p.ProductCode == "WB" && p.RequiresCrossmatch));
            Assert.True(await verify.ProductTypes.AnyAsync(p => p.ProductCode == "RBC-LR" && p.RequiresRetype));
            Assert.True(await verify.ProductTypes.AnyAsync(p => p.ProductCode == "FFP" && !p.RequiresRetype));
            Assert.True(await verify.ProductTypes.AnyAsync(p => p.ProductCode == "CRYO" && p.Isbt128ProductCode == "E5165"));
            Assert.True(await verify.ProductTypes.AnyAsync(p => p.ProductCode == "GRAN" && p.ComponentClass == ComponentClass.Granulocytes));
            Assert.True(await verify.TestDefinitions.AnyAsync(t => t.Code == AboRhRetypeRule.TestCode && t.Category == TestCategory.AboRhRetype));
            Assert.True(await verify.TestDefinitions.AnyAsync(t => t.Code == ProductRetypeAssignment.RhPositiveTestCode && t.Category == TestCategory.AboRhRetype));
            Assert.True(await verify.TestDefinitions.AnyAsync(t => t.Code == ProductRetypeAssignment.RhNegativeTestCode && t.Category == TestCategory.AboRhRetype));
            var rbcLr = await verify.ProductTypes.SingleAsync(p => p.ProductCode == "RBC-LR");
            Assert.True(rbcLr.RequiresRetype);
            Assert.NotNull(rbcLr.RhPositiveRetypeTestId);
            Assert.NotNull(rbcLr.RhNegativeRetypeTestId);
            Assert.Equal(2, await verify.ProductRetypeResults.CountAsync(r => r.Status == ResultStatus.Pending));
            Assert.True(await verify.ProductRetypeResults.AnyAsync(r => r.TestCode == ProductRetypeAssignment.RhPositiveTestCode));
            Assert.True(await verify.ProductRetypeResults.AnyAsync(r => r.TestCode == ProductRetypeAssignment.RhNegativeTestCode));
            Assert.Equal(5, await verify.InventoryLocations.CountAsync());
            Assert.True(await verify.AntibodyPanelLots.AnyAsync(l => l.LotNumber == "GHP-ABID-2026A" && l.IsActive));
            Assert.True(await verify.AntibodyPanelCells.CountAsync() >= 10);

            // The original demo patient, five clinical scenarios, plus alloimmunization,
            // autologous/directed FDA/AABB validation, and HL7 data-load patients.
            Assert.Equal(9, await verify.Patients.CountAsync());
            Assert.Equal(10, await verify.Encounters.CountAsync());
            Assert.Equal(14, await verify.Orders.CountAsync());
            Assert.True(await verify.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0009"));
            Assert.True(await verify.TestResults.AnyAsync(r =>
                r.SourceReference == "CTRL-HL7-ORU-0009"
                && r.Source == ResultSource.Interface
                && r.Status == ResultStatus.PendingVerification));
            Assert.Equal(4, await verify.Hl7Messages.CountAsync(m => m.MessageControlId.StartsWith("CTRL-HL7-")));
            Assert.True(await verify.BloodUnits.AnyAsync(u =>
                u.UnitNumber == "W000123BPAM001" && u.Status == UnitStatus.Transfused));
            Assert.True(await verify.TransfusionEvents.AnyAsync(t =>
                t.PatientIdentificationMethod == "HL7-BPAM"
                && t.UnitIdentificationMethod == "HL7-BPAM"));
            Assert.Equal(6, await verify.BillingEvents.CountAsync());
            Assert.True(await verify.BillingEvents.AnyAsync(e => e.BillingCode == "BB-SCREEN" && e.Status == BillingEventStatus.Pending));
            Assert.True(await verify.BillingEvents.AnyAsync(e => e.BillingCode == "BB-RBC-ISSUE"));
            Assert.True(await verify.BillingEvents.AnyAsync(e => e.BillingCode == "BB-RBC-TX"));
            Assert.Equal(6, await verify.Hl7Messages.CountAsync(m => m.MessageControlId.StartsWith("CTRL-DFT-")));
            Assert.Equal(2, await verify.Hl7Messages.CountAsync(m => m.MessageControlId.StartsWith("CTRL-DFT-BPAM-")));

            // Three original units, 28 stocked across every ABO/Rh, two modification
            // results, one received by ISBT 128 scan, two waiting for ABO/Rh retype,
            // one on operational hold, six ISBT divide-scenario units
            // (three available + one completed source + two V0A/V0B results),
            // autologous + directed, lookback sibling, missing + damaged,
            // and two expected inbound packing-list units, plus the Helen BPAM unit.
            Assert.Equal(51, await verify.BloodUnits.CountAsync());
            Assert.Equal(2, await verify.BloodUnits.CountAsync(u => u.Status == UnitStatus.Expected));
            Assert.True(await verify.BloodUnits.AnyAsync(u => u.UnitNumber == "W000123ASN0001" && u.ShipmentId == "ASN-DEMO-01"));
            Assert.True(await verify.BloodUnits.AnyAsync(u => u.Status == UnitStatus.OnHold && u.HoldReason != null));
            Assert.Equal(2, await verify.BloodUnits.CountAsync(u => u.Status == UnitStatus.Received));

            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e => e.RuleCode == AboCompatibilityRule.AboCode && !e.IsOverridable));
            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e =>
                e.RuleCode == BloodAttributeCompatibilityRule.AntigenNegCode
                && e.IsOverridable
                && e.MinSecurityLevel == 2));
            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e => e.RuleCode == CrossmatchValidityRule.Code && !e.IsOverridable));
            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e => e.RuleCode == AntibodyHistoryCrossmatchRule.RuleCode && e.IsOverridable));

            // Seeding clinical/reference rows also produced audit events.
            Assert.True(await verify.AuditEvents.AnyAsync());

            Assert.True(await verify.IsbtProductCodes.CountAsync() >= 40);
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p =>
                p.ProductDescriptionCode == "E0336"
                && p.Description.Contains("AS1")
                && p.StandardVersion == UsSupplierProductCodeSeed.StandardVersion));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0206"));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E0701"));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E5165"));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E5166"));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E3858"));
            Assert.True(await verify.IsbtProductCodes.AnyAsync(p => p.ProductDescriptionCode == "E4253"));

            await AssertExtendedScenariosAsync(verify);
        }
    }

    /// <summary>
    /// Each extended demo scenario must land, otherwise a scenario silently returned early
    /// because a unit or reference row it depends on was missing.
    /// </summary>
    private static async Task AssertExtendedScenariosAsync(BloodBankDbContext verify)
    {
        Assert.True(await verify.PhaseDefinitions.AnyAsync(p => p.Code == "IS" && p.IsActive));
        Assert.True(await verify.PhaseDefinitions.AnyAsync(p => p.Code == "CC" && p.IsCheckCell && !p.IncludeInInterpretation));
        var absc = await verify.TestDefinitions.SingleAsync(t => t.Code == "ABSC" && t.IsActive);
        Assert.Equal(ResultValueType.Subtest, absc.ResultValueType);
        Assert.Contains("Cell1", absc.PanelSubtestsJson);
        Assert.Contains("AHG", absc.PanelSubtestsJson);

        // The neonatal rule adds TSNEO, so the catalog has to define it.
        Assert.True(await verify.TestDefinitions.AnyAsync(t => t.Code == "TSNEO" && t.IsActive));

        var neonate = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0002");
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), neonate.DateOfBirth);

        // Rh negative type drives the Weak D rule, which matches on the canonical
        // interpretation derived from the stored value.
        var aboRh = await verify.TestResults.SingleAsync(r =>
            r.TestCode == "ABORH" && r.Value == AboRhResultValue.Format(AboGroup.A, RhType.Negative));
        Assert.Equal("A Negative", ResultInterpretation.Resolve(aboRh.Interpretation, aboRh.Value));

        Assert.True(await verify.AntibodyHistory.AnyAsync(a => a.AntibodySpecificity == "anti-K" && a.IsActive));
        Assert.True(await verify.AntigenProfiles.AnyAsync(a => a.Result == AntigenResult.Negative));
        Assert.True(await verify.UnitBloodAttributes.AnyAsync(a => a.Result == AntigenResult.Negative));

        var emergency = await verify.Issues.SingleAsync(i => i.IssueType == IssueType.EmergencyRelease);
        Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency, emergency.CrossmatchStatus);
        Assert.NotNull(emergency.OverrideId);
        Assert.True(await verify.Overrides.AnyAsync(o =>
            o.Action == OverrideAction.EmergencyRelease && o.ContextId == emergency.Id));

        var reaction = await verify.TransfusionEvents.SingleAsync(t => t.ReactionSuspected);
        Assert.Equal(TransfusionDisposition.Stopped, reaction.FinalDisposition);
        Assert.True(await verify.Orders.AnyAsync(o => o.OrderType == OrderType.TransfusionReactionWorkup));
        Assert.True(await verify.ReactionInvestigations.AnyAsync(i =>
            i.TransfusionEventId == reaction.Id
            && i.ClericalCheckCompleted
            && i.DatResult == DatWorkupResult.Negative));

        // Modifications consume a source unit and produce a derived one.
        Assert.Equal(3, await verify.UnitModifications.CountAsync());
        Assert.Equal(7, await verify.UnitModificationUnits.CountAsync());
        Assert.True(await verify.UnitModifications.AnyAsync(m => m.ModificationType == ModificationType.Wash));
        Assert.True(await verify.UnitModifications.AnyAsync(m => m.ModificationType == ModificationType.Divide));
        Assert.Equal(4, await verify.BloodUnits.CountAsync(u => u.DerivedFromModificationId != null));
        Assert.Equal(3, await verify.BloodUnits.CountAsync(u => u.Status == UnitStatus.Modified));

        Assert.True(await verify.ModificationRules.AnyAsync(r => r.ModificationCode == "DIV-RBC-LR" && r.IsActive));
        Assert.True(await verify.ModificationRules.AnyAsync(r => r.ModificationCode == "DIV-WB-RBC" && r.IsActive));
        Assert.True(await verify.BloodUnits.AnyAsync(u =>
            u.Din == "W123425000001" && u.ProductCodeData == "E0336V00" && u.Volume == 300m && u.Status == UnitStatus.Available));
        Assert.True(await verify.BloodUnits.AnyAsync(u =>
            u.Din == "W123425000002" && u.ProductCodeData == "E0336V0A" && u.Volume == 150m && u.Status == UnitStatus.Available));
        Assert.True(await verify.BloodUnits.AnyAsync(u =>
            u.Din == "W123425000003" && u.ProductCodeData == "E0023V00" && u.Volume == 450m && u.Status == UnitStatus.Available));
        Assert.True(await verify.BloodUnits.AnyAsync(u =>
            u.Din == "W123425000010" && u.ProductCodeData == "E0336V0A" && u.Volume == 150m && u.DerivedFromModificationId != null));
        Assert.True(await verify.BloodUnits.AnyAsync(u =>
            u.Din == "W123425000010" && u.ProductCodeData == "E0336V0B" && u.Volume == 150m && u.DerivedFromModificationId != null));

        var scanned = await verify.BloodUnits.SingleAsync(u => u.Source == ComponentEntrySource.Scanner);
        Assert.Equal(4, await verify.BloodComponentRawScans.CountAsync(s => s.BloodProductId == scanned.Id));
        Assert.True(await verify.BloodComponentScanSessions.AnyAsync(s => s.IsCompleted));

        var pregnant = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0007");
        Assert.NotNull(pregnant.RecentPregnancyUtc);
        var alloSpecimen = await verify.Specimens.SingleAsync(s => s.AccessionNumber == "ACC0017");
        Assert.Equal(
            SpecimenValidityPolicy.ComputeExpiresUtc(alloSpecimen.CollectedUtc, alloimmunizationRisk: true),
            alloSpecimen.ExpiresUtc);
        Assert.NotEqual(
            SpecimenValidityPolicy.ComputeExpiresUtc(alloSpecimen.CollectedUtc, alloimmunizationRisk: false),
            alloSpecimen.ExpiresUtc);

        var reserved = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0008");
        var autologous = await verify.BloodUnits.SingleAsync(u => u.UnitNumber == "W000123AUTO001");
        Assert.Equal(DonationRestriction.Autologous, autologous.DonationRestriction);
        Assert.Equal(reserved.Id, autologous.ReservedPatientId);
        var directed = await verify.BloodUnits.SingleAsync(u => u.UnitNumber == "W000123DIR0001");
        Assert.Equal(DonationRestriction.Directed, directed.DonationRestriction);
        Assert.Equal(reserved.Id, directed.ReservedPatientId);

        const string lookbackDin = "W123426000001";
        Assert.True(await verify.BloodUnits.CountAsync(u => u.Din == lookbackDin) >= 2);
        Assert.True(await verify.LookbackNotifications.AnyAsync(n =>
            n.Din == lookbackDin && n.Status == LookbackNotificationStatus.Pending));

        Assert.True(await verify.BloodUnits.AnyAsync(u =>
            u.UnitNumber == "W000123MISS001" && u.Status == UnitStatus.Missing && u.MissingReason != null));
        Assert.True(await verify.BloodUnits.AnyAsync(u =>
            u.UnitNumber == "W000123DMG0001" && u.Status == UnitStatus.Damaged && u.DamagedReason != null));

        var patricia = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0001");
        Assert.True(await verify.TestResults.AnyAsync(r =>
            r.PatientId == patricia.Id && r.TestCode == "ABSC" && r.Value == "Negative" && r.Status == ResultStatus.Verified));
        Assert.Equal(2, await verify.PatientBloodTypeHistory.CountAsync(h =>
            h.PatientId == patricia.Id && h.Abo == AboGroup.O && h.RhD == RhType.Positive));

        var helen = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0009");
        Assert.True(await verify.TransfusionEvents.AnyAsync(t =>
            t.PatientId == helen.Id
            && t.PatientIdentificationMethod == "HL7-BPAM"
            && t.FinalDisposition == TransfusionDisposition.Completed));
        Assert.True(await verify.Hl7Messages.AnyAsync(m =>
            m.MessageControlId == "CTRL-HL7-RAS-0009" && m.AckCode == "AA"));
        Assert.True(await verify.BillingEvents.AnyAsync(e =>
            e.PatientId == helen.Id
            && e.TriggerType == BillingTriggerType.UnitTransfused
            && e.Status == BillingEventStatus.Pending));
    }

    [Fact]
    public async Task Seed_HelenHl7Messages_SurfacePlacerTestAndUnit()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var oru = await verify.Hl7Messages.SingleAsync(m => m.MessageControlId == "CTRL-HL7-ORU-0009");
        Assert.True(Hl7MessageIdentity.TryRead(oru.RawMessage, out var oruId));
        Assert.Equal("MRN0009", oruId.MedicalRecordNumber);
        Assert.Equal("PLACER-HL7-0009", oruId.PlacerOrderNumber);
        Assert.Equal("ABSC", oruId.TestCode);

        var ras = await verify.Hl7Messages.SingleAsync(m => m.MessageControlId == "CTRL-HL7-RAS-0009");
        Assert.True(Hl7MessageIdentity.TryRead(ras.RawMessage, out var rasId));
        Assert.Equal("W000123BPAM001", rasId.UnitNumber);
    }

    [Fact]
    public async Task Seed_HelenHl7Order_SurfacesPostedInterfaceValueAndVerifyAction()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var helen = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0009");
        var helenOrders = await new OrderService(
            new EfRepository<Order>(verify),
            new EfRepository<OrderLine>(verify),
            new EfRepository<OrderSpecimen>(verify),
            new EfRepository<Encounter>(verify),
            new EfRepository<OrderingLocation>(verify),
            new EfRepository<Patient>(verify),
            new EfRepository<Specimen>(verify),
            new EfRepository<OrderingProvider>(verify),
            new EfRepository<ProductType>(verify),
            new EfRepository<TestDefinition>(verify),
            new EfRepository<TestGrouper>(verify),
            _factory.Clock,
            verify,
            results: new EfRepository<TestResult>(verify)).ListByPatientAsync(helen.Id);
        var hl7Order = Assert.Single(helenOrders, o => o.OrderNumber == "PLACER-HL7-0009");
        Assert.Equal(OrderSource.Hl7, hl7Order.Source);
        Assert.Equal("Negative", hl7Order.CurrentResultValue);
        Assert.Equal(ResultSource.Interface, hl7Order.CurrentResultSource);
        Assert.True(hl7Order.HasPostedInterfaceOrInstrumentValue);
        Assert.Equal("Verify", hl7Order.BenchActionLabel);
    }

    [Fact]
    public async Task Seed_HelenHl7Bpam_SurfacesVolumeLocationAndTransfusionist()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var helen = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0009");
        var history = await new PatientProductHistoryService(
            new EfRepository<Allocation>(verify),
            new EfRepository<Crossmatch>(verify),
            new EfRepository<Issue>(verify),
            new EfRepository<Return>(verify),
            new EfRepository<TransfusionEvent>(verify),
            new EfRepository<BloodUnit>(verify),
            new EfRepository<ProductType>(verify),
            new EfRepository<Encounter>(verify),
            new EfRepository<Order>(verify),
            new EfRepository<Specimen>(verify),
            new EfRepository<PatientBloodTypeHistory>(verify)).ListByPatientAsync(helen.Id);
        var row = Assert.Single(history, h =>
            h.EventType == PatientProductHistoryEventType.Transfused
            && h.UnitNumber == "W000123BPAM001");
        Assert.Equal("HL7-BPAM", row.PatientIdentificationMethod);
        Assert.Equal(300m, row.VolumeTransfused);
        Assert.Equal("4W Oncology", row.IssuedToLocation);
        Assert.Equal("Nurse, Pat", row.Transfusionist);
    }

    [Fact]
    public async Task Seed_PendingRetypeWorklist_IncludesRetDemoUnits()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var pending = await new ProductRetypeService(
            new InventoryRepository(verify),
            new EfRepository<ProductRetypeResult>(verify),
            new EfRepository<TestDefinition>(verify),
            verify,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(verify, _factory.Clock, _factory.CurrentUser)).ListPendingAsync();
        var pos = Assert.Single(pending, u => u.UnitNumber == "W000123RET0001");
        var neg = Assert.Single(pending, u => u.UnitNumber == "W000123RET0002");
        Assert.Equal("RBC-LR", pos.ProductCode);
        Assert.Equal(AboGroup.O, pos.Abo);
        Assert.Equal(RhType.Positive, pos.RhD);
        Assert.Equal(ProductRetypeAssignment.RhPositiveTestCode, pos.TestCode);
        Assert.Equal(UnitStatus.Received, pos.Status);
        Assert.Equal("RBC-LR", neg.ProductCode);
        Assert.Equal(AboGroup.A, neg.Abo);
        Assert.Equal(RhType.Negative, neg.RhD);
        Assert.Equal(ProductRetypeAssignment.RhNegativeTestCode, neg.TestCode);
        Assert.Equal(UnitStatus.Received, neg.Status);
    }

    [Fact]
    public async Task Seed_ChargeRulesDoNotOverlapBillingCatalogs()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var testKeys = await verify.TestServiceBillings
            .Where(r => r.IsActive)
            .Select(r => new { r.Trigger, r.TestCode })
            .ToListAsync();
        var productRows = await verify.ProductBillings
            .Where(r => r.IsActive)
            .Select(r => new { r.Trigger, r.IsbtProductCode })
            .ToListAsync();
        var productCodesByIsbt = await verify.ProductTypes
            .Where(p => p.Isbt128ProductCode != null)
            .Select(p => new { p.Isbt128ProductCode, p.ProductCode })
            .ToListAsync();
        var activeRules = await verify.ChargeRules.Where(r => r.IsActive).ToListAsync();

        Assert.DoesNotContain(activeRules, rule =>
            !string.IsNullOrWhiteSpace(rule.TriggerKey)
            && testKeys.Any(t =>
                t.Trigger == rule.TriggerType
                && string.Equals(t.TestCode, rule.TriggerKey, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(activeRules, rule =>
            !string.IsNullOrWhiteSpace(rule.TriggerKey)
            && productRows.Any(p =>
                p.Trigger == rule.TriggerType
                && productCodesByIsbt.Any(map =>
                    string.Equals(map.Isbt128ProductCode, p.IsbtProductCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(map.ProductCode, rule.TriggerKey, StringComparison.OrdinalIgnoreCase))));
        Assert.Contains(activeRules, r =>
            r.TriggerType == BillingTriggerType.UnitIssued && r.TriggerKey == null);
    }

    [Fact]
    public async Task Seed_UpsertsMissingProductCodes_WhenPlaceholderAlreadyPresent()
    {
        await using (var context = _factory.Create())
        {
            // Stale placeholder with a different StandardVersion than the US subset seed.
            if (!await context.IsbtProductCodes.AnyAsync(p =>
                    p.ProductDescriptionCode == "E0206"
                    && p.StandardVersion == "PLACEHOLDER-REQUIRES-ICCBBA"))
            {
                context.IsbtProductCodes.Add(new IsbtProductCode
                {
                    ProductDescriptionCode = "E0206",
                    Description = "PLACEHOLDER — Red Blood Cells",
                    ComponentClass = "RedBloodCells",
                    AttributesJson = "[]",
                    StandardVersion = "PLACEHOLDER-REQUIRES-ICCBBA",
                    IsPlaceholder = true
                });
                await context.SaveChangesAsync();
            }
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var verify = _factory.Create())
        {
            Assert.True(await verify.IsbtProductCodes.CountAsync() >= 40);
            Assert.Equal(1, await verify.IsbtProductCodes.CountAsync(p => p.ProductDescriptionCode == "E0206"));
            var e0206 = await verify.IsbtProductCodes.SingleAsync(p => p.ProductDescriptionCode == "E0206");
            Assert.Equal("RED BLOOD CELLS|CPDA-1/450mL/refg|Irradiated", e0206.Description);
            Assert.Equal(UsSupplierProductCodeSeed.StandardVersion, e0206.StandardVersion);
        }
    }

    [Fact]
    public async Task Seed_DoesNotRevertLicensedProductCode()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<BloodBankDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new BloodBankDbContext(options, _factory.Clock, _factory.CurrentUser);
        await context.Database.EnsureCreatedAsync();
        await TestCatalogSeeder.EnsureSpecimenTypesAsync(context, _factory.Clock.UtcNow);

        context.IsbtProductCodes.Add(new IsbtProductCode
        {
            ProductDescriptionCode = "E0206",
            Description = "Facility-licensed extract row",
            ComponentClass = "RedBloodCells",
            AttributesJson = "[]",
            StandardVersion = "ICCBBA-ST-TEST",
            IsPlaceholder = false
        });
        await context.SaveChangesAsync();

        await DatabaseSeeder.SeedAsync(context);

        var licensed = await context.IsbtProductCodes.SingleAsync(p => p.ProductDescriptionCode == "E0206");
        Assert.False(licensed.IsPlaceholder);
        Assert.Equal("ICCBBA-ST-TEST", licensed.StandardVersion);
        Assert.Equal("Facility-licensed extract row", licensed.Description);
    }

    [Fact]
    public async Task Seed_AddsRequiredReferenceCodes_WhenTableHasPartialMigrationData()
    {
        await using (var context = _factory.Create())
        {
            context.OrderingLocations.Add(new OrderingLocation
            {
                Code = "CUSTOM",
                Name = "Custom location from SQLite migration",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var verify = _factory.Create())
        {
            Assert.True(await verify.OrderingLocations.AnyAsync(l => l.Code == "CUSTOM"));
            Assert.True(await verify.OrderingLocations.AnyAsync(l => l.Code == "OR"));
            Assert.True(await verify.OrderingLocations.AnyAsync(l => l.Code == "ED"));
            Assert.Equal(9, await verify.Patients.CountAsync());
        }
    }
}
