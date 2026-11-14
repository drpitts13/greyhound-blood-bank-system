using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Issuing;
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
            // autologous/directed FDA/AABB validation, HL7 data-load patients, and the
            // unit antigen typing patient, the massive transfusion demo patient, the multi-unit run patient,
            // the special-requirements patient, and the two RhIG demo mothers (Cycle 530).
            Assert.Equal(18, await verify.Patients.CountAsync()); // 16 named demo patients, the unidentified trauma patient, and the demo newborn
            Assert.Equal(16, await verify.Encounters.CountAsync());

            // TEST-BB-439: special requirements demo: two enforced and one informational, in effect, typed twice O+.
            var special = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0023");
            var specialRows = await verify.SpecialTransfusionRequirements.Where(r => r.PatientId == special.Id).ToListAsync();
            var definitionCodes = await verify.SpecialRequirementDefinitions
                .Where(d => specialRows.Select(r => r.RequirementDefinitionId).Contains(d.Id))
                .Select(d => d.Code)
                .ToListAsync();
            Assert.Equal(new[] { "CALL-TSMD", "CMVNEG-RBC", "IRRAD-RBC" }, definitionCodes.Order().ToArray());
            Assert.All(specialRows, r => Assert.True(
                SpecialRequirementCatalog.IsClinicallyActive(r.IsActive, r.EffectiveUtc, r.ExpiresUtc, DateTime.UtcNow)));
            Assert.Equal(2, await verify.PatientBloodTypeHistory.CountAsync(h => h.PatientId == special.Id && h.Abo == AboGroup.O));

            // Multi-unit demo: two reserved, two in one cooler in transit, two received at the bedside.
            var multi = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0022");
            Assert.Equal(2, await verify.Allocations.CountAsync(a => a.PatientId == multi.Id && a.Status == AllocationStatus.Reserved));
            Assert.Equal(2, await verify.Issues.CountAsync(i => i.PatientId == multi.Id && i.CoolerId == "Cooler M2" && i.WardReceivedUtc == null));
            Assert.Equal(2, await verify.Issues.CountAsync(i => i.PatientId == multi.Id && i.WardReceivedUtc != null && i.Status == IssueStatus.Issued));
            // Its red-cell order uses the type-and-screen tube, so more units can be reserved in the demo.
            var redCellOrder = await verify.Orders.SingleAsync(o => o.OrderNumber == "ORD0023");
            Assert.True(await verify.OrderSpecimens.AnyAsync(os => os.OrderId == redCellOrder.Id));

            // Demo MTP: one active protocol, four units issued under it, all in one recorded pack.
            var mtp = await verify.MtpActivations.SingleAsync();
            Assert.Equal(MtpActivationStatus.Active, mtp.Status);
            Assert.Equal(4, await verify.Issues.CountAsync(i => i.MtpActivationId == mtp.Id && i.IssueType == IssueType.MassiveTransfusion));
            var pack = await verify.MtpPackRecords.SingleAsync();
            Assert.Equal(4, await verify.Issues.CountAsync(i => i.MtpPackRecordId == pack.Id));
            Assert.Null((await verify.TransportContainers.SingleAsync()).MinTemperatureC);
            Assert.Equal(23, await verify.Orders.CountAsync()); // 20, the blood orders to prepare demo, the surgery demo, and ORD-RXN-NEXT (Cycle 631)
            // TEST-BB-465: a STAT type and screen with no tube, for the bench's No specimen view.
            var stat = await verify.Orders.SingleAsync(o => o.OrderNumber == "ORD0024");
            Assert.Equal(OrderPriority.Stat, stat.Priority);
            Assert.False(await verify.OrderSpecimens.AnyAsync(os => os.OrderId == stat.Id));
            Assert.Equal(2, await verify.OrderLines.CountAsync(l => l.OrderId == stat.Id));

            // TEST-BB-447: the reaction demo's specimen workup: ACC0014 before the issue with ORD0016 (DAT and ABO/Rh),
            // and the post-transfusion tube ACC0024 with the TRX-POST group ordered, unresulted.
            var reaction = await verify.ReactionInvestigations.SingleAsync(r => r.PreTransfusionOrderId != null);
            Assert.Equal("ACC0014", (await verify.Specimens.SingleAsync(sp => sp.Id == reaction.PreTransfusionSpecimenId)).AccessionNumber);
            Assert.Equal("ORD0016", (await verify.Orders.SingleAsync(o => o.Id == reaction.PreTransfusionOrderId)).OrderNumber);
            Assert.Equal(new[] { "ABORH", "DAT" }, (await verify.OrderLines.Where(l => l.OrderId == reaction.PreTransfusionOrderId).Select(l => l.TestCode).ToListAsync()).Order().ToArray());
            Assert.Equal("ACC0024", (await verify.Specimens.SingleAsync(sp => sp.Id == reaction.PostTransfusionSpecimenId)).AccessionNumber);
            var postOrder = await verify.Orders.SingleAsync(o => o.Id == reaction.PostTransfusionOrderId);
            Assert.Equal((OrderType.TransfusionReactionWorkup, $"RXN-{reaction.Id}-POST"), (postOrder.OrderType, postOrder.OrderNumber));
            Assert.Equal(3, await verify.OrderLines.CountAsync(l => l.OrderId == postOrder.Id));
            Assert.False(await verify.TestResults.AnyAsync(r => r.OrderId == postOrder.Id));
            Assert.Equal(("TRX-PRE", "TRX-POST"), await verify.ReactionWorkupSettings.Select(x => ValueTuple.Create(x.PreTransfusionTestGroupCode, x.PostTransfusionTestGroupCode)).SingleAsync());
            Assert.Equal("false", (await verify.SystemSettings.SingleAsync(s => s.Key == FacilityPolicyKeys.AllowElectronicCrossmatch)).Value);
            Assert.True(await verify.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0009"));
            Assert.True(await verify.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN0010"));
            Assert.Equal(2, await verify.BloodUnits.CountAsync(u =>
                (u.UnitNumber == "W000123UAG0001" || u.UnitNumber == "W000123UAG0002")
                && u.Status == UnitStatus.Assigned));
            var antigenUnits = await verify.BloodUnits
                .Where(u => u.UnitNumber == "W000123UAG0001" || u.UnitNumber == "W000123UAG0002")
                .Select(u => u.Id)
                .ToListAsync();
            Assert.Equal(2, await verify.ProductRetypeResults.CountAsync(r =>
                antigenUnits.Contains(r.BloodProductId)
                && r.Status == ResultStatus.Verified
                && r.MatchesLabel));
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
            // and two expected inbound packing-list units, plus the Helen BPAM unit,
            // plus two units assigned for unit antigen typing, plus four issued under the demo MTP.
            Assert.Equal(74, await verify.BloodUnits.CountAsync()); // 68, the two units in the ship-out demo, the reaction next-unit demo (Cycle 631), the two returned to the supplier (Cycle 686), and the recalled unit to pull (Cycle 690)
            // TEST-BB-469: a demo supplier order for five group O red cells, received against a delivery of three.
            var demoOrder = await verify.SupplierOrders.SingleAsync(o => o.OrderNumber == "SO-DEMO-001");
            Assert.Equal(SupplierOrderStatus.Received, demoOrder.Status);
            Assert.Equal(3, await verify.BloodUnits.CountAsync(u => u.DeliveryId == demoOrder.BloodDeliveryId));
            Assert.Equal(5, (await verify.SupplierOrderLines.SingleAsync(l => l.SupplierOrderId == demoOrder.Id)).Quantity);
            // TEST-BB-509: a submitted demo order for one group A red cell, not yet received, shows as on order.
            var onOrder = await verify.SupplierOrders.SingleAsync(o => o.OrderNumber == "SO-DEMO-002");
            Assert.Equal(SupplierOrderStatus.Submitted, onOrder.Status);
            Assert.Equal((AboGroup?)AboGroup.A, (await verify.SupplierOrderLines.SingleAsync(l => l.SupplierOrderId == onOrder.Id)).Abo);
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
            Assert.True(await verify.ExceptionDefinitions.AnyAsync(e =>
                e.RuleCode == SpecimenExpirationOverrideRule.Code
                && e.IsOverridable
                && e.MinSecurityLevel == 2
                && e.Name == "Specimen expiration override"));

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
            o.Action == OverrideAction.EmergencyRelease && o.ContextId == emergency.BloodProductId));

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

    // TEST-BB-462: the demo shows each cooler check state: Cooler M2 checked with all units packed, Cooler 1 checked
    // with one unit missing, CLR-TRAUMA-1 not checked. Seeding again adds no more check rows.
    // TEST-BB-1025: seeded products carry the attributes their names state, so an irradiated requirement can be met by
    // "Red Blood Cells, Irradiated"; seeding again adds none twice (end-user testing pass 16).
    [Fact]
    public async Task Seed_ProductsCarryTheAttributesTheirNamesState()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var codes = await (from a in verify.ProductAttributeAssignments
                           join pa in verify.ProductAttributes on a.ProductAttributeId equals pa.Id
                           join p in verify.ProductTypes on a.ProductTypeId equals p.Id
                           select new { p.ProductCode, pa.Code }).ToListAsync();
        Assert.Contains(codes, c => c.ProductCode == "RBC-IRR" && c.Code == "IRRAD");
        Assert.Contains(codes, c => c.ProductCode == "RBC-LR" && c.Code == "LR");
        Assert.DoesNotContain(codes, c => c.ProductCode == "FFP");
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public async Task Seed_DemoCoolers_ShowEachCheckState_Once()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var coolers = new CoolerCheckService(
            new EfRepository<Issue>(verify), new InventoryRepository(verify), new EfRepository<BloodUnit>(verify),
            new EfRepository<ProductType>(verify), new EfRepository<Patient>(verify),
            new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate), _factory.CurrentUser,
            new AuditWriter(verify, _factory.Clock, _factory.CurrentUser), verify, new AuditQuery(verify));
        var open = (await coolers.SummarizeOpenAsync()).ToDictionary(c => c.CoolerId);
        Assert.True(open["Cooler M2"].LastCheckReady);
        Assert.False(open["Cooler 1"].LastCheckReady);
        Assert.NotNull(open["Cooler 1"].LastCheckedUtc);
        Assert.Null(open["CLR-TRAUMA-1"].LastCheckedUtc);

        var checks = await verify.AuditEvents.CountAsync(e => e.EntityType == nameof(Issue) && e.Reason!.StartsWith("Cooler check: "));
        Assert.Equal(open["Cooler M2"].Units + open["Cooler 1"].Units, checks);
        Assert.Contains("1 missing", (await verify.AuditEvents.FirstAsync(e => e.Reason!.StartsWith("Cooler check: Cooler 1"))).Reason);
    }

    // TEST-BB-377: the demo patient's specimens are seeded with the expiration policy gives them, so editing one needs no override.
    [Fact]
    public async Task Seed_DemoPatientSpecimens_ExpireAsPolicySays()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var patient = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0001");
        var service = new BloodBankLIS.Application.Specimens.SpecimenService(
            new EfRepository<Specimen>(verify), new EfRepository<Patient>(verify), new EfRepository<SpecimenTypeDefinition>(verify),
            verify, _factory.Clock, transfusions: new EfRepository<TransfusionEvent>(verify));
        foreach (var accession in new[] { "ACC0001", "ACC-PEND-COL", "ACC-PEND-RCV" })
        {
            var specimen = await verify.Specimens.SingleAsync(s => s.AccessionNumber == accession);
            var preview = await service.PreviewExpirationAsync(patient.Id, specimen.SpecimenType, specimen.CollectedUtc);
            Assert.True(preview.Succeeded, preview.Error);
            Assert.Equal(preview.Value!.PolicyExpiresUtc, specimen.ExpiresUtc);
        }
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
            new EfRepository<PatientBloodTypeHistory>(verify),
            new EfRepository<Override>(verify)).ListByPatientAsync(helen.Id);
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
            Assert.Equal(18, await verify.Patients.CountAsync()); // 16 named demo patients, the unidentified trauma patient, and the demo newborn
        }
    }

    // TEST-BB-520: the example tests are seeded once each as inactive drafts that would pass validation if activated,
    // with their subtests; the anti-K titer demo shows two titers three weeks apart.
    [Fact]
    public async Task Seed_AddsExampleTestsAsValidDrafts_AndTheTiterDemo()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        string[] codes = ["ABTITER", "ABOTITER", "FMHQ", "DATIGG", "ELUTION", "WEAKDP", "RHKPHENO"];
        var subtests = (await verify.SubtestDefinitions.Where(s => s.IsActive && !s.IsDraft).Select(s => s.Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var attributes = (await verify.BloodAttributeDefinitions.Where(a => a.IsActive).Select(a => a.Code).ToListAsync()).ToHashSet(StringComparer.Ordinal);
        var phases = (await verify.PhaseDefinitions.Where(p => p.IsActive && !p.IsDraft).ToListAsync())
            .ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes)
        {
            var definition = Assert.Single(await verify.TestDefinitions.Where(t => t.Code == code).ToListAsync());
            Assert.True(definition.IsDraft && !definition.IsActive, code);
            Assert.Equal(DatabaseSeeder.ExampleTestReason, definition.ChangeReason);
            var evaluation = BloodBankLIS.Domain.Rules.Config.TestDefinitionValidator.Validate(
                definition, false, subtests, attributes, null, phases);
            Assert.False(evaluation.IsHardStopped, $"{code}: {string.Join("; ", evaluation.HardStops.Select(h => h.Message))}");
        }

        Assert.True(subtests.IsSupersetOf(["IGG", "C3D", "DATCTL", "LASTWASH"]));
        var titer = await verify.TestDefinitions.SingleAsync(t => t.Code == "ABTITER");
        Assert.Equal((1, 2048, "AHG", "1+", true), (titer.TiterStartDilution, titer.TiterEndDilution, titer.TiterPhaseCode, titer.TiterEndpointMinGrade, titer.TiterScored));
        var fmhq = await verify.TestDefinitions.SingleAsync(t => t.Code == "FMHQ");
        Assert.Equal(("%", 2), (fmhq.ResultUnits, fmhq.DecimalPlaces));
        Assert.Null(fmhq.ReferenceHigh);

        var patient = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0004");
        var titers = await verify.TestResults.Where(r => r.PatientId == patient.Id && r.TestCode == "ABTITER").OrderBy(r => r.VerifiedUtc).ToListAsync();
        Assert.Equal(["Titer 1:8 (score 28)", "Titer 1:32 (score 48)"], titers.Select(r => r.Interpretation));
        Assert.Equal(2, titers.Select(r => r.SpecimenId).Distinct().Count());
    }

    // TEST-BB-528: starter translations are seeded once per field (TNS <-> LAB10117, patient classes, race and
    // ethnicity), a field the site has edited is left alone, demo type and screen lines keep their grouper, two demo
    // patients have race and ethnicity, and one unmapped order code is listed.
    [Fact]
    public async Task Seed_AddsStarterTranslations_OncePerField_AndTheTranslationDemo()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
            var race = await context.InterfaceValueTranslations.Where(t => t.DataItemKey == "Patient.Race" && t.InternalValue == "OTHER").ToListAsync();
            context.InterfaceValueTranslations.RemoveRange(race);
            await context.SaveChangesAsync();
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var tns = Assert.Single(await verify.InterfaceValueTranslations.Where(t => t.DataItemKey == "Procedure.Code").ToListAsync());
        Assert.Equal(("TNS", "LAB10117", InterfaceTranslationDirection.Both, (long?)null), (tns.InternalValue, tns.ExternalValue, tns.Direction, tns.EndpointId));
        var classes = await verify.InterfaceValueTranslations.Where(t => t.DataItemKey == "Encounter.PatientClass").ToListAsync();
        Assert.Equal(5, classes.Count);
        Assert.Equal(InterfaceTranslationDirection.Outbound, classes.Single(c => c.InternalValue == "Observation").Direction);
        // The site removed OTHER: a later seed does not put it back.
        Assert.Equal(5, await verify.InterfaceValueTranslations.CountAsync(t => t.DataItemKey == "Patient.Race"));
        Assert.Equal(2, await verify.InterfaceValueTranslations.CountAsync(t => t.DataItemKey == "Patient.Ethnicity"));

        var tnsLines = await verify.OrderLines.Where(l => l.Order!.TestCode == "TNS" && (l.TestCode == "ABORH" || l.TestCode == "ABSC")).ToListAsync();
        Assert.NotEmpty(tnsLines);
        Assert.All(tnsLines, l => Assert.Equal("TNS", l.GrouperCode));
        var first = await verify.Patients.SingleAsync(p => p.MedicalRecordNumber == "MRN0001");
        Assert.Equal(("WHITE", "NOT-HISPANIC"), (first.Race, first.Ethnicity));
        var unmapped = Assert.Single(await verify.InterfaceUnmappedValues.Where(v => v.InstrumentInterfaceId == null).ToListAsync());
        Assert.Equal(("Order.TestCode", "LAB99999", (DateTime?)null), (unmapped.DataItemKey, unmapped.Value, unmapped.ResolvedUtc));
    }

    // TEST-BB-543: demo reagents are seeded once with the tests they are used for and an open lot each, and the demo's
    // recent manual results are recorded against those lots, so the lot lookback has rows.
    [Fact]
    public async Task Seed_AddsDemoReagentLots_AndRecordsRecentManualResults_Once()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var reagents = await verify.Reagents.ToListAsync();
        Assert.Equal(7, reagents.Count);
        Assert.Equal("ABSC, DAT, XM", reagents.Single(r => r.Code == "AHG").TestCodes);
        Assert.Equal(7, await verify.ReagentLots.CountAsync(l => l.State == ReagentLotState.Opened));
        var recorded = await verify.TestResultReagentLots.ToListAsync();
        Assert.NotEmpty(recorded);
        Assert.Equal(recorded.Count, recorded.Select(r => (r.TestResultId, r.ReagentLotId)).Distinct().Count());
    }

    // TEST-BB-536: an upgraded database: the old assay and analyte map is copied into translations by the migration SQL,
    // the value translations are added, and only the text rules still exactly as seeded are turned off.
    [Fact]
    public async Task Upgrade_CopiesTheOldMap_AndTurnsOffOnlyUnchangedTextRules()
    {
        using var fresh = new SqliteContextFactory();
        await using (var context = fresh.Create())
        {
            var echo = new InstrumentInterface
            {
                Name = DatabaseSeeder.EchoInterfaceName,
                Connection = new InstrumentConnection { Port = DatabaseSeeder.EchoListenPort },
                Rules =
                [
                    new InstrumentInterfaceRule { Name = "B Pos", ConditionExpression = "message.test_interp = 'B Pos'", ActionExpression = "set('test_interp','B|Positive')", IsEnabled = true },
                    new InstrumentInterfaceRule { Name = "A Pos", ConditionExpression = "message.test_interp = 'A Pos'", ActionExpression = "set('test_interp','A|Positive'); set('error','W')", IsEnabled = true }
                ]
            };
            context.InstrumentInterfaces.Add(echo);
            await context.SaveChangesAsync();
            context.InstrumentTestMappings.AddRange(
                new InstrumentTestMapping { InterfaceId = echo.Id, InstrumentCode = "Group", ApplicationCode = "ABORH", Kind = InstrumentMappingKind.Assay, Direction = InterfaceTranslationDirection.Both },
                new InstrumentTestMapping { InterfaceId = echo.Id, InstrumentCode = "Group2", ApplicationCode = "ABORH", Kind = InstrumentMappingKind.Assay, Direction = InterfaceTranslationDirection.Inbound },
                new InstrumentTestMapping { InterfaceId = echo.Id, InstrumentCode = "Anti-A", ApplicationCode = "Anti-A", Kind = InstrumentMappingKind.Analyte, Direction = InterfaceTranslationDirection.Inbound });
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync(BloodBankLIS.Infrastructure.Persistence.Migrations.InstrumentValueTranslations.CopyTestMappingsSql);
            await DatabaseSeeder.SeedInstrumentInterfacesAsync(context, CancellationToken.None);
        }

        await using var verify = fresh.Create();
        var upgraded = await verify.InstrumentInterfaces.Include(i => i.Translations).Include(i => i.Rules).SingleAsync();
        var codes = upgraded.Translations.Where(t => t.DataItemKey != BloodBankLIS.Domain.Instruments.InstrumentTranslationKeys.Interpretation
                && t.DataItemKey != BloodBankLIS.Domain.Instruments.InstrumentTranslationKeys.ErrorCode && t.LocalValue != "KEEP")
            .Select(t => (t.DataItemKey, t.InstrumentValue, t.LocalValue, t.Direction)).OrderBy(t => t.DataItemKey).ThenBy(t => t.InstrumentValue).ToList();
        Assert.Equal(
            [
                ("Instrument.OrderTestCode", "Group", "ABORH", InterfaceTranslationDirection.Outbound),
                ("Instrument.ResultTestCode", "Group", "ABORH", InterfaceTranslationDirection.Inbound),
                ("Instrument.ResultTestCode", "Group2", "ABORH", InterfaceTranslationDirection.Inbound),
                ("Instrument.SubtestCode", "Anti-A", "Anti-A", InterfaceTranslationDirection.Inbound)
            ],
            codes);
        Assert.Contains(upgraded.Translations, t => t.InstrumentValue == "B Pos" && t.LocalValue == "B|Positive");
        Assert.False(upgraded.Rules.Single(r => r.Name == "B Pos").IsEnabled);
        // A site's edited rule is left as it is.
        Assert.True(upgraded.Rules.Single(r => r.Name == "A Pos").IsEnabled);
    }

    // TEST-BB-536: the Echo is seeded with its codes as translations (the retired map), its interpretations and error
    // codes as translations instead of text rules, and one unmapped instrument assay for the list.
    [Fact]
    public async Task Seed_GivesTheEchoTranslations_InsteadOfTextRules()
    {
        await using (var context = _factory.Create())
        {
            await DatabaseSeeder.SeedAsync(context);
            await DatabaseSeeder.SeedAsync(context);
        }

        await using var verify = _factory.Create();
        var echo = await verify.InstrumentInterfaces.Include(i => i.Translations).Include(i => i.Rules)
            .SingleAsync(i => i.Name == DatabaseSeeder.EchoInterfaceName);
        string Local(string key, string value) => echo.Translations.Single(t => t.DataItemKey == key && t.InstrumentValue == value).LocalValue;
        Assert.Equal("ABORH", Local(BloodBankLIS.Domain.Instruments.InstrumentTranslationKeys.ResultTestCode, "Group"));
        Assert.Equal("Cell1", Local(BloodBankLIS.Domain.Instruments.InstrumentTranslationKeys.SubtestCode, "Screen 1"));
        Assert.Equal("B|Positive", Local(BloodBankLIS.Domain.Instruments.InstrumentTranslationKeys.Interpretation, "B Pos"));
        Assert.Equal("Ignore", Local(BloodBankLIS.Domain.Instruments.InstrumentTranslationKeys.ErrorCode, "N"));
        Assert.All(echo.Translations.Where(t => t.DataItemKey == BloodBankLIS.Domain.Instruments.InstrumentTranslationKeys.ErrorCode),
            t => Assert.False(string.IsNullOrWhiteSpace(t.Description)));
        Assert.Single(echo.Translations, t => t.DataItemKey == BloodBankLIS.Domain.Instruments.InstrumentTranslationKeys.OrderTestCode && t.LocalValue == "ABORH");
        Assert.DoesNotContain(echo.Rules, r => r.IsEnabled && r.Name == "B Pos");
        Assert.Contains(echo.Rules, r => r.IsEnabled && r.Name == "Numeric 80 to 4+");
        Assert.True(await verify.InterfaceUnmappedValues.AnyAsync(v => v.InstrumentInterfaceId == echo.Id && v.Value == "Antigen"));
    }
}
