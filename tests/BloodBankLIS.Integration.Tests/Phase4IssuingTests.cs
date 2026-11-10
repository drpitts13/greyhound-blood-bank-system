using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Printing;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class Phase4IssuingTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public Phase4IssuingTests(SqliteContextFactory factory) => _factory = factory;

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
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<SpecialRequirementDefinition>(c),
            _factory.Clock);

    private CompatibilityService Compatibility(BloodBankDbContext c) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            BloodAttrCompat(c), AntibodyScreenCompat(c), c, _factory.Clock, _factory.CurrentUser,
            new EfRepository<Issue>(c), new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            workups: new EfRepository<AntibodyIdentificationWorkup>(c),
            retypes: new EfRepository<ProductRetypeResult>(c));

    private CompatibilityService CompatibilityWithAlerts(BloodBankDbContext c) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            BloodAttrCompat(c), AntibodyScreenCompat(c), c, _factory.Clock, _factory.CurrentUser,
            new EfRepository<Issue>(c), new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            workups: new EfRepository<AntibodyIdentificationWorkup>(c),
            retypes: new EfRepository<ProductRetypeResult>(c),
            alerts: new AlertService(new EfRepository<Alert>(c), c, _factory.Clock, _factory.CurrentUser,
                new AuditWriter(c, _factory.Clock, _factory.CurrentUser)),
            deviations: new EfRepository<Deviation>(c));

    private IssuingService Issuing(BloodBankDbContext c, IPermissionEvaluator? permissions = null, IProductDispensePublisher? dispense = null, bool outages = false)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        return new IssuingService(
            new InventoryRepository(c), new EfRepository<Issue>(c), new EfRepository<Allocation>(c),
            new EfRepository<Crossmatch>(c), new EfRepository<Return>(c), new EfRepository<TransfusionEvent>(c),
            new EfRepository<Override>(c), new EfRepository<Patient>(c), new EfRepository<Specimen>(c),
            new EfRepository<ProductType>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<ExceptionDefinition>(c),
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<ProductAttribute>(c),
            new EfRepository<ProductAttributeAssignment>(c),
            new EfRepository<Order>(c),
            new EfRepository<User>(c),
            new EfRepository<InventoryLocation>(c),
            BloodAttrCompat(c),
            AntibodyScreenCompat(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            new ReactionInvestigationService(
                new EfRepository<ReactionInvestigation>(c),
                new EfRepository<TransfusionEvent>(c),
                new InventoryRepository(c),
                c, _factory.Clock, _factory.CurrentUser, audit),
            permissions ?? new FixedPermissionEvaluator(3),
            c, _factory.Clock, _factory.CurrentUser, audit,
            workups: new EfRepository<AntibodyIdentificationWorkup>(c),
            requirementDefinitions: new EfRepository<SpecialRequirementDefinition>(c),
            antibodies: new EfRepository<AntibodyHistory>(c),
            orderActions: new EfRepository<OrderAction>(c),
            retypes: new EfRepository<ProductRetypeResult>(c),
            autoThawRequests: new EfRepository<AutoThawRequest>(c),
            mtpActivations: new EfRepository<MtpActivation>(c),
            compatibilityVersions: new EfRepository<CompatibilityRuleVersion>(c),
            compatibilityRules: new EfRepository<CompatibilityRule>(c),
            unitSpecialTests: new EfRepository<BloodComponentSpecialTest>(c),
            dispensePublisher: dispense,
            orderLines: new EfRepository<OrderLine>(c),
            outages: outages ? new EfRepository<DowntimeEpisode>(c) : null,
            electronicCrossmatch: new ElectronicCrossmatchEligibilityService(
                new EfRepository<Patient>(c),
                new EfRepository<PatientBloodTypeHistory>(c),
                new EfRepository<AntibodyHistory>(c),
                new EfRepository<AntibodyIdentificationWorkup>(c),
                AntibodyScreenCompat(c),
                new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
                clock: _factory.Clock));
    }

    private UnitActivityService Activity(BloodBankDbContext c) =>
        new(
            new InventoryRepository(c),
            new EfRepository<Allocation>(c),
            new EfRepository<Crossmatch>(c),
            new EfRepository<Issue>(c),
            new EfRepository<Return>(c),
            new EfRepository<TransfusionEvent>(c),
            new EfRepository<OrderAction>(c),
            new EfRepository<ActionDefinition>(c),
            new EfRepository<UnitModificationUnit>(c),
            new EfRepository<UnitModification>(c),
            new EfRepository<Patient>(c));

    private InventoryService Inventory(BloodBankDbContext c)
    {
        var lookups = new IsbtLookupCatalog(
            new EfRepository<IsbtAboRhdCode>(c),
            new EfRepository<IsbtProductCode>(c));
        return new InventoryService(
            new InventoryRepository(c),
            new EfRepository<UnitBloodAttribute>(c),
            new EfRepository<BloodAttributeDefinition>(c),
            lookups,
            c,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<User>(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            new EfRepository<Patient>(c));
    }

    private sealed record Scenario(
        long PatientId, long SpecimenId, long UnitId, long ProductTypeId, string Mrn, string DateOfBirth = "1975-06-01", string UnitNumber = "");

    private static IssueUnitRequest IssueReq(
        Scenario s,
        IssueType issueType = IssueType.Standard,
        string? overrideReason = null,
        string? authorizedBy = null) =>
        new(s.UnitId, s.PatientId,
            PatientIdentifier1Value: s.Mrn,
            PatientIdentifier2Value: s.DateOfBirth,
            IssueType: issueType,
            OverrideReason: overrideReason,
            AuthorizedBy: authorizedBy,
            Appearance: UnitAppearance.Acceptable);

    private static DocumentTransfusionRequest TxReq(
        Scenario s,
        TransfusionDisposition disposition = TransfusionDisposition.Completed,
        bool reactionSuspected = false,
        ComponentScanVerificationRequest? bedsideScan = null,
        string? secondVerifier = null) =>
        new(disposition,
            ReactionSuspected: reactionSuspected,
            BedsideScan: bedsideScan,
            SecondVerifier: secondVerifier,
            PatientIdentifier1Value: s.Mrn,
            PatientIdentifier2Value: s.DateOfBirth);

    /// <summary>
    /// Seeds a patient with a known current ABO/Rh, an accepted specimen, a product
    /// type, and an Available unit. Caller controls compatibility via the ABO/Rh args.
    /// </summary>
    private async Task<Scenario> SeedAsync(
        string key,
        AboGroup patientAbo = AboGroup.O, RhType patientRh = RhType.Positive,
        AboGroup unitAbo = AboGroup.O, RhType unitRh = RhType.Positive,
        bool requiresCrossmatch = true,
        bool includeHistoricalAbo = true,
        bool includeCurrentAbo = true,
        Sex sex = Sex.Unknown,
        DateOnly? dateOfBirth = null,
        bool requiresRetype = false,
        UnitStatus unitStatus = UnitStatus.Available,
        ComponentClass componentClass = ComponentClass.RedBloodCells)
    {
        await using var c = _factory.Create();
        var birth = dateOfBirth ?? new DateOnly(1975, 6, 1);

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}",
            LastName = "Issue",
            FirstName = "Test",
            DateOfBirth = birth,
            Sex = sex
        };
        c.Patients.Add(patient);

        var productType = new ProductType
        {
            ProductCode = $"RBC-{key}",
            Name = "Test RBC",
            ComponentClass = componentClass,
            RequiresCrossmatch = requiresCrossmatch,
            RequiresRetype = requiresRetype
        };
        c.ProductTypes.Add(productType);
        await c.SaveChangesAsync();

        if (includeCurrentAbo)
        {
            c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
            {
                PatientId = patient.Id,
                Abo = patientAbo,
                RhD = patientRh,
                Source = BloodTypeSource.TestResult,
                IsCurrent = true
            });
        }
        if (includeHistoricalAbo && includeCurrentAbo)
        {
            c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
            {
                PatientId = patient.Id,
                Abo = patientAbo,
                RhD = patientRh,
                Source = BloodTypeSource.HistoricalImport,
                IsCurrent = false
            });
        }

        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{key}",
            PatientId = patient.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2),
            ReceivedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);

        var unit = new BloodUnit
        {
            UnitNumber = $"U-{key}",
            ProductTypeId = productType.Id,
            Abo = unitAbo,
            RhD = unitRh,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = unitStatus
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        return new Scenario(patient.Id, specimen.Id, unit.Id, productType.Id, $"MRN-{key}", birth.ToString("yyyy-MM-dd"), unit.UnitNumber);
    }

    private async Task RecordCompatibleCrossmatchAsync(Scenario s)
    {
        await using var c = _factory.Create();
        var result = await Compatibility(c).RecordCrossmatchAsync(
            new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Compatible));
        Assert.True(result.Succeeded);
        // TEST-BB-422: the clinical status matches the recorded result.
        Assert.Equal(CrossmatchClinicalStatus.Compatible, result.Value!.ClinicalStatus);
    }

    private async Task AllocateAsync(Scenario s)
    {
        await using var c = _factory.Create();
        var result = await Compatibility(c).AllocateUnitAsync(new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Allocate_WithoutVerifiedRetype_IsBlocked_UntilRetypeThenEmergencyCanReleaseUnretyped()
    {
        var pending = await SeedAsync("RETYPE-ALLOC", requiresRetype: true);
        await using (var blocked = _factory.Create())
        {
            var denied = await Compatibility(blocked).AllocateUnitAsync(
                new AllocateUnitRequest(pending.UnitId, pending.PatientId, pending.SpecimenId));
            Assert.False(denied.Succeeded);
            Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == ProductRetypeAllocationRule.Code);
        }

        await using (var confirm = _factory.Create())
        {
            var test = new TestDefinition
            {
                Code = "ABORH-RETYPE-POS-ALLOC",
                Name = "ABO/Rh retype",
                Category = TestCategory.AboRh,
                IsActive = true
            };
            confirm.TestDefinitions.Add(test);
            await confirm.SaveChangesAsync();
            confirm.ProductRetypeResults.Add(new ProductRetypeResult
            {
                BloodProductId = pending.UnitId,
                TestDefinitionId = test.Id,
                TestCode = test.Code,
                MatchesLabel = true,
                Status = ResultStatus.Verified,
                InterpretedAbo = AboGroup.O,
                InterpretedRh = RhType.Positive,
                EnteredBy = "tech",
                EnteredUtc = _factory.Clock.UtcNow,
                VerifiedBy = "tech",
                VerifiedUtc = _factory.Clock.UtcNow
            });
            await confirm.SaveChangesAsync();
        }

        await using (var allowed = _factory.Create())
        {
            var reserved = await Compatibility(allowed).AllocateUnitAsync(
                new AllocateUnitRequest(pending.UnitId, pending.PatientId, pending.SpecimenId));
            Assert.True(reserved.Succeeded, reserved.Error);
        }

        var emergency = await SeedAsync("RETYPE-EMERG", requiresRetype: true, unitStatus: UnitStatus.Received);
        await using var issue = _factory.Create();
        var standard = await Issuing(issue).IssueUnitAsync(IssueReq(emergency));
        Assert.True(standard.Evaluation!.IsHardStopped);
        Assert.Contains(standard.Evaluation!.HardStops, r => r.Code == ProductRetypeAllocationRule.Code);

        var released = await Issuing(issue).IssueUnitAsync(IssueReq(
            emergency,
            IssueType.EmergencyRelease,
            overrideReason: "Emergency release before ABO/Rh retype",
            authorizedBy: "dr-authorizer"));
        Assert.True(released.Succeeded, released.Error ?? string.Join("; ", released.Evaluation?.HardStops.Select(r => r.Message) ?? []));
        Assert.Contains(released.Evaluation!.Warnings, r => r.Code == ProductRetypeAllocationRule.Code);
        Assert.Equal(UnitStatus.Issued, (await issue.BloodUnits.FindAsync(emergency.UnitId))!.Status);
    }

    [Fact]
    public async Task GetLatestForUnitNumber_ReturnsNewestIssue_IgnoringCaseAndSpaces()
    {
        var s = await SeedAsync("LATEST-ISSUE");
        long newerId;
        await using (var seed = _factory.Create())
        {
            // Only one open issue per unit; an earlier issue that came back is how a unit gets a second one.
            var older = new Issue { BloodProductId = s.UnitId, PatientId = s.PatientId, IssuedUtc = _factory.Clock.UtcNow.AddHours(-5), IssuedBy = "tech1", Status = IssueStatus.Returned };
            var newer = new Issue { BloodProductId = s.UnitId, PatientId = s.PatientId, IssuedUtc = _factory.Clock.UtcNow.AddHours(-1), IssuedBy = "tech1" };
            seed.Issues.AddRange(older, newer);
            await seed.SaveChangesAsync();
            newerId = newer.Id;
        }

        await using var context = _factory.Create();
        var found = await Issuing(context).GetLatestForUnitNumberAsync("  u-latest-issue ");
        var missing = await Issuing(context).GetLatestForUnitNumberAsync("U-NOT-A-UNIT");

        Assert.Equal(newerId, found?.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task Allocate_OpenAntibodyIdWorkup_WarnsAndReserves()
    {
        var s = await SeedAsync("ABID-ALLOC-OPEN");
        await using (var context = _factory.Create())
        {
            context.AntibodyIdentificationWorkups.Add(new AntibodyIdentificationWorkup
            {
                PatientId = s.PatientId,
                PrimaryLotId = 1,
                Status = AntibodyWorkupStatus.InProgress
            });
            await context.SaveChangesAsync();
        }

        await using var act = _factory.Create();
        var result = await Compatibility(act).AllocateUnitAsync(
            new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId));
        Assert.True(result.Succeeded, result.Error);
        Assert.Contains(
            result.Evaluation!.Warnings,
            r => r.Code == AntibodyIdentificationHistoryPostRule.AllocateOpenCode);
        Assert.Equal(AllocationStatus.Reserved, result.Value!.Status);
    }

    // TEST-BB-595: several reservations released to stock with one reason; each is released and audited on its own, and
    // one that cannot be released (unknown, already released) is reported without stopping the others.
    [Fact]
    public async Task ReleaseBatch_ReleasesEach_AndReportsThoseItCannot()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var s = await SeedAsync($"RELB-{suffix}");
        await RecordCompatibleCrossmatchAsync(s);

        await using var c = _factory.Create();
        var reserved = await Compatibility(c).AllocateUnitAsync(new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId));
        Assert.True(reserved.Succeeded, reserved.Error);
        var id = reserved.Value!.Id;

        Assert.Contains("reason is required", (await Compatibility(c).ReleaseAllocationsAsync(new ReleaseAllocationsRequest([id], " "))).Error);
        Assert.Contains("at least one", (await Compatibility(c).ReleaseAllocationsAsync(new ReleaseAllocationsRequest([], "Ended"))).Error);

        var batch = await Compatibility(c).ReleaseAllocationsAsync(new ReleaseAllocationsRequest([id, id, -1], " Reservation ended "));
        Assert.True(batch.Succeeded, batch.Error);
        Assert.Equal(2, batch.Value!.Count);
        Assert.True(batch.Value.Single(o => o.AllocationId == id).Released);
        Assert.False(batch.Value.Single(o => o.AllocationId == -1).Released);
        Assert.NotNull(batch.Value.Single(o => o.AllocationId == -1).Error);
        Assert.Equal(AllocationStatus.Released, (await c.Allocations.AsNoTracking().SingleAsync(a => a.Id == id)).Status);
        Assert.True(await c.AuditEvents.AnyAsync(e => e.EntityType == nameof(Allocation) && e.EntityId == id && e.Reason == "Reservation ended"));

        var again = await Compatibility(c).ReleaseAllocationsAsync(new ReleaseAllocationsRequest([id], "Again"));
        Assert.False(Assert.Single(again.Value!).Released);
    }

    [Fact]
    public async Task AllocateAndRelease_WriteAssignmentWithUnitIdentity()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var s = await SeedAsync($"ALAUD-{suffix}");
        await RecordCompatibleCrossmatchAsync(s);

        await using var c = _factory.Create();
        var unit = await c.BloodUnits.FindAsync(s.UnitId);
        Assert.NotNull(unit);
        unit!.Din = $"W0000{suffix}";
        await c.SaveChangesAsync();

        var reserved = await Compatibility(c).AllocateUnitAsync(
            new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId));
        Assert.True(reserved.Succeeded, reserved.Error);

        var released = await Compatibility(c).ReleaseAllocationAsync(reserved.Value!.Id, "Not needed");
        Assert.True(released.Succeeded, released.Error);

        var events = c.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Assignment
            && e.EntityType == nameof(Allocation)
            && e.EntityId == reserved.Value.Id
            && e.Reason == "Unit assigned to patient."
            && e.NewValueJson is not null
            && e.NewValueJson.Contains($"\"UnitNumber\":\"{unit.UnitNumber}\"")
            && e.NewValueJson.Contains($"\"Din\":\"{unit.Din}\"")
            && e.NewValueJson.Contains($"\"BloodProductId\":{unit.Id}"));
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Assignment
            && e.EntityType == nameof(Allocation)
            && e.EntityId == reserved.Value.Id
            && e.Reason == "Not needed"
            && e.NewValueJson is not null
            && e.NewValueJson.Contains($"\"UnitNumber\":\"{unit.UnitNumber}\"")
            && e.NewValueJson.Contains($"\"Din\":\"{unit.Din}\"")
            && e.NewValueJson.Contains($"\"BloodProductId\":{unit.Id}"));
    }

    [Fact]
    public async Task WardReceipt_WritesTransfusionWithUnitIdentity()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var s = await SeedAsync($"WRAUD-{suffix}");
        await RecordCompatibleCrossmatchAsync(s);

        await using var c = _factory.Create();
        var unit = await c.BloodUnits.FindAsync(s.UnitId);
        Assert.NotNull(unit);
        unit!.Din = $"W0000{suffix}";
        await c.SaveChangesAsync();

        var reserved = await Compatibility(c).AllocateUnitAsync(
            new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId));
        Assert.True(reserved.Succeeded, reserved.Error);

        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Succeeded, issued.Error);

        var receipt = await Issuing(c).RecordWardReceiptAsync(
            issued.Value!.Id, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable));
        Assert.True(receipt.Succeeded, receipt.Error);

        var events = c.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Transfusion
            && e.EntityType == nameof(Issue)
            && e.EntityId == issued.Value.Id
            && e.Reason == "Ward receipt of issued unit."
            && e.NewValueJson is not null
            && e.NewValueJson.Contains($"\"UnitNumber\":\"{unit.UnitNumber}\"")
            && e.NewValueJson.Contains($"\"Din\":\"{unit.Din}\"")
            && e.NewValueJson.Contains($"\"BloodProductId\":{unit.Id}")
            && e.NewValueJson.Contains($"\"IssueId\":{issued.Value.Id}"));
    }

    [Fact]
    public async Task Issue_WritesIssueWithUnitIdentity()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var s = await SeedAsync($"ISAUD-{suffix}");
        await RecordCompatibleCrossmatchAsync(s);

        await using var c = _factory.Create();
        var unit = await c.BloodUnits.FindAsync(s.UnitId);
        Assert.NotNull(unit);
        unit!.Din = $"W0000{suffix}";
        await c.SaveChangesAsync();

        var reserved = await Compatibility(c).AllocateUnitAsync(
            new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId));
        Assert.True(reserved.Succeeded, reserved.Error);

        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Succeeded, issued.Error);

        var events = c.AuditEvents.ToList();
        Assert.Contains(events, e =>
            e.EventType == AuditEventType.Issue
            && e.EntityType == nameof(BloodUnit)
            && e.EntityId == unit.Id
            && e.NewValueJson is not null
            && e.NewValueJson.Contains($"\"UnitNumber\":\"{unit.UnitNumber}\"")
            && e.NewValueJson.Contains($"\"Din\":\"{unit.Din}\"")
            && e.NewValueJson.Contains($"\"BloodProductId\":{unit.Id}")
            && e.NewValueJson.Contains("\"IssueType\""));
    }

    [Fact]
    public async Task RecordCrossmatch_OpenAntibodyIdWorkup_WarnsAndRecords()
    {
        var s = await SeedAsync("ABID-XM-OPEN");
        await using (var context = _factory.Create())
        {
            context.AntibodyIdentificationWorkups.Add(new AntibodyIdentificationWorkup
            {
                PatientId = s.PatientId,
                PrimaryLotId = 1,
                Status = AntibodyWorkupStatus.InProgress
            });
            await context.SaveChangesAsync();
        }

        await using var act = _factory.Create();
        var result = await Compatibility(act).RecordCrossmatchAsync(
            new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Compatible));
        Assert.True(result.Succeeded, result.Error);
        Assert.Contains(
            result.Evaluation!.Warnings,
            r => r.Code == AntibodyIdentificationHistoryPostRule.CrossmatchOpenCode);
        Assert.Equal(CrossmatchResult.Compatible, result.Value!.Result);
    }

    [Fact]
    public async Task Issue_OpenAntibodyIdWorkup_WarnsAndIssues()
    {
        var s = await SeedAsync("ABID-ISSUE-OPEN");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        await using (var context = _factory.Create())
        {
            context.AntibodyIdentificationWorkups.Add(new AntibodyIdentificationWorkup
            {
                PatientId = s.PatientId,
                PrimaryLotId = 1,
                Status = AntibodyWorkupStatus.InProgress
            });
            await context.SaveChangesAsync();
        }

        await using var act = _factory.Create();
        var issued = await Issuing(act).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Succeeded, issued.Error);
        Assert.Contains(
            issued.Evaluation!.Warnings,
            r => r.Code == AntibodyIdentificationHistoryPostRule.IssueOpenCode);
        Assert.Equal(IssueStatus.Issued, issued.Value!.Status);
    }

    [Fact]
    public async Task FullPath_Crossmatch_Allocate_Issue_Transfuse()
    {
        var s = await SeedAsync("HAPPY");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.True(issued.Succeeded);
            Assert.Equal(IssueStatus.Issued, issued.Value!.Status);
            Assert.Equal(UnitAppearance.Acceptable, issued.Value.IssueAppearance);
            issueId = issued.Value.Id;
        }

        await using (var verify = _factory.Create())
        {
            Assert.Equal(UnitStatus.Issued, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
            Assert.Equal(AllocationStatus.Consumed, await verify.Allocations.Where(a => a.BloodProductId == s.UnitId).Select(a => a.Status).SingleAsync());
        }

        await using (var c = _factory.Create())
        {
            var receipt = await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable));
            Assert.True(receipt.Succeeded);
            Assert.Equal("ward-nurse", receipt.Value!.WardReceivedBy);

            var transfused = await Issuing(c).DocumentTransfusionAsync(issueId, TxReq(s));
            Assert.True(transfused.Succeeded);
        }

        await using (var verify = _factory.Create())
        {
            Assert.Equal(UnitStatus.Transfused, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
            Assert.Equal(IssueStatus.Transfused, (await verify.Issues.FindAsync(issueId))!.Status);
            var xm = await verify.Crossmatches.SingleAsync(x => x.BloodProductId == s.UnitId);
            var allocation = await verify.Allocations.SingleAsync(a => a.BloodProductId == s.UnitId);
            Assert.True(await verify.AuditEvents.AnyAsync(a =>
                a.EventType == AuditEventType.Crossmatch && a.EntityId == xm.Id));
            Assert.True(await verify.AuditEvents.AnyAsync(a =>
                a.EventType == AuditEventType.Assignment && a.EntityId == allocation.Id));
            Assert.True(await verify.AuditEvents.AnyAsync(a =>
                a.EventType == AuditEventType.Issue && a.EntityId == s.UnitId));
            Assert.True(await verify.AuditEvents.AnyAsync(a =>
                a.EventType == AuditEventType.Transfusion && a.EntityId == s.UnitId));
        }
    }

    [Fact]
    public async Task Issue_WithoutIssueCreate_IsHardStopped()
    {
        var s = await SeedAsync("ISS-PERM");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var denied = await Issuing(c, new FixedPermissionEvaluator(3, PermissionCodes.PatientWrite))
            .IssueUnitAsync(IssueReq(s));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Evaluation!.HardStops, r => r.Code == IssueAuthorizationRule.CreateCode);
        Assert.Equal(UnitStatus.Assigned, (await c.BloodUnits.FindAsync(s.UnitId))!.Status);

        var allowed = await Issuing(c, new FixedPermissionEvaluator(3, PermissionCodes.IssueCreate))
            .IssueUnitAsync(IssueReq(s));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal(IssueStatus.Issued, allowed.Value!.Status);
    }

    [Fact]
    public async Task Autologous_MatchingPatient_Issues()
    {
        var s = await SeedAsync("AUTO-OK");
        await using (var c = _factory.Create())
        {
            var unit = await c.BloodUnits.FindAsync(s.UnitId);
            unit!.DonationRestriction = DonationRestriction.Autologous;
            unit.ReservedPatientId = s.PatientId;
            await c.SaveChangesAsync();
        }

        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using var issue = _factory.Create();
        var issued = await Issuing(issue).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Succeeded, issued.Error);
    }

    [Fact]
    public async Task Autologous_WrongPatient_BlocksAllocate()
    {
        var s = await SeedAsync("AUTO-BAD");
        long otherId;
        await using (var c = _factory.Create())
        {
            var other = new Patient
            {
                MedicalRecordNumber = "MRN-AUTO-OTHER",
                LastName = "Other",
                FirstName = "Patient",
                DateOfBirth = new DateOnly(1982, 2, 2)
            };
            c.Patients.Add(other);
            await c.SaveChangesAsync();
            otherId = other.Id;
            var unit = await c.BloodUnits.FindAsync(s.UnitId);
            unit!.DonationRestriction = DonationRestriction.Autologous;
            unit.ReservedPatientId = otherId;
            await c.SaveChangesAsync();
        }

        await using var act = _factory.Create();
        var result = await Compatibility(act).AllocateUnitAsync(
            new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == AutologousDirectedRule.IssueCode);
    }

    [Fact]
    public async Task Directed_ConvertedToAllogeneic_AllocatesToOtherPatient()
    {
        var s = await SeedAsync("DIR-CONV");
        long otherId;
        await using (var c = _factory.Create())
        {
            var other = new Patient
            {
                MedicalRecordNumber = "MRN-DIR-CONV-OTHER",
                LastName = "Other",
                FirstName = "Recipient",
                DateOfBirth = new DateOnly(1984, 4, 4)
            };
            c.Patients.Add(other);
            await c.SaveChangesAsync();
            otherId = other.Id;
            var unit = await c.BloodUnits.FindAsync(s.UnitId);
            unit!.DonationRestriction = DonationRestriction.Directed;
            unit.ReservedPatientId = otherId;
            if (!await c.Users.AnyAsync(u => u.UserName == "tech2"))
            {
                c.Users.Add(new User
                {
                    UserName = "tech2",
                    DisplayName = "Tech Two",
                    IsActive = true
                });
            }
            await c.SaveChangesAsync();
        }

        await using (var conv = _factory.Create())
        {
            var converted = await Inventory(conv).ConvertDirectedToAllogeneicAsync(
                s.UnitId, "Directed recipient discharged", "tech2");
            Assert.True(converted.Succeeded, converted.Error);
        }

        await using var act = _factory.Create();
        var result = await Compatibility(act).AllocateUnitAsync(
            new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId));
        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public async Task Issue_WithoutCrossmatch_OnRequiredProduct_IsHardStopped()
    {
        var s = await SeedAsync("NOXM");
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));

        Assert.False(issued.Succeeded);
        Assert.NotNull(issued.Evaluation);
        Assert.True(issued.Evaluation!.IsHardStopped);
        Assert.Contains(issued.Evaluation.HardStops, r => r.Code == CrossmatchValidityRule.Code);
    }

    [Fact]
    public async Task Issue_IncompatibleCrossmatch_RequiresOverride()
    {
        var s = await SeedAsync("INCXM");
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var xm = await Compatibility(c).RecordCrossmatchAsync(
            new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Incompatible));
        Assert.True(xm.Succeeded, xm.Error);

        var blocked = await Issuing(c).IssueUnitAsync(IssueReq(s));
        Assert.False(blocked.Succeeded);
        Assert.True(blocked.RequiresOverride);
        Assert.Contains(blocked.Evaluation!.Warnings, r => r.Code == CrossmatchValidityRule.Code);

        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, overrideReason: "Clinically indicated incompatible issue", authorizedBy: "supervisor"));
        Assert.True(issued.Succeeded, issued.Error);
        Assert.Equal(IssueStatus.Issued, issued.Value!.Status);
    }

    // TEST-BB-329: inconsistent times or volume are refused and leave the unit issued; consistent ones document.
    [Fact]
    public async Task DocumentTransfusion_InconsistentTimesOrVolume_AreRefused()
    {
        var s = await SeedAsync("TX-DOC");
        await RecordCompatibleCrossmatchAsync(s);
        long issueId;
        await using (var c = _factory.Create())
        {
            (await c.BloodUnits.FindAsync(s.UnitId))!.Volume = 300m;
            await c.SaveChangesAsync();
            Assert.True((await Compatibility(c).AllocateUnitAsync(new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId))).Succeeded);
            // Issued three hours ago, so a start two hours ago is consistent with the unit's life (TEST-BB-342).
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with
            {
                IssuedUtc = _factory.Clock.UtcNow.AddHours(-3), LateEntryReason = "Charted after the shift"
            });
            Assert.True(issued.Succeeded, issued.Error);
            issueId = issued.Value!.Id;
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
        }

        var now = _factory.Clock.UtcNow;
        await using (var c = _factory.Create())
        {
            var backwards = await Issuing(c).DocumentTransfusionAsync(issueId, TxReq(s) with { StartUtc = now.AddHours(-1), StopUtc = now.AddHours(-2) });
            Assert.Contains(backwards.Evaluation!.HardStops, r => r.Code == TransfusionDocumentationRule.Code);

            var tooMuch = await Issuing(c).DocumentTransfusionAsync(issueId, TxReq(s) with { VolumeTransfused = 450m });
            Assert.Contains(tooMuch.Evaluation!.HardStops, r => r.Message.Contains("more than the unit held"));

            var future = await Issuing(c).DocumentTransfusionAsync(issueId, TxReq(s) with { StartUtc = now.AddHours(2) });
            Assert.True(future.Evaluation!.IsHardStopped);
        }

        await using (var c = _factory.Create())
        {
            Assert.Equal(UnitStatus.Issued, (await c.BloodUnits.FindAsync(s.UnitId))!.Status);
            Assert.False(await c.TransfusionEvents.AnyAsync(t => t.IssueId == issueId));

            var ok = await Issuing(c).DocumentTransfusionAsync(issueId, TxReq(s) with
            {
                StartUtc = now.AddHours(-2), StopUtc = now.AddHours(-1), VolumeTransfused = 280m
            });
            Assert.True(ok.Succeeded, ok.Error);
            Assert.Equal(280m, ok.Value!.VolumeTransfused);
        }
    }

    // TEST-BB-331: a backdated issue needs a reason and keeps both times; a future issue time is refused.
    [Fact]
    public async Task Issue_LateEntry_NeedsReason_AndKeepsBothTimes()
    {
        var s = await SeedAsync("LATE-ISS");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        var now = _factory.Clock.UtcNow;

        await using (var c = _factory.Create())
        {
            var future = await Issuing(c).IssueUnitAsync(IssueReq(s) with { IssuedUtc = now.AddHours(2) });
            Assert.Contains(future.Evaluation!.HardStops, r => r.Code == IssueTimeRule.Code);

            var noReason = await Issuing(c).IssueUnitAsync(IssueReq(s) with { IssuedUtc = now.AddHours(-3) });
            Assert.Contains(noReason.Evaluation!.HardStops, r => r.Code == IssueTimeRule.Code);
            Assert.False(await c.Issues.AnyAsync(i => i.BloodProductId == s.UnitId));
        }

        long issueId;
        await using (var c = _factory.Create())
        {
            var late = await Issuing(c).IssueUnitAsync(IssueReq(s) with
            {
                IssuedUtc = now.AddHours(-3), LateEntryReason = "Issued on paper during downtime"
            });
            Assert.True(late.Succeeded, late.Error ?? string.Join("; ", late.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            issueId = late.Value!.Id;
            Assert.Equal(now.AddHours(-3), late.Value.IssuedUtc);
            Assert.Equal("Issued on paper during downtime", late.Value.LateEntryReason);
            var dto = IssueDto.From(late.Value);
            Assert.Equal("Issued on paper during downtime", dto.LateEntryReason);
            Assert.NotNull(dto.EnteredUtc);
        }

        await using var verify = _factory.Create();
        var audit = await verify.AuditEvents.Where(a => a.EntityType == nameof(BloodUnit) && a.EntityId == s.UnitId
            && a.NewValueJson!.Contains("LateEntryReason")).OrderByDescending(a => a.Id).FirstAsync();
        Assert.Contains("Issued on paper during downtime", audit.NewValueJson);
        Assert.Contains("EnteredUtc", audit.NewValueJson);
    }

    // TEST-BB-392 (OCD-054): a paper issue from downtime, whose unit, specimen, and crossmatch have expired since, can be
    // back-entered only by a role holding issue.downtime-backentry, with the paper record's time, a reason, and its DIN.
    [Fact]
    public async Task DowntimeBackEntry_JudgesExpiryAsOfThePaperTime_AndIsControlledByRole()
    {
        var s = await SeedAsync("DOWNTIME-BE");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        var now = _factory.Clock.UtcNow;
        var paperTime = now.AddHours(-3);

        // Downtime: the unit went out on paper; since then the unit, specimen, and crossmatch expired and the sweep ran.
        await using (var c = _factory.Create())
        {
            var unit = await c.BloodUnits.SingleAsync(u => u.Id == s.UnitId);
            unit.ExpiresUtc = now.AddHours(-1);
            unit.Status = UnitStatus.Expired;
            var specimen = await c.Specimens.SingleAsync(x => x.Id == s.SpecimenId);
            specimen.CollectedUtc = now.AddHours(-6);
            specimen.ExpiresUtc = now.AddMinutes(-30);
            foreach (var xm in await c.Crossmatches.Where(x => x.BloodProductId == s.UnitId).ToListAsync())
            {
                xm.PerformedUtc = now.AddHours(-5);
                xm.ExpiresUtc = now.AddMinutes(-30);
            }

            var allocation = await c.Allocations.SingleAsync(a => a.BloodProductId == s.UnitId);
            allocation.AllocatedUtc = now.AddHours(-5);
            allocation.Status = AllocationStatus.Expired;
            await c.SaveChangesAsync();
        }

        IssueUnitRequest BackEntry(DateTime? at = null, string? din = null) => IssueReq(s) with
        {
            IssuedUtc = at ?? paperTime,
            LateEntryReason = "Issued on paper during LIS downtime",
            DowntimeBackEntry = true,
            BackEntryDin = din ?? s.UnitNumber.ToLowerInvariant()
        };

        await using (var c = _factory.Create())
        {
            // An ordinary late entry is still judged now, so the expired unit is refused.
            var ordinary = await Issuing(c).IssueUnitAsync(IssueReq(s) with { IssuedUtc = paperTime, LateEntryReason = "Issued on paper during LIS downtime" });
            Assert.False(ordinary.Succeeded);

            var noRole = await Issuing(c, new FixedPermissionEvaluator(3, PermissionCodes.IssueCreate)).IssueUnitAsync(BackEntry());
            Assert.Contains(noRole.Evaluation!.HardStops, r => r.Code == DowntimeBackEntryRule.PermissionCode);

            var noTime = await Issuing(c).IssueUnitAsync(BackEntry() with { IssuedUtc = null });
            Assert.Contains(noTime.Evaluation!.HardStops, r => r.Code == DowntimeBackEntryRule.TimeCode);

            var wrongDin = await Issuing(c).IssueUnitAsync(BackEntry(din: "W0000 26 999999"));
            Assert.Contains(wrongDin.Evaluation!.HardStops, r => r.Code == DowntimeBackEntryRule.DinCode);

            // Paper time after the unit expired: still expired then, so still refused.
            var afterExpiry = await Issuing(c).IssueUnitAsync(BackEntry(at: now.AddMinutes(-20)));
            Assert.False(afterExpiry.Succeeded);
            Assert.False(await c.Issues.AnyAsync(i => i.BloodProductId == s.UnitId));
        }

        await using (var c = _factory.Create())
        {
            // As of the paper time the unit and specimen were near expiry: those warnings still need an override.
            var unacknowledged = await Issuing(c).IssueUnitAsync(BackEntry());
            Assert.True(unacknowledged.Evaluation!.RequiresOverride);
            Assert.Contains(unacknowledged.Evaluation.Warnings, w => w.Code == "UNIT-NEAR-EXPIRY");

            var entered = await Issuing(c).IssueUnitAsync(BackEntry() with
            {
                OverrideReason = "Near-expiry unit issued on paper during downtime", AuthorizedBy = "dr-authorizer"
            });
            Assert.True(entered.Succeeded, entered.Error ?? string.Join("; ", entered.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            Assert.True(entered.Value!.DowntimeBackEntry);
            Assert.Equal(paperTime, entered.Value.IssuedUtc);
            Assert.True(IssueDto.From(entered.Value).DowntimeBackEntry);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Issued, (await verify.BloodUnits.SingleAsync(u => u.Id == s.UnitId)).Status);
        var audit = await verify.AuditEvents.Where(a => a.EntityType == nameof(BloodUnit) && a.EntityId == s.UnitId
            && a.NewValueJson!.Contains("\"DowntimeBackEntry\":true")).SingleAsync();
        Assert.Contains("Issued on paper during LIS downtime", audit.NewValueJson);
        Assert.True(await verify.InventoryStatusHistory.AnyAsync(h => h.BloodProductId == s.UnitId && h.Reason!.StartsWith("Downtime back-entry")));
    }

    // TEST-BB-854 (OCD-081, decided 2026-11-10): with outages recorded, a back-entry whose paper time falls outside every
    // one is refused until a reason is given; the reason is kept on the issue and in the audit. Nothing is issued while
    // it is refused.
    [Fact]
    public async Task DowntimeBackEntry_OutsideEveryRecordedOutage_NeedsAReason()
    {
        var s = await SeedAsync("DOWNTIME-OUT");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        var now = _factory.Clock.UtcNow;
        var paperTime = now.AddHours(-3);
        await using (var c = _factory.Create())
        {
            c.DowntimeEpisodes.Add(new DowntimeEpisode
            {
                StartedUtc = now.AddHours(-30), EndedUtc = now.AddHours(-28), Description = "Earlier network outage",
                CreatedBy = "tech-test", CreatedUtc = now
            });

            // The specimen, crossmatch and reservation were in place before the paper issue.
            var specimen = await c.Specimens.SingleAsync(x => x.Id == s.SpecimenId);
            specimen.CollectedUtc = now.AddHours(-6);
            foreach (var xm in await c.Crossmatches.Where(x => x.BloodProductId == s.UnitId).ToListAsync())
            {
                xm.PerformedUtc = now.AddHours(-5);
            }

            (await c.Allocations.SingleAsync(a => a.BloodProductId == s.UnitId)).AllocatedUtc = now.AddHours(-5);
            await c.SaveChangesAsync();
        }

        IssueUnitRequest BackEntry(string? outsideReason = null) => IssueReq(s) with
        {
            IssuedUtc = paperTime,
            LateEntryReason = "Issued on paper during LIS downtime",
            DowntimeBackEntry = true,
            BackEntryDin = s.UnitNumber,
            OutsideOutageReason = outsideReason
        };

        await using (var c = _factory.Create())
        {
            var refused = await Issuing(c, outages: true).IssueUnitAsync(BackEntry("   "));
            Assert.Contains(refused.Evaluation!.HardStops, r => r.Code == DowntimeEpisodeRule.OutsideCode);
            Assert.False(await c.Issues.AnyAsync(i => i.BloodProductId == s.UnitId));
        }

        await using (var c = _factory.Create())
        {
            var entered = await Issuing(c, outages: true).IssueUnitAsync(BackEntry("Paper log from the unplanned outage, not yet recorded"));
            if (entered.RequiresOverride)
            {
                entered = await Issuing(c, outages: true).IssueUnitAsync(BackEntry("Paper log from the unplanned outage, not yet recorded") with
                {
                    OverrideReason = "Issued on paper during downtime", AuthorizedBy = "dr-authorizer"
                });
            }

            Assert.True(entered.Succeeded, entered.Error ?? string.Join("; ", entered.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            Assert.Equal("Paper log from the unplanned outage, not yet recorded", entered.Value!.OutsideOutageReason);
        }

        await using var verify = _factory.Create();
        var audit = await verify.AuditEvents.Where(a => a.EntityType == nameof(BloodUnit) && a.EntityId == s.UnitId
            && a.NewValueJson!.Contains("\"DowntimeBackEntry\":true")).SingleAsync();
        Assert.Contains("Paper log from the unplanned outage", audit.NewValueJson);
    }

    [Fact]
    public async Task Issue_WithoutStatedTime_IsNotALateEntry()
    {
        var s = await SeedAsync("NOW-ISS");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));

        Assert.True(issued.Succeeded, issued.Error);
        Assert.Null(issued.Value!.LateEntryReason);
    }

    [Fact]
    public async Task Issue_WhenNotAllocated_IsHardStopped()
    {
        var s = await SeedAsync("NOALLOC");
        await RecordCompatibleCrossmatchAsync(s);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));

        Assert.False(issued.Succeeded);
        Assert.Contains(issued.Evaluation!.HardStops, r => r.Code == IssueGate.AllocationCode);
    }

    [Fact]
    public async Task Issue_HemolysisAppearance_IsHardStopped()
    {
        var s = await SeedAsync("ISS-HEMOL");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with { Appearance = UnitAppearance.Hemolysis });

        Assert.False(issued.Succeeded);
        Assert.Contains(issued.Evaluation!.HardStops, r => r.Code == IssueAppearanceRule.Code);
        Assert.NotEqual(UnitStatus.Issued, (await c.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    [Fact]
    public async Task Allocate_AboIncompatibleUnit_IsBlocked()
    {
        // Patient O, unit AB -> incompatible for RBC.
        var s = await SeedAsync("INCOMPAT", patientAbo: AboGroup.O, unitAbo: AboGroup.AB);

        await using var c = _factory.Create();
        var allocated = await Compatibility(c).AllocateUnitAsync(new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId));

        Assert.False(allocated.Succeeded);
        Assert.True(allocated.Evaluation!.IsHardStopped);
        Assert.Contains(allocated.Evaluation.HardStops, r => r.Code == AboCompatibilityRule.AboCode);
    }

    private async Task AddVerifiedRetypeAsync(long unitId, string key, bool matchesLabel)
    {
        await using var c = _factory.Create();
        var test = new TestDefinition
        {
            Code = $"RETYPE-{key}", Name = "Product retype", Category = TestCategory.AboRhRetype,
            ResultValueType = ResultValueType.AboRh, IsActive = true, IsDraft = false, Version = 1
        };
        c.TestDefinitions.Add(test);
        await c.SaveChangesAsync();
        c.ProductRetypeResults.Add(new ProductRetypeResult
        {
            BloodProductId = unitId, TestDefinitionId = test.Id, TestCode = test.Code,
            Value = matchesLabel ? "O|Positive" : "A|Positive",
            InterpretedAbo = matchesLabel ? AboGroup.O : AboGroup.A, InterpretedRh = RhType.Positive,
            MatchesLabel = matchesLabel, DiscrepancyDetail = matchesLabel ? null : "Labeled O Positive; interpreted A Positive.",
            Status = ResultStatus.Verified, EnteredBy = "tech1", EnteredUtc = _factory.Clock.UtcNow,
            VerifiedBy = "tech2", VerifiedUtc = _factory.Clock.UtcNow
        });
        await c.SaveChangesAsync();
    }

    // TEST-BB-323: an emergency-released unit that was never retyped goes back to Received, not Available, on return.
    [Fact]
    public async Task Return_OfUnretypedEmergencyUnit_GoesBackToReceived_ForRetype()
    {
        var s = await SeedAsync("RET-UNRETYPED", requiresRetype: true, unitStatus: UnitStatus.Received);
        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.EmergencyRelease, overrideReason: "Emergency before retype", authorizedBy: "dr-authorizer"));
            Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            issueId = issued.Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var returned = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest(
                "Not needed; cooler intact", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
            Assert.True(returned.Succeeded, returned.Error);
            Assert.Contains("\"RetypePending\":true", returned.Value!.ReissueEvaluationJson);
            Assert.True(ReturnDto.From(returned.Value).RetypePending);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Received, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
        var last = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == s.UnitId).OrderByDescending(h => h.Id).FirstAsync();
        Assert.Equal(UnitStatus.Received, last.ToStatus);
        Assert.Contains(RetypeStockStatus.PendingReason, last.Reason);
        Assert.Contains(await new InventoryRepository(verify).ListPendingRetypeAsync(), u => u.Id == s.UnitId);
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EventType == AuditEventType.Return && a.EntityId == s.UnitId
            && a.NewValueJson!.Contains("\"RetypePending\":true")));
    }

    [Fact]
    public async Task Return_OfRetypedUnit_StillGoesToAvailable()
    {
        var s = await SeedAsync("RET-RETYPED", requiresRetype: true, unitStatus: UnitStatus.Available);
        await AddVerifiedRetypeAsync(s.UnitId, "RET-RETYPED", matchesLabel: true);
        await AllocateAsync(s);
        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.EmergencyRelease, overrideReason: "Emergency", authorizedBy: "dr-authorizer"));
            Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            issueId = issued.Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var returned = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest(
                "Not needed", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
            Assert.True(returned.Succeeded, returned.Error);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    // TEST-BB-343: start at the bedside, then complete; the unit shows as transfusing in between.
    private static StartTransfusionRequest StartReq(Scenario s, DateTime? startUtc = null) =>
        new(StartUtc: startUtc, PatientIdentifier1Value: s.Mrn, PatientIdentifier2Value: s.DateOfBirth);

    private async Task<long> IssuedAndReceivedAsync(Scenario s, int issuedHoursAgo = 1)
    {
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with
        {
            IssuedUtc = _factory.Clock.UtcNow.AddHours(-issuedHoursAgo), LateEntryReason = "Charted after the shift"
        });
        Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
        Assert.True((await Issuing(c).RecordWardReceiptAsync(issued.Value!.Id, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
        return issued.Value!.Id;
    }

    // TEST-BB-752: a unit still out carries its patient's reaction investigation while it is open or under review, for
    // the bedside warning; a closed investigation, or another patient's, is not carried.
    [Fact]
    public async Task IssuedList_CarriesThePatientsOpenReactionInvestigation()
    {
        var s = await SeedAsync("TX-REACT-OPEN");
        var other = await SeedAsync("TX-REACT-NONE");
        var issueId = await IssuedAndReceivedAsync(s);
        var otherIssueId = await IssuedAndReceivedAsync(other);
        await using var c = _factory.Create();
        var earlier = new TransfusionEvent
        {
            IssueId = issueId, BloodProductId = s.UnitId, PatientId = s.PatientId, StartUtc = _factory.Clock.UtcNow.AddMinutes(-40),
            ReactionSuspected = true, DocumentedBy = "nurse"
        };
        c.TransfusionEvents.Add(earlier);
        await c.SaveChangesAsync();
        var reported = _factory.Clock.UtcNow.AddMinutes(-10);
        var investigation = new ReactionInvestigation
        {
            TransfusionEventId = earlier.Id, PatientId = s.PatientId, BloodProductId = s.UnitId, ReportedUtc = reported, ReportedBy = "nurse"
        };
        c.ReactionInvestigations.Add(investigation);
        await c.SaveChangesAsync();

        var rows = await Issuing(c).ListOutstandingIssuedAsync();
        var row = Assert.Single(rows, r => r.IssueId == issueId);
        Assert.Equal((investigation.Id, reported), (row.OpenReactionInvestigationId, row.OpenReactionReportedUtc));
        Assert.Null(Assert.Single(rows, r => r.IssueId == otherIssueId).OpenReactionInvestigationId);

        investigation.Status = ReactionInvestigationStatus.UnderReview;
        await c.SaveChangesAsync();
        Assert.Equal(investigation.Id, Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), r => r.IssueId == issueId).OpenReactionInvestigationId);

        investigation.Status = ReactionInvestigationStatus.Closed;
        await c.SaveChangesAsync();
        Assert.Null(Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), r => r.IssueId == issueId).OpenReactionInvestigationId);
    }

    // TEST-BB-754: a reserved unit on Ready to issue carries its patient's open reaction investigation, so the bank is
    // warned before the unit leaves; another patient's reserved unit does not.
    [Fact]
    public async Task ReadyList_CarriesThePatientsOpenReactionInvestigation()
    {
        var s = await SeedAsync("RDY-REACT-OPEN");
        var other = await SeedAsync("RDY-REACT-NONE");
        var issuedId = await IssuedAndReceivedAsync(s);
        var second = await SeedAsync("RDY-REACT-OPEN-2");
        await using (var move = _factory.Create())
        {
            // A second unit reserved for the same patient.
            var unit = (await move.BloodUnits.FindAsync(second.UnitId))!;
            await AllocateAsync(s with { UnitId = unit.Id });
        }
        await AllocateAsync(other);

        await using var c = _factory.Create();
        var earlier = new TransfusionEvent
        {
            IssueId = issuedId, BloodProductId = s.UnitId, PatientId = s.PatientId, StartUtc = _factory.Clock.UtcNow.AddMinutes(-40),
            ReactionSuspected = true, DocumentedBy = "nurse"
        };
        c.TransfusionEvents.Add(earlier);
        await c.SaveChangesAsync();
        var investigation = new ReactionInvestigation
        {
            TransfusionEventId = earlier.Id, PatientId = s.PatientId, BloodProductId = s.UnitId, ReportedUtc = _factory.Clock.UtcNow, ReportedBy = "nurse"
        };
        c.ReactionInvestigations.Add(investigation);
        await c.SaveChangesAsync();

        var rows = await Issuing(c).ListReadyToIssueAllocationsAsync();
        Assert.Equal(investigation.Id, Assert.Single(rows, r => r.PatientId == s.PatientId).OpenReactionInvestigationId);
        Assert.Null(Assert.Single(rows, r => r.PatientId == other.PatientId).OpenReactionInvestigationId);
    }

    // TEST-BB-761: the reaction list counts, for a case not closed, the patient's units issued and reserved; a closed
    // case counts none.
    [Fact]
    public async Task ReactionList_CountsThePatientsUnitsOutOrReserved()
    {
        var s = await SeedAsync("RXN-UNITS-OUT");
        var issuedId = await IssuedAndReceivedAsync(s);
        var second = await SeedAsync("RXN-UNITS-OUT-2");
        await AllocateAsync(s with { UnitId = second.UnitId });

        await using var c = _factory.Create();
        var earlier = new TransfusionEvent
        {
            IssueId = issuedId, BloodProductId = s.UnitId, PatientId = s.PatientId, StartUtc = _factory.Clock.UtcNow.AddMinutes(-40),
            ReactionSuspected = true, DocumentedBy = "nurse"
        };
        c.TransfusionEvents.Add(earlier);
        await c.SaveChangesAsync();
        var investigation = new ReactionInvestigation
        {
            TransfusionEventId = earlier.Id, PatientId = s.PatientId, BloodProductId = s.UnitId, ReportedUtc = _factory.Clock.UtcNow, ReportedBy = "nurse"
        };
        c.ReactionInvestigations.Add(investigation);
        await c.SaveChangesAsync();

        ReactionInvestigationService Reactions() => new(
            new EfRepository<ReactionInvestigation>(c), new EfRepository<TransfusionEvent>(c), new InventoryRepository(c),
            c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            issues: new EfRepository<Issue>(c), allocations: new EfRepository<Allocation>(c));

        // The issued unit (received on the ward, still out) and the reserved one.
        Assert.Equal(2, (await Reactions().ListDtosAsync()).Single(r => r.Id == investigation.Id).PatientUnitsOutOrReserved);

        // TEST-BB-765: the chart's lookup returns the open case with its number and report time, and nothing once closed.
        var forChart = Assert.Single(await Reactions().OpenForPatientAsync(s.PatientId));
        Assert.Equal((investigation.Id, investigation.ReportedUtc), (forChart.Id, forChart.ReportedUtc));

        investigation.Status = ReactionInvestigationStatus.Closed;
        await c.SaveChangesAsync();
        Assert.Equal(0, (await Reactions().ListDtosAsync()).Single(r => r.Id == investigation.Id).PatientUnitsOutOrReserved);
        Assert.Empty(await Reactions().OpenForPatientAsync(s.PatientId));
    }

    // TEST-BB-768: an issue records the patient's reaction investigation open at the time; an issue made before any
    // reaction records none. It is a record, not a block: the second unit is issued.
    [Fact]
    public async Task Issue_RecordsTheReactionUnderInvestigation()
    {
        var s = await SeedAsync("ISS-RXN-OPEN");
        var firstIssueId = await IssuedAndReceivedAsync(s);
        var second = await SeedAsync("ISS-RXN-OPEN-2");

        long investigationId;
        await using (var c = _factory.Create())
        {
            Assert.Null((await c.Issues.SingleAsync(i => i.Id == firstIssueId)).ReactionInvestigationOpenAtIssueId);
            var earlier = new TransfusionEvent
            {
                IssueId = firstIssueId, BloodProductId = s.UnitId, PatientId = s.PatientId, StartUtc = _factory.Clock.UtcNow.AddMinutes(-40),
                ReactionSuspected = true, DocumentedBy = "nurse"
            };
            c.TransfusionEvents.Add(earlier);
            await c.SaveChangesAsync();
            var investigation = new ReactionInvestigation
            {
                TransfusionEventId = earlier.Id, PatientId = s.PatientId, BloodProductId = s.UnitId, ReportedUtc = _factory.Clock.UtcNow, ReportedBy = "nurse"
            };
            c.ReactionInvestigations.Add(investigation);
            await c.SaveChangesAsync();
            investigationId = investigation.Id;
        }

        var secondIssueId = await IssuedAndReceivedAsync(s with { UnitId = second.UnitId });
        await using var verify = _factory.Create();
        Assert.Equal(investigationId, (await verify.Issues.SingleAsync(i => i.Id == secondIssueId)).ReactionInvestigationOpenAtIssueId);

        // TEST-BB-771: the investigation lists that unit as issued while it was open, and not the first one.
        var reactions = new ReactionInvestigationService(
            new EfRepository<ReactionInvestigation>(verify), new EfRepository<TransfusionEvent>(verify), new InventoryRepository(verify),
            verify, _factory.Clock, _factory.CurrentUser, new AuditWriter(verify, _factory.Clock, _factory.CurrentUser),
            issues: new EfRepository<Issue>(verify));
        var listed = Assert.Single((await reactions.GetDtoAsync(investigationId))!.IssuedWhileOpen!);
        Assert.Equal((secondIssueId, second.UnitId), (listed.IssueId, listed.UnitId));
        Assert.Single((await reactions.ListDtosAsync()).Single(r => r.Id == investigationId).IssuedWhileOpen!);
    }

    // TEST-BB-856 (OCD-078, decided 2026-11-10): while a reaction is under investigation, issuing another unit to the
    // patient, and starting it at the bedside, each need an override with the reason and who authorized it; each is
    // recorded as an override for supervisor review; a start missing the authorizer is refused with nothing recorded.
    [Fact]
    public async Task ReactionUnderInvestigation_IssueAndBedsideStart_NeedAnOverride()
    {
        var s = await SeedAsync("RXN-OVR");
        var firstIssueId = await IssuedAndReceivedAsync(s);
        var second = await SeedAsync("RXN-OVR-2");
        var now = _factory.Clock.UtcNow;
        await using (var c = _factory.Create())
        {
            var earlier = new TransfusionEvent
            {
                IssueId = firstIssueId, BloodProductId = s.UnitId, PatientId = s.PatientId, StartUtc = now.AddMinutes(-50),
                ReactionSuspected = true, DocumentedBy = "nurse"
            };
            c.TransfusionEvents.Add(earlier);
            await c.SaveChangesAsync();
            c.ReactionInvestigations.Add(new ReactionInvestigation
            {
                TransfusionEventId = earlier.Id, PatientId = s.PatientId, BloodProductId = s.UnitId, ReportedUtc = now.AddMinutes(-30), ReportedBy = "nurse"
            });
            await c.SaveChangesAsync();
        }

        var next = s with { UnitId = second.UnitId };
        await RecordCompatibleCrossmatchAsync(next);
        await AllocateAsync(next);
        long issueId;
        await using (var c = _factory.Create())
        {
            var asked = await Issuing(c).IssueUnitAsync(IssueReq(next));
            Assert.True(asked.RequiresOverride);
            Assert.Contains(asked.Evaluation!.Warnings, w => w.Code == ReactionUnderInvestigationRule.Code);

            var issued = await Issuing(c).IssueUnitAsync(IssueReq(next) with
            {
                OverrideReason = "Physician reviewed the febrile reaction; continue", AuthorizedBy = "dr-authorizer"
            });
            Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            issueId = issued.Value!.Id;
            Assert.True(await c.Overrides.AnyAsync(o => o.ContextId == second.UnitId && o.RuleCode.Contains(ReactionUnderInvestigationRule.Code)));
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
        }

        await using (var c = _factory.Create())
        {
            var noAuthorizer = await Issuing(c).StartTransfusionAsync(issueId, StartReq(next) with { OverrideReason = "Physician said continue" });
            Assert.False(noAuthorizer.Succeeded);
            Assert.Contains(noAuthorizer.Evaluation!.Warnings, w => w.Code == ReactionUnderInvestigationRule.Code);
            Assert.False(await c.TransfusionEvents.AnyAsync(t => t.IssueId == issueId));

            var started = await Issuing(c).StartTransfusionAsync(issueId, StartReq(next) with
            {
                OverrideReason = "Physician said continue", AuthorizedBy = "dr-authorizer"
            });
            Assert.True(started.Succeeded, started.Error);
            var bedside = await c.Overrides.SingleAsync(o => o.ContextId == second.UnitId && o.RuleCode == ReactionUnderInvestigationRule.Code && o.Reason == "Physician said continue");
            Assert.Equal(("Physician said continue", "dr-authorizer"), (bedside.Reason, bedside.AuthorizedBy));
            Assert.True(await c.AuditEvents.AnyAsync(a => a.EventType == AuditEventType.Override && a.EntityId == second.UnitId
                && a.NewValueJson!.Contains(ReactionUnderInvestigationRule.Code)));
        }
    }

    // TEST-BB-620: a unit still out that was issued under another MRN (the chart's MRN changed since, as after a merge)
    // carries that MRN on the issued list; one issued under the current MRN does not.
    [Fact]
    public async Task IssuedList_NamesAnEarlierMrn_WhenTheChartChangedSinceIssue()
    {
        var s = await SeedAsync("TX-EARLIER-ID");
        var issueId = await IssuedAndReceivedAsync(s);
        await using var c = _factory.Create();
        Assert.Null(Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), r => r.IssueId == issueId).IssuedUnderEarlierMrn);

        var issue = (await c.Issues.FindAsync(issueId))!;
        issue.PatientIdentifier1 = "UNK-20260530-01";
        await c.SaveChangesAsync();
        var row = Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), r => r.IssueId == issueId);
        Assert.Equal("UNK-20260530-01", row.IssuedUnderEarlierMrn);
    }

    [Fact]
    public async Task StartThenComplete_ShowsTransfusing_ThenFinishesTheSameRecord()
    {
        var s = await SeedAsync("TX-STARTCOMP");
        var issueId = await IssuedAndReceivedAsync(s);
        var now = _factory.Clock.UtcNow;

        long transfusionId;
        await using (var c = _factory.Create())
        {
            var started = await Issuing(c).StartTransfusionAsync(issueId, StartReq(s, now.AddMinutes(-40)));
            Assert.True(started.Succeeded, started.Error ?? string.Join("; ", started.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            transfusionId = started.Value!.Id;
            Assert.Null(started.Value.StopUtc);
            Assert.Equal(UnitStatus.TransfusionStarted, (await c.BloodUnits.FindAsync(s.UnitId))!.Status);
            Assert.Equal(IssueStatus.Issued, (await c.Issues.FindAsync(issueId))!.Status);

            var row = Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), r => r.IssueId == issueId);
            Assert.Equal(now.AddMinutes(-40), row.TransfusionStartedUtc);

            // While it runs: no second start, no one-step documentation, no return.
            Assert.False((await Issuing(c).StartTransfusionAsync(issueId, StartReq(s))).Succeeded);
            var again = await Issuing(c).DocumentTransfusionAsync(issueId, TxReq(s));
            Assert.False(again.Succeeded);
            Assert.Contains("Complete it", again.Error);
            Assert.False((await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest(
                "Not needed", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true))).Succeeded);
        }

        await using (var c = _factory.Create())
        {
            var completed = await Issuing(c).CompleteTransfusionAsync(issueId, new CompleteTransfusionRequest(
                TransfusionDisposition.Completed, StopUtc: now.AddMinutes(-5), VolumeTransfused: 280m, ReactionSuspected: true));
            Assert.True(completed.Succeeded, completed.Error ?? string.Join("; ", completed.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            Assert.Equal(transfusionId, completed.Value!.Id);
        }

        await using var verify = _factory.Create();
        var record = await verify.TransfusionEvents.SingleAsync(t => t.IssueId == issueId);
        Assert.Equal(now.AddMinutes(-40), record.StartUtc);
        Assert.Equal(now.AddMinutes(-5), record.StopUtc);
        Assert.Equal(280m, record.VolumeTransfused);
        // The suspected reaction then quarantines the remainder (existing reaction behavior).
        Assert.Contains(await verify.InventoryStatusHistory.Where(h => h.BloodProductId == s.UnitId).ToListAsync(),
            h => h.FromStatus == UnitStatus.TransfusionStarted && h.ToStatus == UnitStatus.Transfused);
        Assert.Equal(IssueStatus.Transfused, (await verify.Issues.FindAsync(issueId))!.Status);
        Assert.True(await verify.ReactionInvestigations.AnyAsync(r => r.PatientId == s.PatientId));
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EntityType == nameof(BloodUnit) && a.EntityId == s.UnitId && a.Reason == "Transfusion started."));
    }

    // TEST-BB-483: a transfusion whose completion is overdue leads the Issued units list; the rest keep issue order.
    [Fact]
    public async Task CompletionOverdue_LeadsTheIssuedList()
    {
        var earlier = await SeedAsync("TX-ORDER-A");
        var earlierIssue = await IssuedAndReceivedAsync(earlier, issuedHoursAgo: 10);
        var later = await SeedAsync("TX-ORDER-B");
        var laterIssue = await IssuedAndReceivedAsync(later, issuedHoursAgo: 9);
        var now = _factory.Clock.UtcNow;
        await using var c = _factory.Create();
        var started = await Issuing(c).StartTransfusionAsync(laterIssue, StartReq(later, now.AddHours(-8)));
        Assert.True(started.Succeeded, started.Error ?? string.Join("; ", started.Evaluation?.HardStops.Select(r => r.Message) ?? []));

        var rows = (await Issuing(c).ListOutstandingIssuedAsync()).ToList();
        var overdue = rows.FindIndex(r => r.IssueId == laterIssue);
        var waiting = rows.FindIndex(r => r.IssueId == earlierIssue);
        Assert.True(rows[overdue].TransfusionCompletionOverdue);
        Assert.False(rows[waiting].TransfusionCompletionOverdue);
        Assert.True(overdue < waiting);
        Assert.Equal("1 transfusion completion overdue", DashboardQueueCatalog.OutstandingNote(rows));
    }

    [Fact]
    public async Task Start_WithoutIdentifiers_IsRefused_AndRecordsNothing()
    {
        var s = await SeedAsync("TX-STARTNOID");
        var issueId = await IssuedAndReceivedAsync(s);
        await using var c = _factory.Create();
        var started = await Issuing(c).StartTransfusionAsync(issueId, new StartTransfusionRequest());
        Assert.False(started.Succeeded);
        Assert.Equal(UnitStatus.Issued, (await c.BloodUnits.FindAsync(s.UnitId))!.Status);
        Assert.False(await c.TransfusionEvents.AnyAsync(t => t.IssueId == issueId));
    }

    [Fact]
    public async Task Complete_WithoutStart_OrWithStopBeforeStart_IsRefused()
    {
        var s = await SeedAsync("TX-COMPBAD");
        var issueId = await IssuedAndReceivedAsync(s);
        var now = _factory.Clock.UtcNow;
        await using var c = _factory.Create();
        var notStarted = await Issuing(c).CompleteTransfusionAsync(issueId, new CompleteTransfusionRequest(TransfusionDisposition.Completed, ReactionSuspected: false));
        Assert.False(notStarted.Succeeded);
        Assert.Contains("No started transfusion", notStarted.Error);

        Assert.True((await Issuing(c).StartTransfusionAsync(issueId, StartReq(s, now.AddMinutes(-10)))).Succeeded);
        var backwards = await Issuing(c).CompleteTransfusionAsync(issueId, new CompleteTransfusionRequest(
            TransfusionDisposition.Completed, StopUtc: now.AddMinutes(-30), ReactionSuspected: false));
        Assert.Contains(backwards.Evaluation!.HardStops, r => r.Code == TransfusionDocumentationRule.Code);
        Assert.Equal(UnitStatus.TransfusionStarted, (await c.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    [Fact]
    public async Task FinishedRecordOnAnIssuedIssue_IsNotShownAsStarted_AndCannotBeStartedAgain()
    {
        var s = await SeedAsync("TX-LEGACY");
        var issueId = await IssuedAndReceivedAsync(s);
        var now = _factory.Clock.UtcNow;
        await using var c = _factory.Create();
        c.TransfusionEvents.Add(new TransfusionEvent
        {
            IssueId = issueId, BloodProductId = s.UnitId, PatientId = s.PatientId,
            StartUtc = now.AddMinutes(-30), StopUtc = now.AddMinutes(-10), DocumentedBy = "legacy"
        });
        await c.SaveChangesAsync();

        var row = Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), r => r.IssueId == issueId);
        Assert.Null(row.TransfusionStartedUtc);
        var again = await Issuing(c).StartTransfusionAsync(issueId, StartReq(s));
        Assert.False(again.Succeeded);
        Assert.Contains("already has a transfusion record", again.Error);
    }

    // TEST-BB-354: the shift handoff counts running transfusions and incompatible retrospective crossmatches.
    [Fact]
    public async Task ShiftHandoff_CountsRunningTransfusions_AndIncompatibleRetrospectiveCrossmatches()
    {
        var running = await SeedAsync("HO-RUN");
        var runningIssue = await IssuedAndReceivedAsync(running, issuedHoursAgo: 8);
        var emergency = await SeedAsync("HO-RETRO");
        await AllocateAsync(emergency);

        ShiftHandoff before;
        await using (var c = _factory.Create())
        {
            before = (await Handoff(c).CreateAsync(new CreateShiftHandoffRequest("Before"))).Value!;
            Assert.True((await Issuing(c).StartTransfusionAsync(runningIssue, StartReq(running, _factory.Clock.UtcNow.AddHours(-7)))).Succeeded);
            Assert.True((await Issuing(c).IssueUnitAsync(IssueReq(
                emergency, IssueType.EmergencyRelease, overrideReason: "Uncrossmatched release", authorizedBy: "dr-authorizer"))).Succeeded);
            Assert.True((await Compatibility(c).RecordCrossmatchAsync(new RecordCrossmatchRequest(
                emergency.UnitId, emergency.PatientId, emergency.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Incompatible))).Succeeded);
        }

        await using var v = _factory.Create();
        var after = await Handoff(v).CreateAsync(new CreateShiftHandoffRequest("After"));
        Assert.True(after.Succeeded, after.Error);
        Assert.Equal(before.TransfusionsRunning + 1, after.Value!.TransfusionsRunning);
        Assert.Equal(before.TransfusionsCompletionOverdue + 1, after.Value.TransfusionsCompletionOverdue);
        Assert.Equal(before.RetrospectiveIncompatible + 1, after.Value.RetrospectiveIncompatible);
        Assert.Equal(after.Value.RetrospectiveIncompatible, ShiftHandoffDto.From(after.Value).RetrospectiveIncompatible);
    }

    private ShiftHandoffService Handoff(BloodBankDbContext c) =>
        new(new EfRepository<ShiftHandoff>(c), c, _factory.Clock, _factory.CurrentUser,
            issuing: Issuing(c), audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.ShiftHandoff));

    [Fact]
    public async Task StartOnlyPermission_CanStart_ButNotComplete()
    {
        var s = await SeedAsync("TX-STARTPERM");
        var issueId = await IssuedAndReceivedAsync(s);
        var startOnly = new FixedPermissionEvaluator(1, PermissionCodes.TransfusionStart);
        await using var c = _factory.Create();
        Assert.True((await Issuing(c, startOnly).StartTransfusionAsync(issueId, StartReq(s))).Succeeded);
        var complete = await Issuing(c, startOnly).CompleteTransfusionAsync(issueId, new CompleteTransfusionRequest(TransfusionDisposition.Completed, ReactionSuspected: false));
        Assert.False(complete.Succeeded);
        Assert.Contains(complete.Evaluation!.HardStops, r => r.Message.Contains("transfusion.complete"));
    }

    // TEST-BB-342: a transfusion is judged against the unit's life at its start time, not when it is charted.
    private async Task<long> IssuedThreeHoursAgoAsync(Scenario s)
    {
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with
        {
            IssuedUtc = _factory.Clock.UtcNow.AddHours(-3),
            LateEntryReason = "Charted after the shift"
        });
        Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
        Assert.True((await Issuing(c).RecordWardReceiptAsync(issued.Value!.Id, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
        return issued.Value!.Id;
    }

    private async Task ExpireUnitAsync(long unitId, DateTime expiresUtc)
    {
        await using var c = _factory.Create();
        await c.BloodUnits.Where(u => u.Id == unitId).ExecuteUpdateAsync(u => u.SetProperty(x => x.ExpiresUtc, expiresUtc));
    }

    [Fact]
    public async Task DocumentTransfusion_UnitExpiredAfterItStarted_CanBeDocumented()
    {
        var s = await SeedAsync("TX-LATECHART");
        var issueId = await IssuedThreeHoursAgoAsync(s);
        var now = _factory.Clock.UtcNow;
        await ExpireUnitAsync(s.UnitId, now.AddMinutes(-30));

        await using var c = _factory.Create();
        var documented = await Issuing(c).DocumentTransfusionAsync(issueId, TxReq(s) with
        {
            StartUtc = now.AddHours(-2),
            StopUtc = now.AddHours(-1)
        });
        Assert.True(documented.Succeeded, documented.Error ?? string.Join("; ", documented.Evaluation?.HardStops.Select(r => r.Message) ?? []));
        Assert.Equal(UnitStatus.Transfused, (await c.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    [Fact]
    public async Task DocumentTransfusion_StartedAfterExpiry_IsRefused()
    {
        var s = await SeedAsync("TX-STARTEXP");
        var issueId = await IssuedThreeHoursAgoAsync(s);
        var now = _factory.Clock.UtcNow;
        await ExpireUnitAsync(s.UnitId, now.AddMinutes(-30));

        await using var c = _factory.Create();
        var documented = await Issuing(c).DocumentTransfusionAsync(issueId, TxReq(s) with { StartUtc = now.AddMinutes(-10) });
        Assert.False(documented.Succeeded);
        Assert.Contains(documented.Evaluation!.HardStops, r => r.Message.Contains("had expired") && r.Message.Contains("before the transfusion started"));
        Assert.Equal(UnitStatus.Issued, (await c.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    [Fact]
    public async Task DocumentTransfusion_StartBeforeIssue_IsRefused()
    {
        var s = await SeedAsync("TX-BEFOREISS");
        var issueId = await IssuedThreeHoursAgoAsync(s);

        await using var c = _factory.Create();
        var documented = await Issuing(c).DocumentTransfusionAsync(issueId, TxReq(s) with { StartUtc = _factory.Clock.UtcNow.AddHours(-4) });
        Assert.False(documented.Succeeded);
        Assert.Contains(documented.Evaluation!.HardStops, r => r.Code == TransfusionDocumentationRule.Code && r.Message.Contains("before the unit was issued"));
        Assert.False(await c.TransfusionEvents.AnyAsync(t => t.IssueId == issueId));
    }

    // TEST-BB-341: a unit returned to stock offers one step to reserve it again for the same patient,
    // through the normal reservation (nothing reserved automatically), and can then be reissued.
    [Fact]
    public async Task Return_ToStock_OffersReserveAgain_ThenReissues()
    {
        var s = await SeedAsync("RET-AGAIN");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var returned = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest(
                "Not needed yet", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
            Assert.True(returned.Succeeded, returned.Error);
            var dto = ReturnDto.From(returned.Value!);
            Assert.True(dto.BackInStock);
            Assert.Equal(s.PatientId, dto.PatientId);
            Assert.Equal($"/compatibility?patientId={s.PatientId}&bloodUnitId={s.UnitId}&from=return", dto.ReserveAgainHref);
            Assert.False(await c.Allocations.AnyAsync(a => a.BloodProductId == s.UnitId && a.Status == AllocationStatus.Reserved));
        }

        await AllocateAsync(s);
        await using (var c = _factory.Create())
        {
            var reissued = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.True(reissued.Succeeded, reissued.Error ?? string.Join("; ", reissued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
        }

        await using var verify = _factory.Create();
        Assert.Equal(2, await verify.Allocations.CountAsync(a => a.BloodProductId == s.UnitId && a.Status == AllocationStatus.Consumed));
    }

    [Fact]
    public async Task Return_ToQuarantine_OffersNoReserveAgain()
    {
        var s = await SeedAsync("RET-NOAGAIN");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
        }

        await using var c2 = _factory.Create();
        var returned = await Issuing(c2).ReturnUnitAsync(issueId, new ReturnUnitRequest(
            "Seal damaged", TemperatureAcceptable: true, SealIntegrityAcceptable: false, VisualInspectionAcceptable: true,
            Destination: UnitStatus.Quarantine));
        Assert.True(returned.Succeeded, returned.Error);
        var dto = ReturnDto.From(returned.Value!);
        Assert.False(dto.BackInStock);
        Assert.Null(dto.ReserveAgainHref);
    }

    // TEST-BB-323: a unit whose verified retype disagrees with its label cannot leave on any issue type.
    [Fact]
    public async Task EmergencyRelease_OfUnitWithVerifiedRetypeMismatch_IsHardStopped()
    {
        var s = await SeedAsync("RET-MISMATCH", requiresRetype: true, unitStatus: UnitStatus.Received);
        await AddVerifiedRetypeAsync(s.UnitId, "RET-MISMATCH", matchesLabel: false);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.EmergencyRelease, overrideReason: "Emergency", authorizedBy: "dr-authorizer"));

        Assert.False(issued.Succeeded);
        Assert.Contains(issued.Evaluation!.HardStops, r => r.Code == IssueGate.UnitRetypeMismatchCode);
        Assert.Equal(UnitStatus.Received, (await c.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    private MtpActivationService Mtp(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<MtpActivation>(c), new EfRepository<Patient>(c), new EfRepository<Issue>(c),
            new EfRepository<BloodUnit>(c), new EfRepository<ProductType>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser), permissions ?? new FixedPermissionEvaluator(3),
            new EfRepository<MtpPackDefinition>(c));

    private async Task<long> ActivateMtpAsync(long patientId)
    {
        await using var c = _factory.Create();
        var activated = await Mtp(c).ActivateAsync(patientId, new ActivateMtpRequest("Dr. Rivera, trauma", "Hemorrhagic shock", "ED trauma 1"));
        Assert.True(activated.Succeeded, activated.Error ?? string.Join("; ", activated.Evaluation?.HardStops.Select(r => r.Message) ?? []));
        return activated.Value!.Id;
    }

    private MtpPackAdminService Packs(BloodBankDbContext c)
    {
        var env = new BloodBankLIS.Infrastructure.Common.StaticEnvironmentInfo("Development", isDevMode: false);
        return new(new EfRepository<MtpPackDefinition>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env));
    }

    private TransportContainerAdminService Containers(BloodBankDbContext c)
    {
        var env = new BloodBankLIS.Infrastructure.Common.StaticEnvironmentInfo("Development", isDevMode: false);
        return new(new EfRepository<TransportContainer>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env));
    }

    private MtpPackingService Packing(BloodBankDbContext c, IPermissionEvaluator? permissions = null, IFacilityTimeZone? timeZone = null) =>
        new(new EfRepository<MtpPackRecord>(c), new EfRepository<MtpActivation>(c), new EfRepository<TransportContainer>(c),
            new EfRepository<Issue>(c), new EfRepository<BloodUnit>(c), new EfRepository<ProductType>(c), new EfRepository<Patient>(c),
            new EfRepository<MtpPackDefinition>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser), permissions ?? new FixedPermissionEvaluator(3), timeZone);

    // TEST-BB-328: the pack report prints the facility's local time and names the zone; the UTC record is unchanged.
    [Fact]
    public async Task MtpPackReport_PrintsLocalTime_AndNamesTheZone()
    {
        var (_, activationId, issueId, containerId) = await IssuedUnderMtpAsync("MTP-LOCAL", "COOL-T");
        var chicago = BloodBankLIS.Infrastructure.Common.FacilityTimeZone.FromId("America/Chicago");
        await using var c = _factory.Create();
        var packed = await Packing(c).PackAsync(activationId, new PackMtpRequest(containerId, null, 4m, [issueId]));
        Assert.True(packed.Succeeded, packed.Error);

        var report = await Packing(c, timeZone: chicago).GetReportAsync(packed.Value!.Id);

        Assert.Equal(chicago.Label, report!.TimeZoneLabel);
        Assert.Equal(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(report.PackedUtc, DateTimeKind.Utc), chicago.Zone), report.PackedLocal);
        Assert.Equal(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(report.ActivatedUtc, DateTimeKind.Utc), chicago.Zone), report.ActivatedLocal);
        var unit = Assert.Single(report.Units);
        Assert.Equal(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(unit.ExpiresUtc, DateTimeKind.Utc), chicago.Zone), unit.ExpiresLocal);
        Assert.NotEqual(report.PackedUtc, report.PackedLocal);
    }

    /// <summary>Activates MTP for a seeded patient and issues its unit under it; returns (activation, issue, container).</summary>
    private async Task<(Scenario S, long ActivationId, long IssueId, long ContainerId)> IssuedUnderMtpAsync(string key, string containerCode)
    {
        var s = await SeedAsync(key);
        await AllocateAsync(s);
        var activationId = await ActivateMtpAsync(s.PatientId);
        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s, IssueType.MassiveTransfusion, overrideReason: "MTP", authorizedBy: "dr-authorizer"));
        Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
        var container = await Containers(c).CreateAsync(new SaveTransportContainerRequest(
            containerCode, "Large cooler", TransportContainerType.Cooler, null, 1m, 10m, 4, 10));
        Assert.True(container.Succeeded, container.Error);
        return (s, activationId, issued.Value!.Id, container.Value!.Id);
    }

    // TEST-BB-411: a transport container's validated range and hours change only with a reason.
    [Fact]
    public async Task TransportContainer_Update_And_Deactivate_NeedAReason()
    {
        await using var c = _factory.Create();
        var created = await Containers(c).CreateAsync(new SaveTransportContainerRequest(
            "COOL-RSN", "Reason cooler", TransportContainerType.Cooler, null, 1m, 10m, 4, 10));
        Assert.True(created.Succeeded, created.Error);

        var change = new SaveTransportContainerRequest("COOL-RSN", "Reason cooler", TransportContainerType.Cooler, null, 1m, 6m, 6, 10);
        var refused = await Containers(c).UpdateAsync(created.Value!.Id, change);
        Assert.Contains(refused.Evaluation!.HardStops, r => r.Code == ValidatedConfigChangeReasonRule.Code);
        Assert.Equal(10m, (await c.TransportContainers.AsNoTracking().SingleAsync(x => x.Id == created.Value.Id)).MaxTemperatureC);

        var updated = await Containers(c).UpdateAsync(created.Value.Id, change with { Reason = "Revalidated 2026-11-04, report QA-17" });
        Assert.True(updated.Succeeded, updated.Error);
        Assert.True(await c.ConfigurationChangeHistory.AnyAsync(h => h.EntityId == created.Value.Id && h.ChangeReason == "Revalidated 2026-11-04, report QA-17"));

        Assert.False((await Containers(c).SetActiveAsync(created.Value.Id, false)).Succeeded);
        Assert.True((await Containers(c).SetActiveAsync(created.Value.Id, false, "Cooler lid cracked, retired")).Succeeded);
    }

    // TEST-BB-326: a pack records container, temperature, packer, and units; its report carries everything printed.
    [Fact]
    public async Task MtpPack_IsRecorded_Audited_AndReported()
    {
        var (s, activationId, issueId, containerId) = await IssuedUnderMtpAsync("MTP-COOL", "COOL-A");

        long packId;
        await using (var c = _factory.Create())
        {
            var packed = await Packing(c).PackAsync(activationId, new PackMtpRequest(containerId, "Cooler 3", 4.5m, [issueId], "To trauma bay"));
            Assert.True(packed.Succeeded, packed.Error ?? string.Join("; ", packed.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            Assert.Equal(1, packed.Value!.PackNumber);
            Assert.Equal(1, packed.Value.UnitCount);
            Assert.False(packed.Value.TemperatureOutsideRange);
            packId = packed.Value.Id;
        }

        await using (var c = _factory.Create())
        {
            Assert.Equal(packId, (await c.Issues.FindAsync(issueId))!.MtpPackRecordId);
            var audit = await c.AuditEvents.SingleAsync(a => a.EventType == AuditEventType.MassiveTransfusionProtocol
                && a.EntityType == nameof(MtpPackRecord) && a.EntityId == packId);
            Assert.Contains("Packed", audit.NewValueJson);
            Assert.Contains("U-MTP-COOL", audit.NewValueJson);

            var report = await Packing(c).GetReportAsync(packId);
            Assert.NotNull(report);
            Assert.Equal(1, report.PackNumber);
            Assert.Equal(activationId, report.MtpActivationId);
            Assert.Equal("MRN-MTP-COOL", report.MedicalRecordNumber);
            Assert.Equal("Dr. Rivera, trauma", report.RequestedBy);
            Assert.Equal("COOL-A", report.ContainerCode);
            Assert.Equal("Cooler 3", report.ContainerLabel);
            Assert.Equal("1.0 to 10.0 °C", report.ContainerRange);
            Assert.Equal(4.5m, report.TemperatureC);
            Assert.Equal(_factory.CurrentUser.UserName, report.PackedBy);
            var unit = Assert.Single(report.Units);
            Assert.Equal("U-MTP-COOL", unit.UnitNumber);
            Assert.Equal("RBC-MTP-COOL", unit.ProductCode);
            Assert.Equal("Test RBC", unit.ProductName);
            Assert.Equal("O Pos", unit.BloodType);
            Assert.True(unit.ExpiresUtc > _factory.Clock.UtcNow);
            Assert.True(unit.Uncrossmatched);
            Assert.Equal("Uncrossmatched", unit.Crossmatch);
            Assert.Contains(report.Disclaimers, l => l.Contains("Uncrossmatched"));
            Assert.Contains(report.Disclaimers, l => l.Contains("up to 4 hours"));

            Assert.True(await Packing(c).RecordPrintedAsync(packId));
            Assert.True(await c.AuditEvents.AnyAsync(a => a.EventType == AuditEventType.Print && a.EntityType == nameof(MtpPackRecord) && a.EntityId == packId));
            Assert.Equal(1, Assert.Single(await Packing(c).ListForActivationAsync(activationId)).UnitCount);
        }
    }

    [Fact]
    public async Task MtpPack_OutOfRangeTemperature_IsRecordedAndFlagged()
    {
        var (_, activationId, issueId, containerId) = await IssuedUnderMtpAsync("MTP-WARM", "COOL-W");
        await using var c = _factory.Create();

        var packed = await Packing(c).PackAsync(activationId, new PackMtpRequest(containerId, null, 12.0m, [issueId]));

        Assert.True(packed.Succeeded, packed.Error);
        Assert.True(packed.Value!.TemperatureOutsideRange);
        Assert.Contains(packed.Evaluation!.Warnings, r => r.Code == TransportContainerRule.PackCode);
        var report = await Packing(c).GetReportAsync(packed.Value.Id);
        Assert.True(report!.TemperatureOutsideRange);
        Assert.Contains(report.Disclaimers, l => l.Contains("outside the range"));
    }

    [Fact]
    public async Task MtpPack_Refusals_WriteNothing()
    {
        var (s, activationId, issueId, containerId) = await IssuedUnderMtpAsync("MTP-NOPK", "COOL-N");
        await using var c = _factory.Create();

        var noTemperature = await Packing(c).PackAsync(activationId, new PackMtpRequest(containerId, null, null, [issueId]));
        Assert.True(noTemperature.Evaluation!.IsHardStopped);

        var notOurs = await Packing(c).PackAsync(activationId, new PackMtpRequest(containerId, null, 4m, [issueId, 999_999]));
        Assert.Contains(notOurs.Evaluation!.HardStops, r => r.Message.Contains("issue 999999"));

        var noPermission = await Packing(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueReturn))
            .PackAsync(activationId, new PackMtpRequest(containerId, null, 4m, [issueId]));
        Assert.True(noPermission.Evaluation!.IsHardStopped);

        Assert.False(await c.MtpPackRecords.AnyAsync(p => p.MtpActivationId == activationId));
        Assert.Null((await c.Issues.FindAsync(issueId))!.MtpPackRecordId);

        // A unit can be in only one pack, and a closed protocol takes no more packs.
        Assert.True((await Packing(c).PackAsync(activationId, new PackMtpRequest(containerId, null, 4m, [issueId]))).Succeeded);
        var twice = await Packing(c).PackAsync(activationId, new PackMtpRequest(containerId, null, 4m, [issueId]));
        Assert.Contains(twice.Evaluation!.HardStops, r => r.Message.Contains("U-MTP-NOPK"));
        Assert.True((await Mtp(c).CloseAsync(activationId, new CloseMtpRequest("Bleeding controlled"))).Succeeded);
        var closed = await Packing(c).PackAsync(activationId, new PackMtpRequest(containerId, null, 4m, [issueId]));
        Assert.Contains(closed.Evaluation!.HardStops, r => r.Message.Contains("closed"));
        Assert.Equal(1, await c.MtpPackRecords.CountAsync(p => p.MtpActivationId == activationId));
    }

    // TEST-BB-325: a facility pack chosen at activation gives round guidance on the board; it never blocks an issue.
    [Fact]
    public async Task MtpPack_ChosenAtActivation_ShowsRoundProgress_AndIgnoresReturnedUnits()
    {
        var s = await SeedAsync("MTP-PACK");
        await AllocateAsync(s);
        long packId;
        await using (var c = _factory.Create())
        {
            var pack = await Packs(c).CreateAsync(new SaveMtpPackRequest("pk-a", "Adult pack", 2, 2, 0, 0, 0, 10));
            Assert.True(pack.Succeeded, pack.Error);
            Assert.Equal("PK-A", pack.Value!.Code);
            Assert.Equal("2 red cells, 2 plasma", pack.Value.Summary);
            Assert.True(await c.ConfigurationChangeHistory.AnyAsync(h => h.EntityType == nameof(MtpPackDefinition) && h.EntityId == pack.Value.Id));
            packId = pack.Value.Id;

            var activated = await Mtp(c).ActivateAsync(s.PatientId, new ActivateMtpRequest("Dr. Rivera", "Shock", null, packId));
            Assert.True(activated.Succeeded, activated.Error);
            Assert.Equal(1, activated.Value!.Pack!.CurrentRound);
        }

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s, IssueType.MassiveTransfusion, overrideReason: "MTP", authorizedBy: "dr-authorizer"));
            Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            issueId = issued.Value!.Id;
            var board = Assert.Single(await Mtp(c).ListActiveAsync(), a => a.PatientId == s.PatientId);
            Assert.Equal("PK-A", board.Pack!.Code);
            Assert.Equal(1, Assert.Single(board.Pack.Components, x => x.ComponentClass == ComponentClass.RedBloodCells).InCurrentRound);
            Assert.Equal(0, Assert.Single(board.Pack.Components, x => x.ComponentClass == ComponentClass.Plasma).InCurrentRound);
        }

        await using (var c = _factory.Create())
        {
            var returned = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest(
                "Not needed", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
            Assert.True(returned.Succeeded, returned.Error);
            var board = Assert.Single(await Mtp(c).ListActiveAsync(), a => a.PatientId == s.PatientId);
            Assert.Equal(0, Assert.Single(board.Pack!.Components, x => x.ComponentClass == ComponentClass.RedBloodCells).InCurrentRound);
            Assert.Equal(1, Assert.Single(board.Counts).Returned);
        }
    }

    [Fact]
    public async Task MtpActivation_WithInactiveOrUnknownPack_IsRefused()
    {
        var s = await SeedAsync("MTP-PACK-OFF");
        await using var c = _factory.Create();
        var pack = await Packs(c).CreateAsync(new SaveMtpPackRequest("PK-OFF", "Retired pack", 4, 4, 1, 0, 0, 10));
        // TEST-BB-411: switching a pack off says why.
        var noReason = await Packs(c).SetActiveAsync(pack.Value!.Id, false);
        Assert.Contains(noReason.Evaluation!.HardStops, r => r.Code == ValidatedConfigChangeReasonRule.Code);
        Assert.True((await Packs(c).SetActiveAsync(pack.Value!.Id, false, "Pack retired by medical director 2026-11-04")).Succeeded);
        Assert.True(await c.ConfigurationChangeHistory.AnyAsync(h => h.EntityId == pack.Value.Id
            && h.ChangeReason == "Pack retired by medical director 2026-11-04"));

        foreach (var id in new long[] { pack.Value.Id, 999_999 })
        {
            var refused = await Mtp(c).ActivateAsync(s.PatientId, new ActivateMtpRequest("Dr. Rivera", "Shock", null, id));
            Assert.Contains(refused.Evaluation!.HardStops, r => r.Code == MtpPackRule.Code);
        }

        Assert.False(await c.MtpActivations.AnyAsync(a => a.PatientId == s.PatientId));
        Assert.DoesNotContain(await Packs(c).ListActiveAsync(), p => p.Id == pack.Value.Id);
    }

    [Fact]
    public async Task MtpPack_InvalidContents_AreRefused_AndNoneAreShipped()
    {
        await using var c = _factory.Create();
        var empty = await Packs(c).CreateAsync(new SaveMtpPackRequest("PK-ZERO", "Nothing", 0, 0, 0, 0, 0, 10));
        Assert.Contains(empty.Evaluation!.HardStops, r => r.Code == MtpPackRule.Code);
        Assert.False(await c.MtpPackDefinitions.AnyAsync(p => p.Code == "PK-ZERO"));
    }

    // TEST-BB-324: a massive-transfusion issue needs an active protocol activation for the patient.
    [Fact]
    public async Task MassiveTransfusionIssue_WithoutActivation_IsRefused()
    {
        var s = await SeedAsync("MTP-NONE");
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.MassiveTransfusion, overrideReason: "MTP", authorizedBy: "dr-authorizer"));

        Assert.False(issued.Succeeded);
        Assert.Contains(issued.Evaluation!.HardStops, r => r.Code == MtpActivationRule.NotActiveCode);
        Assert.False(await c.Issues.AnyAsync(i => i.BloodProductId == s.UnitId));
    }

    [Fact]
    public async Task MassiveTransfusionIssue_LinksToActivation_AndCountsOnTheBoard()
    {
        var s = await SeedAsync("MTP-LINK");
        await AllocateAsync(s);
        var activationId = await ActivateMtpAsync(s.PatientId);

        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.MassiveTransfusion, overrideReason: $"MTP #{activationId}: Hemorrhagic shock", authorizedBy: "dr-authorizer"));
            Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            Assert.Equal(activationId, issued.Value!.MtpActivationId);
        }

        await using var verify = _factory.Create();
        var board = Assert.Single(await Mtp(verify).ListActiveAsync(), a => a.Id == activationId);
        var count = Assert.Single(board.Counts);
        Assert.Equal(ComponentClass.RedBloodCells, count.ComponentClass);
        Assert.Equal(1, count.Issued);
        Assert.Equal("U-MTP-LINK", Assert.Single(board.Units).UnitNumber);
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EntityType == nameof(BloodUnit) && a.EntityId == s.UnitId
            && a.NewValueJson!.Contains($"\"MtpActivationId\":{activationId}")));
    }

    // TEST-BB-338: an MTP issue's override is recorded as a massive-transfusion release, not a warning override.
    [Fact]
    public async Task MassiveTransfusionIssue_RecordsAMassiveTransfusionOverride_AndQueueNamesIt()
    {
        var s = await SeedAsync("MTP-ACTION");
        await AllocateAsync(s);
        await ActivateMtpAsync(s.PatientId);
        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.MassiveTransfusion, overrideReason: "MTP: hemorrhage", authorizedBy: "dr-authorizer"));
        Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
        Assert.NotNull(issued.Value!.OverrideId);

        var row = await c.Overrides.AsNoTracking().SingleAsync(o => o.Id == issued.Value.OverrideId);
        Assert.Equal(OverrideAction.MassiveTransfusion, row.Action);
        Assert.Equal("Massive transfusion release", OverrideActionRule.Describe(row.Action));
    }

    [Fact]
    public async Task EmergencyRelease_DoesNotNeedOrLinkAnActivation()
    {
        var s = await SeedAsync("MTP-EMERG");
        await AllocateAsync(s);
        await ActivateMtpAsync(s.PatientId);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.EmergencyRelease, overrideReason: "Single uncrossmatched unit", authorizedBy: "dr-authorizer"));

        Assert.True(issued.Succeeded, issued.Error);
        Assert.Null(issued.Value!.MtpActivationId);
    }

    [Fact]
    public async Task MtpActivation_OncePerPatient_ClosedWithReason_AndAudited()
    {
        var s = await SeedAsync("MTP-LIFE");
        var activationId = await ActivateMtpAsync(s.PatientId);

        await using var c = _factory.Create();
        var again = await Mtp(c).ActivateAsync(s.PatientId, new ActivateMtpRequest("Dr. Lee", "Still bleeding"));
        Assert.Contains(again.Evaluation!.HardStops, r => r.Code == MtpActivationRule.AlreadyActiveCode);

        var noReason = await Mtp(c).CloseAsync(activationId, new CloseMtpRequest(" "));
        Assert.Contains(noReason.Evaluation!.HardStops, r => r.Code == MtpActivationRule.CloseCode);

        var closed = await Mtp(c).CloseAsync(activationId, new CloseMtpRequest("Bleeding controlled"));
        Assert.True(closed.Succeeded, closed.Error);
        Assert.Equal(MtpActivationStatus.Closed, closed.Value!.Status);
        Assert.Equal("Bleeding controlled", closed.Value.CloseReason);
        Assert.DoesNotContain(await Mtp(c).ListActiveAsync(), a => a.Id == activationId);

        var twice = await Mtp(c).CloseAsync(activationId, new CloseMtpRequest("Again"));
        Assert.True(twice.Evaluation!.IsHardStopped);

        var events = await c.AuditEvents.Where(a => a.EventType == AuditEventType.MassiveTransfusionProtocol && a.EntityId == activationId).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.NewValueJson!.Contains("Activated") && e.Reason == "Hemorrhagic shock");
        Assert.Contains(events, e => e.NewValueJson!.Contains("Closed") && e.Reason == "Bleeding controlled");

        // After closing, a new protocol can be activated, and a new MTP issue needs it.
        var reopened = await Mtp(c).ActivateAsync(s.PatientId, new ActivateMtpRequest("Dr. Lee", "Rebleed"));
        Assert.True(reopened.Succeeded, reopened.Error);
        Assert.Equal(2, (await Mtp(c).ListForPatientAsync(s.PatientId)).Count);
    }

    [Fact]
    public async Task MtpActivation_WithoutEmergencyPermission_IsRefused()
    {
        var s = await SeedAsync("MTP-PERM");
        await using var c = _factory.Create();

        var denied = await Mtp(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate))
            .ActivateAsync(s.PatientId, new ActivateMtpRequest("Dr. Rivera", "Shock"));

        Assert.False(denied.Succeeded);
        Assert.True(denied.Evaluation!.IsHardStopped);
        Assert.False(await c.MtpActivations.AnyAsync(a => a.PatientId == s.PatientId));
    }

    // TEST-BB-321: emergency override cannot release red cells that conflict with an established ABO group.
    [Fact]
    public async Task EmergencyRelease_RedCellsConflictingWithEstablishedType_AreRefused_EvenWithOverride()
    {
        var s = await SeedAsync("EMERG-ABO", patientAbo: AboGroup.O, unitAbo: AboGroup.A,
            requiresRetype: true, unitStatus: UnitStatus.Received);

        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.EmergencyRelease,
                overrideReason: "Massive hemorrhage", authorizedBy: "dr-authorizer"));

            Assert.False(issued.Succeeded);
            Assert.True(issued.Evaluation!.IsHardStopped);
            Assert.Contains(issued.Evaluation.HardStops, r => r.Code == EmergencyUncrossmatchedAboRule.EstablishedConflictCode);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Received, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
        Assert.False(await verify.Issues.AnyAsync(i => i.BloodProductId == s.UnitId));
        Assert.False(await verify.Overrides.AnyAsync(o => o.ContextType == nameof(Issue) && o.ContextId == s.UnitId));
        var blocked = await verify.AuditEvents.SingleAsync(a =>
            a.EventType == AuditEventType.EmergencyRelease && a.EntityType == nameof(BloodUnit) && a.EntityId == s.UnitId);
        Assert.Equal("Issue blocked by safety gate.", blocked.Reason);
        Assert.Contains(EmergencyUncrossmatchedAboRule.EstablishedConflictCode, blocked.NewValueJson);
    }

    [Fact]
    public async Task EmergencyRelease_GroupORedCells_ForEstablishedGroupA_StillIssueWithOverride()
    {
        var s = await SeedAsync("EMERG-ABO-O", patientAbo: AboGroup.A, unitAbo: AboGroup.O,
            requiresRetype: true, unitStatus: UnitStatus.Received);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.EmergencyRelease,
            overrideReason: "Massive hemorrhage, uncrossmatched O units", authorizedBy: "dr-authorizer"));

        Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
    }

    [Fact]
    public async Task EmergencyRelease_WithoutOverride_RequiresOverride()
    {
        var s = await SeedAsync("EMERG-NOOVR");
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(
            IssueReq(s, IssueType.EmergencyRelease));

        Assert.False(issued.Succeeded);
        Assert.True(issued.RequiresOverride);
    }

    [Fact]
    public async Task EmergencyRelease_WithOverride_IssuesAndRecordsOverride()
    {
        var s = await SeedAsync("EMERG");
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.EmergencyRelease,
                overrideReason: "Massive hemorrhage, uncrossmatched O units required", authorizedBy: "dr-authorizer"));

            Assert.True(issued.Succeeded);
            Assert.Equal(IssueType.EmergencyRelease, issued.Value!.IssueType);
            Assert.NotNull(issued.Value.OverrideId);
            Assert.True(issued.Value.TestsIncompleteAtIssue);
            issueId = issued.Value.Id;
        }

        await using (var verify = _factory.Create())
        {
            var ovr = await verify.Overrides.Where(o => o.ContextType == nameof(Issue) && o.ContextId == s.UnitId).SingleAsync();
            Assert.Equal(OverrideAction.EmergencyRelease, ovr.Action);
            Assert.Contains(CrossmatchValidityRule.Code, ovr.RuleCode);

            var overrideAudit = await verify.AuditEvents.SingleAsync(a =>
                a.EventType == AuditEventType.Override && a.Reason == "Massive hemorrhage, uncrossmatched O units required");
            Assert.Equal("Massive hemorrhage, uncrossmatched O units required", overrideAudit.Reason);

            Assert.True(await verify.AuditEvents.AnyAsync(a =>
                a.EventType == AuditEventType.EmergencyRelease
                && a.EntityType == nameof(BloodUnit)
                && a.EntityId == s.UnitId));

            Assert.Equal(UnitStatus.Issued, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
            Assert.True((await verify.Issues.FindAsync(issueId))!.TestsIncompleteAtIssue);
            Assert.NotNull((await verify.Issues.FindAsync(issueId))!.RetrospectiveCrossmatchDueUtc);
        }
    }

    [Fact]
    public async Task MassiveTransfusion_WithOverride_RecordsUncrossmatchedStatusAndDetails()
    {
        var s = await SeedAsync("MTP");
        await AllocateAsync(s);
        await ActivateMtpAsync(s.PatientId);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.MassiveTransfusion,
            overrideReason: "MTP pack, uncrossmatched group O issued", authorizedBy: "dr-authorizer"));

        Assert.True(issued.Succeeded);
        Assert.Equal(IssueType.MassiveTransfusion, issued.Value!.IssueType);
        Assert.True(issued.Value.TestsIncompleteAtIssue);

        var persisted = await c.Issues.FindAsync(issued.Value.Id);
        Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency, persisted!.CrossmatchStatus);
        Assert.Equal("MTP pack, uncrossmatched group O issued", persisted.EmergencyReleaseDetails);
        Assert.NotNull(persisted.RetrospectiveCrossmatchDueUtc);

        var queue = await Issuing(c).ListPendingRetrospectiveCrossmatchesAsync();
        Assert.Contains(queue, q => q.IssueId == persisted.Id && q.IssueType == IssueType.MassiveTransfusion);
    }

    // TEST-BB-378 (OCD-041): the issue override stores the reason and authorizer as named, trimmed, and refuses a blank authorizer.
    [Fact]
    public async Task EmergencyRelease_Override_StoresTrimmedFields_AndNeedsAnAuthorizer()
    {
        var s = await SeedAsync("OVR-TRIM");
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var blank = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.EmergencyRelease, overrideReason: "Hemorrhage", authorizedBy: "   "));
        Assert.False(blank.Succeeded);
        Assert.Contains(blank.Evaluation!.Warnings, w => w.Code == OverrideFieldsRule.Code);

        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.EmergencyRelease, overrideReason: "  Hemorrhage  ", authorizedBy: "  dr-authorizer "));
        Assert.True(issued.Succeeded, issued.Error);
        var stored = await c.Overrides.AsNoTracking().SingleAsync(o => o.Id == issued.Value!.OverrideId);
        Assert.Equal("Hemorrhage", stored.Reason);
        Assert.Equal("dr-authorizer", stored.AuthorizedBy);
    }

    [Fact]
    public async Task EmergencyRelease_AppearsOnRetrospectiveWorklist_UntilCompatibleXm()
    {
        var s = await SeedAsync("RETROXM");
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.EmergencyRelease,
                overrideReason: "Uncrossmatched release for hemorrhage", authorizedBy: "dr-authorizer") with { IssuedTo = "RN Alvarez", CoolerId = "CLR-TRAUMA-1", IssuedToLocation = " Trauma Bay 2 " });
            Assert.True(issued.Succeeded);
            issueId = issued.Value!.Id;
            var saved = await c.Issues.SingleAsync(i => i.Id == issueId);
            saved.IssuedBy = " tech-test ";
            saved.EmergencyReleaseDetails = " Uncrossmatched release for hemorrhage ";
            saved.SecondVerifier = " witness-tech ";
            var authorizer = await c.Overrides.SingleAsync(o => o.Id == saved.OverrideId);
            authorizer.AuthorizedBy = " dr-authorizer ";
            await c.SaveChangesAsync();

            var pending = await Issuing(c).ListPendingRetrospectiveCrossmatchesAsync();
            Assert.Contains(pending, p => p.IssueId == issueId);
            var retro = pending.Single(p => p.IssueId == issueId);
            Assert.Equal(s.Mrn, retro.MedicalRecordNumber);
            Assert.Equal(s.UnitNumber, retro.UnitNumber);
            Assert.Equal("Test RBC", retro.ProductName);
            Assert.Equal(_factory.Clock.UtcNow.AddDays(20), retro.UnitExpiresUtc);
            Assert.Equal("O+", retro.UnitBloodType);
            Assert.Equal(s.SpecimenId, retro.SpecimenId);
            Assert.Equal("ACC-RETROXM", retro.AccessionNumber);
            Assert.Null(retro.CrossmatchExpiresUtc);
            Assert.Null(retro.LatestCrossmatchResult);
            Assert.Equal("RN Alvarez", retro.IssuedTo);
            Assert.Equal("CLR-TRAUMA-1", retro.CoolerId);
            Assert.Equal("Trauma Bay 2", retro.IssuedToLocation);
            Assert.Equal("tech-test", retro.IssuedBy);
            Assert.Equal("Uncrossmatched release for hemorrhage", retro.EmergencyReleaseDetails);
            Assert.Equal("witness-tech", retro.SecondVerifier);
            Assert.Equal("dr-authorizer", retro.EmergencyReleaseAuthorizer);
            Assert.Equal(UnitAppearance.Acceptable, retro.IssueAppearance);
            Assert.Null(retro.PtagCode);
            Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency, retro.CrossmatchStatus);
            saved.IssuedBy = " ";
            saved.EmergencyReleaseDetails = " ";
            saved.SecondVerifier = " ";
            authorizer.AuthorizedBy = " ";
            saved.IssueAppearance = UnitAppearance.Hemolysis;
            saved.PtagCode = " PTAG-RETRO ";
            await c.SaveChangesAsync();
            var blanked = (await Issuing(c).ListPendingRetrospectiveCrossmatchesAsync()).Single(p => p.IssueId == issueId);
            Assert.Null(blanked.IssuedBy);
            Assert.Null(blanked.EmergencyReleaseDetails);
            Assert.Null(blanked.SecondVerifier);
            Assert.Null(blanked.EmergencyReleaseAuthorizer);
            Assert.Equal(UnitAppearance.Hemolysis, blanked.IssueAppearance);
            Assert.Equal("PTAG-RETRO", blanked.PtagCode);
            saved.IssuedBy = "tech-test";
            saved.EmergencyReleaseDetails = "Uncrossmatched release for hemorrhage";
            saved.SecondVerifier = "witness-tech";
            authorizer.AuthorizedBy = "dr-authorizer";
            saved.IssueAppearance = UnitAppearance.Acceptable;
            saved.PtagCode = " ";
            await c.SaveChangesAsync();
            Assert.Equal(IssueType.EmergencyRelease, Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId).IssueType);
            Assert.Equal(IssueType.EmergencyRelease, Assert.Single(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId).IssueType);
            Assert.Equal("tech-test", Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId).IssuedBy);
            Assert.Equal("tech-test", Assert.Single(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId).IssuedBy);
            Assert.Equal("Uncrossmatched release for hemorrhage", Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId).EmergencyReleaseDetails);
            Assert.Equal("Uncrossmatched release for hemorrhage", Assert.Single(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId).EmergencyReleaseDetails);
            Assert.False(string.IsNullOrWhiteSpace(retro.PatientDisplayName));
            Assert.False(string.IsNullOrWhiteSpace(retro.CurrentBloodType));
        }

        await using (var c = _factory.Create())
        {
            var xm = await Compatibility(c).RecordCrossmatchAsync(
                new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Compatible));
            Assert.True(xm.Succeeded);

            var pending = await Issuing(c).ListPendingRetrospectiveCrossmatchesAsync();
            Assert.DoesNotContain(pending, p => p.IssueId == issueId);
        }

        await using var verify = _factory.Create();
        var issue = await verify.Issues.FindAsync(issueId);
        Assert.Equal(CrossmatchClinicalStatus.Compatible, issue!.CrossmatchStatus);
        Assert.NotNull(issue.RetrospectiveCrossmatchCompletedUtc);
        Assert.NotNull(issue.RetrospectiveCrossmatchId);
    }

    [Fact]
    public async Task EmergencyRelease_IncompatibleXm_StaysOnWorklist()
    {
        var s = await SeedAsync("RETROINC");
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.EmergencyRelease,
                overrideReason: "Uncrossmatched release", authorizedBy: "dr-authorizer"))).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var xm = await Compatibility(c).RecordCrossmatchAsync(
                new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Incompatible));
            Assert.True(xm.Succeeded);
            var pending = await Issuing(c).ListPendingRetrospectiveCrossmatchesAsync();
            var incompatible = Assert.Single(pending, p => p.IssueId == issueId);
            Assert.Equal(CrossmatchResult.Incompatible, incompatible.LatestCrossmatchResult);
            Assert.Null(incompatible.CrossmatchExpiresUtc);
        }

        await using var verify = _factory.Create();
        Assert.Equal(CrossmatchClinicalStatus.Incompatible, (await verify.Issues.FindAsync(issueId))!.CrossmatchStatus);
        Assert.Null((await verify.Issues.FindAsync(issueId))!.RetrospectiveCrossmatchCompletedUtc);
    }

    // TEST-BB-336: an incompatible retrospective crossmatch warns, raises one critical alert, and leads the board.
    [Fact]
    public async Task EmergencyRelease_IncompatibleXm_RaisesCriticalAlert_AndLeadsTheBoard()
    {
        var calm = await SeedAsync("RETROCALM");
        await AllocateAsync(calm);
        var s = await SeedAsync("RETROALRT");
        await AllocateAsync(s);

        long issueId;
        long calmIssueId;
        await using (var c = _factory.Create())
        {
            // The calm issue is older, so by due time alone it would be listed first.
            calmIssueId = (await Issuing(c).IssueUnitAsync(IssueReq(
                calm, IssueType.EmergencyRelease,
                overrideReason: "Uncrossmatched release", authorizedBy: "dr-authorizer"))).Value!.Id;
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.EmergencyRelease,
                overrideReason: "Uncrossmatched release", authorizedBy: "dr-authorizer"))).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var xm = await CompatibilityWithAlerts(c).RecordCrossmatchAsync(
                new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Incompatible));
            Assert.True(xm.Succeeded, xm.Error);
            var warning = Assert.Single(xm.Evaluation!.Warnings, w => w.Code == RetrospectiveCrossmatchIncompatibleRule.Code);
            Assert.Contains("issued", warning.Message);
            Assert.Contains("Deviation ", warning.Message);

            // A repeat incompatible crossmatch does not raise a second alert or open a second deviation.
            Assert.True((await CompatibilityWithAlerts(c).RecordCrossmatchAsync(
                new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Incompatible))).Succeeded);

            var pending = await Issuing(c).ListPendingRetrospectiveCrossmatchesAsync();
            var ids = pending.Select(p => p.IssueId).ToList();
            Assert.True(ids.IndexOf(issueId) < ids.IndexOf(calmIssueId), "The incompatible row should lead the board.");
        }

        await using var verify = _factory.Create();
        var alerts = await verify.Alerts.Where(a => a.Source == AlertRule.RetrospectiveCrossmatchIncompatible && a.Key == $"RETROXM:ISSUE-{issueId}").ToListAsync();
        var alert = Assert.Single(alerts);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Contains("MRN", alert.Message);
        Assert.Equal("/issuing?desk=retro", alert.Link);

        // TEST-BB-390 (OCD-046): the incompatible result opened exactly one critical deviation linked to the issue, audited.
        var deviation = Assert.Single(await verify.Deviations
            .Where(d => d.ContextType == RetrospectiveCrossmatchIncompatibleRule.DeviationContextType && d.ContextId == issueId)
            .ToListAsync());
        Assert.Equal(DeviationSeverity.Critical, deviation.Severity);
        Assert.Equal(DeviationStatus.Open, deviation.Status);
        Assert.Contains($"Issue {issueId}", deviation.Description);
        Assert.Contains($"Deviation {deviation.Id} was opened.", alert.Message);
        Assert.True(await verify.AuditEvents.AnyAsync(a => a.EventType == AuditEventType.Deviation && a.EntityId == deviation.Id));
    }

    [Fact]
    public async Task EmergencyRelease_CompatibleXm_RaisesNoAlert()
    {
        var s = await SeedAsync("RETRONOALRT");
        await AllocateAsync(s);
        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.EmergencyRelease,
                overrideReason: "Uncrossmatched release", authorizedBy: "dr-authorizer"))).Value!.Id;
            var xm = await CompatibilityWithAlerts(c).RecordCrossmatchAsync(
                new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Compatible));
            Assert.True(xm.Succeeded, xm.Error);
            Assert.DoesNotContain(xm.Evaluation?.Warnings ?? [], w => w.Code == RetrospectiveCrossmatchIncompatibleRule.Code);
        }

        await using var verify = _factory.Create();
        Assert.False(await verify.Alerts.AnyAsync(a => a.Key == $"RETROXM:ISSUE-{issueId}"));
        Assert.False(await verify.Deviations.AnyAsync(d => d.ContextType == RetrospectiveCrossmatchIncompatibleRule.DeviationContextType && d.ContextId == issueId));
    }

    [Fact]
    public async Task EmergencyRelease_Return_DropsRetrospectiveWorklist()
    {
        var s = await SeedAsync("RETRORET");
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.EmergencyRelease,
                overrideReason: "Uncrossmatched release", authorizedBy: "dr-authorizer"))).Value!.Id;
        }

        await using var ctx = _factory.Create();
        Assert.True((await Issuing(ctx).ReturnUnitAsync(issueId, new ReturnUnitRequest("Never transfused", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true))).Succeeded);
        var pending = await Issuing(ctx).ListPendingRetrospectiveCrossmatchesAsync();
        Assert.DoesNotContain(pending, p => p.IssueId == issueId);
    }

    [Fact]
    public async Task Return_ReissueEligible_ReturnsUnitToAvailable()
    {
        var s = await SeedAsync("RETURN");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var returned = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest("Not needed; cooler intact", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
            Assert.True(returned.Succeeded);
        }

        await using (var verify = _factory.Create())
        {
            Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
            Assert.Equal(IssueStatus.Returned, (await verify.Issues.FindAsync(issueId))!.Status);

            var history = await verify.InventoryStatusHistory.Where(h => h.BloodProductId == s.UnitId).ToListAsync();
            Assert.Contains(history, h => h.FromStatus == UnitStatus.Issued && h.ToStatus == UnitStatus.Returned);
            Assert.Contains(history, h => h.FromStatus == UnitStatus.Returned && h.ToStatus == UnitStatus.Available);
        }
    }

    // TEST-BB-314 (OCD-040): a return that could go back to stock needs a recorded inspection.
    private async Task<(Scenario S, long IssueId)> IssuedForReturnAsync(string tag)
    {
        var s = await SeedAsync(tag);
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        await using var c = _factory.Create();
        return (s, (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id);
    }

    [Fact]
    public async Task Return_WithUnansweredInspection_IsRefused_AndTheUnitStaysIssued()
    {
        var (s, issueId) = await IssuedForReturnAsync("RET-INSP1");

        await using (var c = _factory.Create())
        {
            var r = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest("Not needed", TemperatureAcceptable: true));
            Assert.False(r.Succeeded);
            var stop = Assert.Single(r.Evaluation!.HardStops, h => h.Code == ReturnInspectionRule.Code);
            Assert.Contains("seal integrity, visual inspection", stop.Message);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Issued, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
        Assert.Empty(await verify.Returns.Where(x => x.IssueId == issueId).ToListAsync());
    }

    [Fact]
    public async Task Return_WithFacilityDefault_TreatsUnansweredAsAcceptable_AndSaysSo()
    {
        var (s, issueId) = await IssuedForReturnAsync("RET-INSP2");
        // This class shares one database across tests: turn the policy back off afterwards.
        await SetInspectionDefaultAsync(true);
        try
        {
            await using (var c = _factory.Create())
            {
                Assert.True((await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest("Not needed"))).Succeeded);
            }

            await using var verify = _factory.Create();
            Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
            var json = (await verify.Returns.SingleAsync(x => x.IssueId == issueId)).ReissueEvaluationJson!;
            Assert.Contains("\"InspectionFromFacilityDefault\":true", json);
        }
        finally
        {
            await SetInspectionDefaultAsync(false);
        }
    }

    private async Task SetInspectionDefaultAsync(bool on)
    {
        await using var c = _factory.Create();
        var row = await c.SystemSettings.FirstOrDefaultAsync(x => x.Key == FacilityPolicyKeys.InspectionDefaultAcceptable);
        if (row is null)
        {
            c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.InspectionDefaultAcceptable, Value = on ? "true" : "false", Category = "Inventory" });
        }
        else
        {
            row.Value = on ? "true" : "false";
        }

        await c.SaveChangesAsync();
    }

    [Fact]
    public async Task Return_ExplicitQuarantine_WithoutInspection_IsAllowed_AndRecordedAsUnanswered()
    {
        var (s, issueId) = await IssuedForReturnAsync("RET-INSP3");

        await using (var c = _factory.Create())
        {
            var r = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest("Unsure of storage", Destination: UnitStatus.Quarantine));
            Assert.True(r.Succeeded, r.Error);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Quarantine, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
        var json = (await verify.Returns.SingleAsync(x => x.IssueId == issueId)).ReissueEvaluationJson!;
        Assert.Contains("\"InspectionUnanswered\":true", json);
        Assert.Contains("\"InspectionFromFacilityDefault\":false", json);
    }

    // TEST-BB-315 (OCD-040): appearance at issue and ward receipt is what a person saw on the bag.
    [Fact]
    public async Task Issue_WithoutAppearance_IsRefused_AndTheUnitIsNotIssued()
    {
        var s = await SeedAsync("APP-ISS1");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using (var c = _factory.Create())
        {
            var r = await Issuing(c).IssueUnitAsync(IssueReq(s) with { Appearance = null });
            Assert.False(r.Succeeded);
            Assert.Contains(r.Evaluation!.HardStops, h => h.Code == AppearanceAttestationRule.Code);
        }

        await using var verify = _factory.Create();
        Assert.NotEqual(UnitStatus.Issued, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
        Assert.Empty(await verify.Issues.Where(i => i.BloodProductId == s.UnitId).ToListAsync());
    }

    [Fact]
    public async Task Issue_WithFacilityDefault_RecordsAcceptable_AndAuditsThatItWasDefaulted()
    {
        var s = await SeedAsync("APP-ISS2");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        await SetInspectionDefaultAsync(true);
        try
        {
            long issueId;
            await using (var c = _factory.Create())
            {
                var r = await Issuing(c).IssueUnitAsync(IssueReq(s) with { Appearance = null });
                Assert.True(r.Succeeded, r.Error);
                issueId = r.Value!.Id;
            }

            await using var verify = _factory.Create();
            Assert.Equal(UnitAppearance.Acceptable, (await verify.Issues.FindAsync(issueId))!.IssueAppearance);
            var audit = await verify.AuditEvents
                .Where(a => a.EntityType == nameof(BloodUnit) && a.EntityId == s.UnitId && a.NewValueJson!.Contains("AppearanceFromFacilityDefault"))
                .ToListAsync();
            Assert.Contains(audit, a => a.NewValueJson!.Contains("\"AppearanceFromFacilityDefault\":true"));
        }
        finally
        {
            await SetInspectionDefaultAsync(false);
        }
    }

    [Fact]
    public async Task WardReceipt_WithoutAppearance_IsRefused()
    {
        var (_, issueId) = await IssuedForReturnAsync("APP-WARD1");

        await using var c = _factory.Create();
        var r = await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"));
        Assert.False(r.Succeeded);
        var stop = Assert.Single(r.Evaluation!.HardStops, h => h.Code == AppearanceAttestationRule.Code);
        Assert.Contains("acknowledging receipt", stop.Message);
    }

    [Fact]
    public async Task Return_ExplicitAvailable_WithoutInspection_IsRefused()
    {
        var (_, issueId) = await IssuedForReturnAsync("RET-INSP4");

        await using var c = _factory.Create();
        var r = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest("Back to stock", Destination: UnitStatus.Available));
        Assert.False(r.Succeeded);
        Assert.Contains(r.Evaluation!.HardStops, h => h.Code == ReturnInspectionRule.Code);
    }

    [Fact]
    public async Task Return_NotReissueEligible_GoesToQuarantine()
    {
        var s = await SeedAsync("RETURN-Q");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var returned = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest("Out of temperature range", TemperatureAcceptable: false, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
            Assert.True(returned.Succeeded);
        }

        await using (var verify = _factory.Create())
        {
            var unit = await verify.BloodUnits.FindAsync(s.UnitId);
            Assert.Equal(UnitStatus.Quarantine, unit!.Status);
            Assert.Equal("Out of temperature range", unit.QuarantineReason);
        }
    }

    [Fact]
    public async Task Return_AfterThirtyMinutes_AvailableRejected_QuarantineAllowed()
    {
        var s = await SeedAsync("RETURN-30");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-31) , LateEntryReason = "Simulated earlier issue for the test" })).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var available = await Issuing(c).ReturnUnitAsync(
                issueId,
                new ReturnUnitRequest("Still unused", Destination: UnitStatus.Available, TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
            Assert.False(available.Succeeded);
            Assert.Contains("30 minutes", available.Error, StringComparison.OrdinalIgnoreCase);

            var quarantined = await Issuing(c).ReturnUnitForBloodUnitAsync(
                s.UnitId,
                new ReturnUnitRequest("Out too long", Destination: UnitStatus.Quarantine));
            Assert.True(quarantined.Succeeded, quarantined.Error);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Quarantine, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
        Assert.Equal(IssueStatus.Returned, (await verify.Issues.FindAsync(issueId))!.Status);
    }

    [Fact]
    public async Task Return_CallerTimeOutFlag_DoesNotOverrideAGovernedWindow()
    {
        var s = await SeedAsync("RETURN-GOV");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var location = new InventoryLocation { Code = "GOV-RET", Name = "Governed fridge", TimeOutGoverned = true };
            c.InventoryLocations.Add(location);
            await c.SaveChangesAsync();
            var stored = await c.BloodUnits.FindAsync(s.UnitId);
            stored!.CurrentLocationId = location.Id;
            await c.SaveChangesAsync();
            issueId = (await Issuing(c).IssueUnitAsync(
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-40) , LateEntryReason = "Simulated earlier issue for the test" })).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var available = await Issuing(c).ReturnUnitAsync(
                issueId,
                new ReturnUnitRequest("Caller says the window is fine", TimeOutOfStorageAcceptable: true, Destination: UnitStatus.Available, TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
            Assert.False(available.Succeeded);
            Assert.Contains("30 minutes", available.Error, StringComparison.OrdinalIgnoreCase);

            var row = Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
            Assert.True(row.StorageTimeOutGoverned);
            Assert.False(row.StorageTimeOutAcceptable);
            Assert.Equal("Window elapsed", ReturnReissueRule.TimeOutLabel(row.StorageTimeOutGoverned, row.StorageTimeOutAcceptable));
        }
    }

    // TEST-BB-374: the outstanding-issue row's time-out decision follows the facility window, which the unit chart now uses.
    [Fact]
    public async Task OutstandingIssue_TimeOutDecision_FollowsTheFacilityWindow()
    {
        var s = await SeedAsync("RETURN-WIN");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var location = new InventoryLocation { Code = "WIN-RET", Name = "Governed fridge 2", TimeOutGoverned = true };
            c.InventoryLocations.Add(location);
            await c.SaveChangesAsync();
            var stored = await c.BloodUnits.FindAsync(s.UnitId);
            stored!.CurrentLocationId = location.Id;
            await c.SaveChangesAsync();
            issueId = (await Issuing(c).IssueUnitAsync(
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-40), LateEntryReason = "Simulated earlier issue for the test" })).Value!.Id;
        }

        try
        {
            await using (var c = _factory.Create())
            {
                Assert.False(Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId).StorageTimeOutAcceptable);
                c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.ReturnToAvailableMinutes, Value = "60", Category = "Inventory" });
                await c.SaveChangesAsync();
            }

            await using var v = _factory.Create();
            var row = Assert.Single(await Issuing(v).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
            Assert.True(row.StorageTimeOutGoverned);
            Assert.True(row.StorageTimeOutAcceptable);
        }
        finally
        {
            await using var cleanup = _factory.Create();
            cleanup.SystemSettings.RemoveRange(cleanup.SystemSettings.Where(x => x.Key == FacilityPolicyKeys.ReturnToAvailableMinutes));
            await cleanup.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Return_ExemptLocation_AfterThirtyMinutes_CanReturnToAvailable()
    {
        var s = await SeedAsync("RETURN-EX");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var location = new InventoryLocation { Code = "EX-RET", Name = "Validated remote fridge", TimeOutGoverned = false };
            c.InventoryLocations.Add(location);
            await c.SaveChangesAsync();
            var stored = await c.BloodUnits.FindAsync(s.UnitId);
            stored!.CurrentLocationId = location.Id;
            await c.SaveChangesAsync();
            issueId = (await Issuing(c).IssueUnitAsync(
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-40) , LateEntryReason = "Simulated earlier issue for the test" })).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var row = Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
            Assert.False(row.StorageTimeOutGoverned);
            Assert.True(row.StorageTimeOutAcceptable);

            var returned = await Issuing(c).ReturnUnitAsync(
                issueId,
                new ReturnUnitRequest(
                    "Exempt device, still in temperature",
                    TimeOutOfStorageAcceptable: false,
                    Destination: UnitStatus.Available, TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
            Assert.True(returned.Succeeded, returned.Error);
            Assert.Contains("\"TimeOutOfStorageAcceptable\":true", returned.Value!.ReissueEvaluationJson, StringComparison.Ordinal);
            Assert.Contains("\"TimeOutGoverned\":false", returned.Value.ReissueEvaluationJson, StringComparison.Ordinal);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    // TEST-BB-340: a return asking for disposal quarantines the unit and points to the coded discard.
    [Fact]
    public async Task Return_AfterThirtyMinutes_DisposalRequested_QuarantinesInstead()
    {
        var s = await SeedAsync("RETURN-DISP");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-45) , LateEntryReason = "Simulated earlier issue for the test" })).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var disposed = await Issuing(c).ReturnUnitAsync(
                issueId,
                new ReturnUnitRequest("Not needed, held too long", Destination: UnitStatus.Discarded));
            Assert.True(disposed.Succeeded, disposed.Error);
            Assert.Contains(disposed.Evaluation!.Warnings, w => w.Code == ReturnDisposalRule.Code);
        }

        await using var verify = _factory.Create();
        var unit = (await verify.BloodUnits.FindAsync(s.UnitId))!;
        Assert.Equal(UnitStatus.Quarantine, unit.Status);
        Assert.Equal(UnitQuarantineReason.ReturnedForDisposal, unit.QuarantineReasonCode);
        Assert.Null(unit.DiscardReasonCode);
        Assert.DoesNotContain(await verify.InventoryStatusHistory.Where(h => h.BloodProductId == s.UnitId).ToListAsync(),
            h => h.ToStatus == UnitStatus.Discarded);
    }

    [Fact]
    public async Task Return_FailedVisual_QuarantinesAndLeavesOutstanding()
    {
        var s = await SeedAsync("RETURN-VIS");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
            Assert.Contains(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
        }

        await using (var c = _factory.Create())
        {
            var disposed = await Issuing(c).ReturnUnitAsync(
                issueId,
                new ReturnUnitRequest(
                    "Hemolysis on return",
                    SealIntegrityAcceptable: false,
                    VisualInspectionAcceptable: false,
                    Destination: UnitStatus.Discarded));
            Assert.True(disposed.Succeeded, disposed.Error);
            Assert.False(disposed.Value!.ReissueEligible);
            Assert.Contains(disposed.Evaluation!.Warnings, w => w.Code == ReturnDisposalRule.Code && w.Message.Contains("failed return inspection"));
            Assert.DoesNotContain(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
        }

        await using var verify = _factory.Create();
        var unit = (await verify.BloodUnits.FindAsync(s.UnitId))!;
        Assert.Equal(UnitStatus.Quarantine, unit.Status);
        Assert.Equal(UnitQuarantineReason.FailedReturnInspection, unit.QuarantineReasonCode);
        Assert.Equal(IssueStatus.Returned, (await verify.Issues.FindAsync(issueId))!.Status);
    }

    [Fact]
    public async Task UnitActivity_IncludesAllocateCrossmatchIssueAndCanceledMod()
    {
        var s = await SeedAsync("ACT-TRAIL");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using (var c = _factory.Create())
        {
            var allocation = await c.Allocations.SingleAsync(a => a.BloodProductId == s.UnitId);
            var encounter = new Encounter
            {
                PatientId = s.PatientId,
                VisitNumber = "VIS-ACT-TRAIL",
                EncounterType = EncounterType.Inpatient,
                Status = EncounterStatus.Active,
                AdmitUtc = _factory.Clock.UtcNow
            };
            var location = new OrderingLocation { Code = "LOC-ACT-TRAIL", Name = "Ward", IsActive = true };
            c.Encounters.Add(encounter);
            c.OrderingLocations.Add(location);
            await c.SaveChangesAsync();

            var order = new Order
            {
                OrderNumber = "ORD-ACT-TRAIL",
                PatientId = s.PatientId,
                EncounterId = encounter.Id,
                OrderingLocationId = location.Id,
                OrderCategory = OrderCategory.Product,
                ProductTypeId = s.ProductTypeId,
                Status = OrderStatus.InProcess,
                OrderedUtc = _factory.Clock.UtcNow
            };
            c.Orders.Add(order);
            var definition = new ActionDefinition
            {
                Code = "IRRAD-ACT",
                Name = "Irradiate",
                Category = ActionCategory.Modification,
                ModificationType = ModificationType.Irradiate,
                IsActive = true
            };
            c.ActionDefinitions.Add(definition);
            await c.SaveChangesAsync();

            c.OrderActions.Add(new OrderAction
            {
                OrderId = order.Id,
                ActionDefinitionId = definition.Id,
                Status = OrderActionStatus.Pending,
                SourceBloodUnitId = s.UnitId
            });
            allocation.OrderId = order.Id;
            await c.SaveChangesAsync();

            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.True(issued.Succeeded, issued.Error);
        }

        await using var verify = _factory.Create();
        var trail = await Activity(verify).ListAsync(s.UnitId);
        Assert.Contains(trail, r => r.EventType == "Allocation");
        Assert.Contains(trail, r => r.EventType == "Crossmatch" && r.Summary.Contains("Compatible", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(trail, r => r.EventType == "Issue");
        Assert.Contains(trail, r =>
            r.EventType == "Modification"
            && r.Summary.Contains("Cancelled", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(OrderActionStatus.Cancelled, (await verify.OrderActions.SingleAsync(a => a.SourceBloodUnitId == s.UnitId)).Status);
    }

    [Fact]
    public async Task ElectronicCrossmatch_WithAntibodyHistory_IsBlocked()
    {
        var s = await SeedAsync("EXM");
        await using (var c = _factory.Create())
        {
            c.AntibodyHistory.Add(new AntibodyHistory { PatientId = s.PatientId, AntibodySpecificity = "anti-K", IsActive = true });
            await c.SaveChangesAsync();
            await ExmScreenSeed.AddVerifiedNegativeScreenAsync(c, s.PatientId, _factory.Clock.UtcNow, s.SpecimenId);
        }

        await using var ctx = _factory.Create();
        var result = await Compatibility(ctx).RecordCrossmatchAsync(
            new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Electronic));

        Assert.False(result.Succeeded);
        Assert.True(result.Evaluation!.IsHardStopped);
        Assert.Contains(result.Evaluation.HardStops, r => r.Code == ElectronicCrossmatchEligibilityRule.HistoryCode);
    }

    // TEST-BB-700: an electronic crossmatch needs the verified negative screen on the specimen it is recorded against;
    // a negative screen on another specimen does not stand in for it, and nothing is recorded.
    [Fact]
    public async Task ElectronicCrossmatch_WithoutScreenOnTheSpecimen_IsBlocked()
    {
        var s = await SeedAsync("EXM-NOSCR");
        await using (var c = _factory.Create())
        {
            await ExmScreenSeed.AddVerifiedNegativeScreenAsync(c, s.PatientId, _factory.Clock.UtcNow);
        }

        await using var ctx = _factory.Create();
        var result = await Compatibility(ctx).RecordCrossmatchAsync(
            new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Electronic));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ElectronicCrossmatchEligibilityRule.ScreenCode);
        Assert.False(await ctx.Crossmatches.AnyAsync(x => x.BloodProductId == s.UnitId && x.Method == CrossmatchMethod.Electronic));
    }

    [Fact]
    public async Task Issue_MismatchedPatientIdentifiers_IsHardStopped()
    {
        var s = await SeedAsync("BADID");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with { PatientIdentifier1Value = "WRONG" });
        Assert.True(issued.Evaluation!.IsHardStopped);
        Assert.Contains(issued.Evaluation.HardStops, r => r.Code == IssueGate.IdentityCode);
    }

    [Fact]
    public async Task Issue_UnmetSpecialRequirement_IsHardStopped()
    {
        var s = await SeedAsync("IRRAD");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using (var c = _factory.Create())
        {
            c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
            {
                PatientId = s.PatientId,
                RequirementType = SpecialTransfusionRequirementType.Irradiated,
                Reason = "Directed donation",
                EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
                IsActive = true,
                EnteredBy = "tech-test"
            });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var issued = await Issuing(ctx).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Evaluation!.IsHardStopped);
        Assert.Contains(issued.Evaluation.HardStops, r => r.Code == IssueGate.SpecialReqCode);
    }

    /// <summary>Adds a catalog entry for one unit category and puts it on the patient's chart.</summary>
    private async Task RequireAsync(
        long patientId,
        string code,
        SpecialRequirementCategory category,
        SpecialRequirementEnforcementKind kind,
        ComponentClass componentClass,
        string? attribute = null,
        string? allowedAbo = null,
        string? unitTest = null)
    {
        await using var c = _factory.Create();
        var definition = new SpecialRequirementDefinition
        {
            Code = code,
            Name = code,
            Level = SpecialRequirementLevel.Unit,
            Category = category,
            EnforcementKind = kind,
            ComponentClass = componentClass,
            ProductAttributeCode = attribute,
            AllowedAboGroups = allowedAbo,
            UnitTestCode = unitTest,
            IsActive = true,
            Version = 1
        };
        c.SpecialRequirementDefinitions.Add(definition);
        await c.SaveChangesAsync();
        c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
        {
            PatientId = patientId,
            RequirementDefinitionId = definition.Id,
            RequirementType = SpecialTransfusionRequirementType.Other,
            Reason = "Ordered by the transfusion service physician",
            EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
            IsActive = true,
            EnteredBy = "tech-test"
        });
        await c.SaveChangesAsync();
    }

    // TEST-BB-441: reserving from Compatibility applies the same special requirement check as the chart and issue.
    [Fact]
    public async Task CompatibilityReserve_RefusesAUnitThatMissesARequirement_ButReservesOneThatMeetsIt()
    {
        var rbc = await SeedAsync("CPR-RBC", patientAbo: AboGroup.A, unitAbo: AboGroup.A);
        await RequireAsync(rbc.PatientId, "CPR-T1", SpecialRequirementCategory.UnitTyping,
            SpecialRequirementEnforcementKind.RequireUnitAbo, ComponentClass.RedBloodCells, allowedAbo: "O");

        await using (var c = _factory.Create())
        {
            var preview = await CompatibilityWithRequirements(c).EvaluateCompatibilityAsync(new EvaluateCompatibilityRequest(rbc.UnitId, rbc.PatientId));
            Assert.Contains(preview.Value!.HardStops, r => r.Code == SpecialTransfusionRequirementRule.Code);

            var refused = await CompatibilityWithRequirements(c).AllocateUnitAsync(new AllocateUnitRequest(rbc.UnitId, rbc.PatientId, rbc.SpecimenId));
            Assert.False(refused.Succeeded);
            var stop = Assert.Single(refused.Evaluation!.HardStops, r => r.Code == SpecialTransfusionRequirementRule.Code);
            Assert.Contains("must be group O", stop.Message);
            Assert.False(await c.Allocations.AnyAsync(a => a.BloodProductId == rbc.UnitId && a.Status == AllocationStatus.Reserved));
        }

        // The red-cell typing does not apply to plasma: group A plasma is reserved for the same requirement.
        var plasma = await SeedAsync("CPR-FFP", patientAbo: AboGroup.A, unitAbo: AboGroup.A,
            requiresCrossmatch: false, componentClass: ComponentClass.Plasma);
        await RequireAsync(plasma.PatientId, "CPR-T2", SpecialRequirementCategory.UnitTyping,
            SpecialRequirementEnforcementKind.RequireUnitAbo, ComponentClass.RedBloodCells, allowedAbo: "O");
        await using var ctx = _factory.Create();
        var reserved = await CompatibilityWithRequirements(ctx).AllocateUnitAsync(new AllocateUnitRequest(plasma.UnitId, plasma.PatientId, plasma.SpecimenId));
        Assert.True(reserved.Succeeded, reserved.Error);
    }

    private CompatibilityService CompatibilityWithRequirements(BloodBankDbContext c) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            BloodAttrCompat(c), AntibodyScreenCompat(c), c, _factory.Clock, _factory.CurrentUser,
            new EfRepository<Issue>(c), new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            workups: new EfRepository<AntibodyIdentificationWorkup>(c),
            retypes: new EfRepository<ProductRetypeResult>(c),
            specialRequirements: new EfRepository<SpecialTransfusionRequirement>(c),
            requirementDefinitions: new EfRepository<SpecialRequirementDefinition>(c),
            productAttributeAssignments: new EfRepository<ProductAttributeAssignment>(c),
            productAttributes: new EfRepository<ProductAttribute>(c),
            unitSpecialTests: new EfRepository<BloodComponentSpecialTest>(c));

    // TEST-BB-412: a red-cell typing stops a group A red cell but not group A plasma for the same patient.
    [Fact]
    public async Task Issue_GroupORedCellsOnly_RefusesAGroupARedCell_ButNotGroupAPlasma()
    {
        var rbc = await SeedAsync("GRPO-RBC", patientAbo: AboGroup.A, unitAbo: AboGroup.A);
        await RecordCompatibleCrossmatchAsync(rbc);
        await AllocateAsync(rbc);
        await RequireAsync(rbc.PatientId, "GRPO-T1", SpecialRequirementCategory.UnitTyping,
            SpecialRequirementEnforcementKind.RequireUnitAbo, ComponentClass.RedBloodCells, allowedAbo: "O");

        await using (var c = _factory.Create())
        {
            var refused = await Issuing(c).IssueUnitAsync(IssueReq(rbc));
            Assert.True(refused.Evaluation!.IsHardStopped);
            var stop = Assert.Single(refused.Evaluation.HardStops, r => r.Code == IssueGate.SpecialReqCode);
            Assert.Contains("must be group O", stop.Message);
            Assert.NotEqual(UnitStatus.Issued, (await c.BloodUnits.FindAsync(rbc.UnitId))!.Status);
        }

        var plasma = await SeedAsync("GRPO-FFP", patientAbo: AboGroup.A, unitAbo: AboGroup.A,
            requiresCrossmatch: false, componentClass: ComponentClass.Plasma);
        await AllocateAsync(plasma);
        await RequireAsync(plasma.PatientId, "GRPO-T2", SpecialRequirementCategory.UnitTyping,
            SpecialRequirementEnforcementKind.RequireUnitAbo, ComponentClass.RedBloodCells, allowedAbo: "O");

        await using var ctx = _factory.Create();
        var issued = await Issuing(ctx).IssueUnitAsync(IssueReq(plasma));
        Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
    }

    // TEST-BB-412: on emergency release an unmet modification needs the authorized override, which is recorded.
    [Fact]
    public async Task Issue_UnmetModification_OnEmergencyRelease_IssuesUnderTheRecordedOverride()
    {
        var s = await SeedAsync("IRRAD-EMERG");
        await AllocateAsync(s);
        await RequireAsync(s.PatientId, "IRR-EM", SpecialRequirementCategory.UnitModification,
            SpecialRequirementEnforcementKind.RequireProductAttribute, ComponentClass.RedBloodCells, attribute: "IRRAD");

        await using (var c = _factory.Create())
        {
            var routine = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.Contains(routine.Evaluation!.HardStops, r => r.Code == IssueGate.SpecialReqCode);

            var withoutOverride = await Issuing(c).IssueUnitAsync(IssueReq(s, IssueType.EmergencyRelease));
            Assert.False(withoutOverride.Succeeded);
            Assert.True(withoutOverride.RequiresOverride);
            Assert.Contains(withoutOverride.Evaluation!.Results, r =>
                r.Code == IssueGate.SpecialReqCode && r.Severity == RuleSeverity.Warning);
        }

        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(
                s, IssueType.EmergencyRelease,
                overrideReason: "Exsanguinating, no irradiated units on the shelf", authorizedBy: "dr-authorizer"));
            Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
        }

        await using var verify = _factory.Create();
        var ovr = await verify.Overrides.Where(o => o.ContextType == nameof(Issue) && o.ContextId == s.UnitId).SingleAsync();
        Assert.Contains(IssueGate.SpecialReqCode, ovr.RuleCode);
    }

    // TEST-BB-412: a CMV-negative typing is met by a negative CMV test recorded on the unit.
    [Fact]
    public async Task Issue_CmvNegativeTyping_IsMetByANegativeTestRecordedOnTheUnit()
    {
        var s = await SeedAsync("CMV-TEST");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        await RequireAsync(s.PatientId, "CMV-T", SpecialRequirementCategory.UnitTyping,
            SpecialRequirementEnforcementKind.RequireUnitTestNegative, ComponentClass.RedBloodCells,
            attribute: "CMVNEG", unitTest: "CMV");

        await using (var c = _factory.Create())
        {
            var refused = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.Contains(refused.Evaluation!.HardStops, r => r.Code == IssueGate.SpecialReqCode);
            c.BloodComponentSpecialTests.Add(new BloodComponentSpecialTest { BloodProductId = s.UnitId, TestCode = "cmv", Result = "Negative" });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var issued = await Issuing(ctx).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.HardStops.Select(r => r.Message) ?? []));
    }

    private Hl7OutboundService Outbound(BloodBankDbContext c) =>
        new(
            new EfRepository<TestResult>(c),
            new EfRepository<Patient>(c),
            new EfRepository<Hl7MessageLog>(c),
            c,
            _factory.Clock,
            endpoints: new EfRepository<InterfaceEndpoint>(c),
            issues: new EfRepository<Issue>(c),
            units: new EfRepository<BloodUnit>(c),
            productTypes: new EfRepository<ProductType>(c),
            orders: new EfRepository<Order>(c));

    // TEST-BB-417: issue and return tell the EHR (BPS^O29) only while an outbound blood product interface is enabled.
    [Fact]
    public async Task IssueAndReturn_QueueADispenseMessage_OnlyWithAnEnabledBloodProductInterface()
    {
        var quiet = await SeedAsync("BPS-OFF");
        await AllocateAsync(quiet);
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c, dispense: Outbound(c)).IssueUnitAsync(IssueReq(quiet, IssueType.EmergencyRelease,
                overrideReason: "Bleeding, interface test", authorizedBy: "dr-authorizer"));
            Assert.True(issued.Succeeded, issued.Error);
            Assert.False(await c.Hl7Messages.AnyAsync(m => m.MessageType == "BPS" && m.MessageControlId.EndsWith(issued.Value!.Id.ToString())));
        }

        long endpointId;
        await using (var c = _factory.Create())
        {
            var endpoint = new InterfaceEndpoint
            {
                Name = "EHR blood product status",
                Direction = Hl7Direction.Outbound,
                InterfaceType = InterfaceType.Bpam,
                MessageTypes = "BPS",
                IsEnabled = true
            };
            c.InterfaceEndpoints.Add(endpoint);
            await c.SaveChangesAsync();
            endpointId = endpoint.Id;
        }

        try
        {
            var s = await SeedAsync("BPS-ON");
            await AllocateAsync(s);
            long issueId;
            await using (var c = _factory.Create())
            {
                var issued = await Issuing(c, dispense: Outbound(c)).IssueUnitAsync(IssueReq(s, IssueType.EmergencyRelease,
                    overrideReason: "Bleeding, interface test", authorizedBy: "dr-authorizer"));
                Assert.True(issued.Succeeded, issued.Error);
                issueId = issued.Value!.Id;
            }

            await using (var c = _factory.Create())
            {
                var returned = await Issuing(c, dispense: Outbound(c)).ReturnUnitAsync(issueId, new ReturnUnitRequest(
                    "Not needed; cooler intact", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
                Assert.True(returned.Succeeded, returned.Error);
            }

            await using var verify = _factory.Create();
            var messages = await verify.Hl7Messages
                .Where(m => m.MessageType == "BPS" && m.EndpointId == endpointId)
                .OrderBy(m => m.Id)
                .ToListAsync();
            Assert.Equal(2, messages.Count);
            Assert.All(messages, m => Assert.Equal(Hl7Direction.Outbound, m.Direction));
            Assert.Contains("BPX|1|DS^", messages[0].RawMessage);
            Assert.Contains("U-BPS-ON", messages[0].RawMessage);
            Assert.Contains("BPX|1|RA^", messages[1].RawMessage);
        }
        finally
        {
            await using var c = _factory.Create();
            var endpoint = await c.InterfaceEndpoints.FindAsync(endpointId);
            endpoint!.IsEnabled = false;
            await c.SaveChangesAsync();
        }
    }

    // TEST-BB-425: a reservation past its end time is still listed, with that time, until it is released with a reason.
    [Fact]
    public async Task LapsedReservation_StaysOnReadyToIssue_UntilReleasedWithAReason()
    {
        var s = await SeedAsync("RES-LAPSED");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        long allocationId;
        await using (var c = _factory.Create())
        {
            var allocation = await c.Allocations.SingleAsync(a => a.BloodProductId == s.UnitId && a.Status == AllocationStatus.Reserved);
            allocation.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-2);
            await c.SaveChangesAsync();
            allocationId = allocation.Id;
        }

        await using (var c = _factory.Create())
        {
            var row = (await Issuing(c).ListReadyToIssueAllocationsAsync()).Single(r => r.AllocationId == allocationId);
            Assert.Equal(_factory.Clock.UtcNow.AddHours(-2), row.ReservationExpiresUtc);
            Assert.True(ReadyToIssueAttentionRule.ReservationLapsed(row, _factory.Clock.UtcNow));

            // TEST-BB-429: the shift handoff counts it until it is released.
            var handoff = await new ShiftHandoffService(
                    new EfRepository<ShiftHandoff>(c), c, _factory.Clock, _factory.CurrentUser, issuing: Issuing(c))
                .CreateAsync(new CreateShiftHandoffRequest("Night shift: one reservation past its end"));
            Assert.True(handoff.Succeeded, handoff.Error);
            Assert.True(handoff.Value!.ReservationsLapsed >= 1);

            // TEST-BB-431: so does the downtime snapshot.
            var snapshot = await new DowntimeReconciliationService(
                    new EfRepository<InterfaceErrorQueueItem>(c), new EfRepository<Hl7MessageLog>(c), new EfRepository<Issue>(c),
                    new BloodBankLIS.Infrastructure.Audit.AuditQuery(c), _factory.Clock, _factory.CurrentUser, issuing: Issuing(c))
                .GetSnapshotAsync();
            Assert.True(snapshot.Succeeded, snapshot.Error);
            Assert.True(snapshot.Value!.ReservationsLapsed >= 1);

            var noReason = await Compatibility(c).ReleaseAllocationAsync(allocationId, " ");
            Assert.False(noReason.Succeeded);
            var released = await Compatibility(c).ReleaseAllocationAsync(allocationId, "Reservation ended, transfusion not needed");
            Assert.True(released.Succeeded, released.Error);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
        Assert.DoesNotContain(await Issuing(verify).ListReadyToIssueAllocationsAsync(), r => r.AllocationId == allocationId);
    }

    [Fact]
    public async Task Issue_ExpiredSpecialRequirement_IsNotEnforced()
    {
        var s = await SeedAsync("SREXP");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using (var c = _factory.Create())
        {
            c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
            {
                PatientId = s.PatientId,
                RequirementType = SpecialTransfusionRequirementType.Irradiated,
                Reason = "Ended",
                EffectiveUtc = _factory.Clock.UtcNow.AddDays(-10),
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(-1),
                IsActive = true,
                EnteredBy = "tech-test"
            });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var issued = await Issuing(ctx).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Succeeded, issued.Error);
    }

    [Fact]
    public async Task Issue_WarmerWithoutAck_IsHardStopped()
    {
        var s = await SeedAsync("WARMNO");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using (var c = _factory.Create())
        {
            c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
            {
                PatientId = s.PatientId,
                RequirementType = SpecialTransfusionRequirementType.Other,
                Reason = "Use warmer",
                EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
                IsActive = true,
                EnteredBy = "tech-test"
            });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var issued = await Issuing(ctx).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Evaluation!.IsHardStopped);
        Assert.Contains(issued.Evaluation.HardStops, r => r.Code == IssueGate.SpecialReqCode);
    }

    [Fact]
    public async Task Issue_WarmerWithAck_Succeeds()
    {
        var s = await SeedAsync("WARMOK");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using (var c = _factory.Create())
        {
            c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
            {
                PatientId = s.PatientId,
                RequirementType = SpecialTransfusionRequirementType.Other,
                Reason = "Use warmer",
                EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
                IsActive = true,
                EnteredBy = "tech-test"
            });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var issued = await Issuing(ctx).IssueUnitAsync(
            IssueReq(s) with { AcknowledgedSpecialRequirementCodes = [SpecialRequirementCatalog.Other] });
        Assert.True(issued.Succeeded, issued.Error);
        Assert.Equal(SpecialRequirementCatalog.Other, issued.Value!.AcknowledgedSpecialRequirementCodes);
    }

    [Fact]
    public async Task Issue_TypeA2WithoutSubgroup_IsHardStopped()
    {
        var s = await SeedAsync("A2MISS", patientAbo: AboGroup.A, unitAbo: AboGroup.A);
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using (var c = _factory.Create())
        {
            c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
            {
                PatientId = s.PatientId,
                RequirementDefinitionId = null,
                RequirementType = SpecialTransfusionRequirementType.Other,
                Reason = "Type for A2",
                EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
                IsActive = true,
                EnteredBy = "tech-test"
            });
            var def = new SpecialRequirementDefinition
            {
                Code = SpecialRequirementCatalog.TypeForA2,
                Name = "Type for A2",
                Level = SpecialRequirementLevel.Patient,
                EnforcementKind = SpecialRequirementEnforcementKind.RequireAboSubgroup,
                IsActive = true,
                IsDraft = false,
                Version = 1
            };
            c.SpecialRequirementDefinitions.Add(def);
            await c.SaveChangesAsync();
            var row = await c.SpecialTransfusionRequirements.SingleAsync(r => r.PatientId == s.PatientId);
            row.RequirementDefinitionId = def.Id;
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var issued = await Issuing(ctx).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Evaluation!.IsHardStopped);
        Assert.Contains(issued.Evaluation.HardStops, r => r.Code == IssueGate.SpecialReqCode);
    }

    [Fact]
    public async Task Issue_TypeA2WithSubgroup_Succeeds()
    {
        var s = await SeedAsync("A2OK", patientAbo: AboGroup.A, unitAbo: AboGroup.A);
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using (var c = _factory.Create())
        {
            var current = await c.PatientBloodTypeHistory.SingleAsync(h => h.PatientId == s.PatientId && h.IsCurrent);
            current.AboSubgroup = AboSubgroup.A2;
            var def = new SpecialRequirementDefinition
            {
                Code = SpecialRequirementCatalog.TypeForA2,
                Name = "Type for A2",
                Level = SpecialRequirementLevel.Patient,
                EnforcementKind = SpecialRequirementEnforcementKind.RequireAboSubgroup,
                IsActive = true,
                IsDraft = false,
                Version = 1
            };
            c.SpecialRequirementDefinitions.Add(def);
            await c.SaveChangesAsync();
            c.SpecialTransfusionRequirements.Add(new SpecialTransfusionRequirement
            {
                PatientId = s.PatientId,
                RequirementDefinitionId = def.Id,
                RequirementType = SpecialTransfusionRequirementType.Other,
                Reason = "Type for A2",
                EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
                IsActive = true,
                EnteredBy = "tech-test"
            });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var issued = await Issuing(ctx).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Succeeded, issued.Error);
    }

    // TEST-BB-701: finishing a transfusion without saying whether a reaction is suspected is refused (documenting or
    // completing), and nothing is recorded; "no reaction" is never assumed.
    [Fact]
    public async Task Transfusion_WithoutReactionAnswer_IsRefused()
    {
        var s = await SeedAsync("TX-NOANS");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.True(issued.Succeeded, issued.Error);
            issueId = issued.Value!.Id;
        }

        await using var ctx = _factory.Create();
        var documented = await Issuing(ctx).DocumentTransfusionAsync(issueId, TxReq(s) with { ReactionSuspected = null });
        Assert.False(documented.Succeeded);
        Assert.Equal(IssuingService.ReactionAnswerRequired, documented.Error);
        var completed = await Issuing(ctx).CompleteTransfusionAsync(issueId, new CompleteTransfusionRequest(TransfusionDisposition.Completed));
        Assert.False(completed.Succeeded);
        Assert.Equal(IssuingService.ReactionAnswerRequired, completed.Error);
        Assert.False(await ctx.TransfusionEvents.AnyAsync(t => t.IssueId == issueId));
        Assert.Equal(UnitStatus.Issued, (await ctx.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    // TEST-BB-703: finishing a transfusion without saying how it ended is refused (documenting or completing), and
    // nothing is recorded; Completed is never assumed.
    [Fact]
    public async Task Transfusion_WithoutDisposition_IsRefused()
    {
        var s = await SeedAsync("TX-NODISP");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.True(issued.Succeeded, issued.Error);
            issueId = issued.Value!.Id;
        }

        await using var ctx = _factory.Create();
        var documented = await Issuing(ctx).DocumentTransfusionAsync(issueId, TxReq(s) with { FinalDisposition = null });
        Assert.False(documented.Succeeded);
        Assert.Equal(IssuingService.DispositionRequired, documented.Error);
        var completed = await Issuing(ctx).CompleteTransfusionAsync(issueId, new CompleteTransfusionRequest(null, ReactionSuspected: false));
        Assert.False(completed.Succeeded);
        Assert.Equal(IssuingService.DispositionRequired, completed.Error);
        Assert.False(await ctx.TransfusionEvents.AnyAsync(t => t.IssueId == issueId));
        Assert.Equal(UnitStatus.Issued, (await ctx.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    [Fact]
    public async Task Transfusion_ReactionSuspected_OpensInvestigation()
    {
        var s = await SeedAsync("RXN");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.True(issued.Succeeded);
            issueId = issued.Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
            var tx = await Issuing(c).DocumentTransfusionAsync(
                issueId, TxReq(s, reactionSuspected: true));
            Assert.True(tx.Succeeded);
        }

        await using var verify = _factory.Create();
        Assert.True(await verify.ReactionInvestigations.AnyAsync(r => r.PatientId == s.PatientId));
    }

    [Fact]
    public async Task Transfusion_ReactionStopped_QuarantinesRemainder()
    {
        var s = await SeedAsync("RXNQ");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.True(issued.Succeeded);
            issueId = issued.Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
            var tx = await Issuing(c).DocumentTransfusionAsync(
                issueId, TxReq(s, TransfusionDisposition.Stopped, reactionSuspected: true));
            Assert.True(tx.Succeeded);
        }

        await using var verify = _factory.Create();
        var unit = await verify.BloodUnits.FindAsync(s.UnitId);
        Assert.Equal(UnitStatus.Quarantine, unit!.Status);
        var inv = await verify.ReactionInvestigations.SingleAsync(r => r.PatientId == s.PatientId);
        Assert.True(inv.RemainderQuarantined);
    }

    [Fact]
    public async Task Transfusion_ReactionCompleted_QuarantinesRemainder()
    {
        var s = await SeedAsync("RXNCMP");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.True(issued.Succeeded);
            issueId = issued.Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
            var tx = await Issuing(c).DocumentTransfusionAsync(
                issueId, TxReq(s, TransfusionDisposition.Completed, reactionSuspected: true));
            Assert.True(tx.Succeeded, tx.Error);
        }

        await using var verify = _factory.Create();
        var unit = await verify.BloodUnits.FindAsync(s.UnitId);
        Assert.Equal(UnitStatus.Quarantine, unit!.Status);
        Assert.Equal(UnitQuarantineReason.ReactionRemainder, unit.QuarantineReasonCode);
        var inv = await verify.ReactionInvestigations.SingleAsync(r => r.PatientId == s.PatientId);
        Assert.True(inv.RemainderQuarantined);
    }

    // TEST-BB-402: a reaction noticed after the transfusion was documented without one opens an investigation, marks
    // the transfusion (audited with its earlier value), and is refused when it cannot be valid.
    [Fact]
    public async Task LateReactionReport_OpensAnInvestigation_AndMarksTheTransfusion()
    {
        var s = await SeedAsync("RXNLATE");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with
            {
                IssuedUtc = _factory.Clock.UtcNow.AddHours(-10), LateEntryReason = "Entered after the shift"
            });
            Assert.True(issued.Succeeded, issued.Error);
            issueId = issued.Value!.Id;
        }

        ReactionInvestigationService Reactions(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
            new(new EfRepository<ReactionInvestigation>(c), new EfRepository<TransfusionEvent>(c), new InventoryRepository(c),
                c, _factory.Clock, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
                permissions: permissions ?? new FixedPermissionEvaluator(1, PermissionCodes.TransfusionDocument));

        await using (var c = _factory.Create())
        {
            // Not yet transfused: nothing to report against.
            var early = await Reactions(c).ReportLateAsync(new ReportLateReactionRequest(issueId, "Fever"));
            Assert.Contains("Document the transfusion first", early.Error);

            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
            var tx = await Issuing(c).DocumentTransfusionAsync(issueId, TxReq(s, TransfusionDisposition.Completed) with
            {
                StartUtc = _factory.Clock.UtcNow.AddHours(-8),
                StopUtc = _factory.Clock.UtcNow.AddHours(-6)
            });
            Assert.True(tx.Succeeded, tx.Error ?? string.Join("; ", tx.Evaluation?.HardStops.Select(r => r.Code + " " + r.Message) ?? []));
            Assert.False(await c.ReactionInvestigations.AnyAsync(r => r.PatientId == s.PatientId));
        }

        await using (var c = _factory.Create())
        {
            var noRole = await Reactions(c, new FixedPermissionEvaluator(1, PermissionCodes.IssueCreate))
                .ReportLateAsync(new ReportLateReactionRequest(issueId, "Fever and chills"));
            Assert.Contains(LateReactionReportRule.Code, noRole.Error);
            var blank = await Reactions(c).ReportLateAsync(new ReportLateReactionRequest(issueId, " "));
            Assert.Contains("Describe the reaction", blank.Error);
            var beforeStart = await Reactions(c).ReportLateAsync(new ReportLateReactionRequest(issueId, "Fever", _factory.Clock.UtcNow.AddHours(-9)));
            Assert.Contains("before the transfusion started", beforeStart.Error);
            var future = await Reactions(c).ReportLateAsync(new ReportLateReactionRequest(issueId, "Fever", _factory.Clock.UtcNow.AddHours(2)));
            Assert.Contains("in the future", future.Error);
            Assert.False(await c.ReactionInvestigations.AnyAsync(r => r.PatientId == s.PatientId));

            var reported = await Reactions(c).ReportLateAsync(new ReportLateReactionRequest(
                issueId, "Fever 39.2 C and chills; reported by RN Lee", _factory.Clock.UtcNow.AddHours(-2), "FNHTR"));
            Assert.True(reported.Succeeded, reported.Error);
            Assert.True(reported.Value!.ReportedAfterTransfusion);
            Assert.Equal(_factory.Clock.UtcNow.AddHours(-2), reported.Value.OnsetUtc);
            Assert.StartsWith("Reported after the transfusion:", reported.Value.Findings);

            var again = await Reactions(c).ReportLateAsync(new ReportLateReactionRequest(issueId, "Still febrile"));
            Assert.Contains($"investigation {reported.Value.Id} is already open", again.Error);
        }

        await using var verify = _factory.Create();
        var transfusion = await verify.TransfusionEvents.SingleAsync(t => t.IssueId == issueId);
        Assert.True(transfusion.ReactionSuspected);
        var flagAudit = await verify.AuditEvents.SingleAsync(a => a.EntityType == nameof(TransfusionEvent) && a.EntityId == transfusion.Id
            && a.Reason == "Reaction reported after the transfusion was documented.");
        Assert.Contains("\"ReactionSuspected\":false", flagAudit.OldValueJson);
        Assert.Single(await verify.ReactionInvestigations.Where(r => r.TransfusionEventId == transfusion.Id).ToListAsync());
    }

    [Fact]
    public async Task CloseInvestigation_WithoutWorkup_Fails()
    {
        var s = await SeedAsync("RXNC");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
            await Issuing(c).DocumentTransfusionAsync(
                issueId, TxReq(s, reactionSuspected: true));
        }

        await using var ctx = _factory.Create();
        var inv = await ctx.ReactionInvestigations.SingleAsync(r => r.PatientId == s.PatientId);
        var audit = new AuditWriter(ctx, _factory.Clock, _factory.CurrentUser);
        var service = new ReactionInvestigationService(
            new EfRepository<ReactionInvestigation>(ctx),
            new EfRepository<TransfusionEvent>(ctx),
            new InventoryRepository(ctx),
            ctx, _factory.Clock, _factory.CurrentUser, audit,
            specimens: new EfRepository<Specimen>(ctx), orders: new EfRepository<Order>(ctx),
            orderLines: new EfRepository<OrderLine>(ctx), testResults: new EfRepository<TestResult>(ctx));

        var closed = await service.UpdateAsync(inv.Id, new UpdateReactionInvestigationRequest(
            null, null, null, null, null, null, null, null, ReactionInvestigationStatus.Closed, 1, ConcludedReactionCode: "NONE", TreatmentCodes: ["NONE"]));
        Assert.False(closed.Succeeded);
        Assert.Contains(ReactionWorkupCompletenessRule.Code, closed.Error);

        var saved = await service.UpdateAsync(inv.Id, new UpdateReactionInvestigationRequest(
            "FNHTR", ReactionSeverity.Mild, "Fever 1.8C", "Non-hemolytic", null, "Continue observation",
            false, false, ReactionInvestigationStatus.UnderReview, null,
            true, "IDs and ABO concordant", true, true, "A Positive", "O Positive",
            DatWorkupResult.Negative, null, true));
        Assert.True(saved.Succeeded);

        // The checklist alone no longer closes the case: the specimen workup must be resulted and verified too.
        var withoutSpecimens = await service.UpdateAsync(inv.Id, new UpdateReactionInvestigationRequest(
            null, null, null, null, null, null, null, null, ReactionInvestigationStatus.Closed, 1, ConcludedReactionCode: "NONE", TreatmentCodes: ["NONE"]));
        Assert.False(withoutSpecimens.Succeeded);
        Assert.Contains("pre-transfusion specimen", withoutSpecimens.Error);
        await ReactionWorkupTestData.CompleteAsync(ctx, inv.Id, _factory.Clock.UtcNow);

        var closeOk = await service.UpdateAsync(inv.Id, new UpdateReactionInvestigationRequest(
            null, null, null, null, null, null, null, null, ReactionInvestigationStatus.Closed, 1, ConcludedReactionCode: "NONE", TreatmentCodes: ["NONE"]));
        Assert.True(closeOk.Succeeded);
        Assert.Equal(ReactionInvestigationStatus.Closed, closeOk.Value!.Status);
    }

    [Fact]
    public async Task CellularIssue_WithoutSecondAbo_IsHardStopped()
    {
        var s = await SeedAsync("ISS1ABO", includeHistoricalAbo: false);
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
        Assert.False(issued.Succeeded);
        Assert.Contains(issued.Evaluation!.HardStops, r => r.Code == SecondAboDeterminationRule.IssueCode);
    }

    // TEST-BB-874 (OCD-074 part two): an infant whose only ABO/Rh was tested on this admission's specimen (one made valid
    // for the admission under the neonatal rules) is issued red cells without a second determination, and is not on the
    // Second ABO/Rh desk; the same type on an ordinary specimen still needs the second determination.
    [Fact]
    public async Task Infant_AdmissionType_StandsWithoutASecondDetermination()
    {
        var newborn = DateOnly.FromDateTime(_factory.Clock.UtcNow).AddDays(-10);
        var infant = await SeedAsync("NEO-ABO", includeHistoricalAbo: false, dateOfBirth: newborn);
        var ordinary = await SeedAsync("NEO-ABO-ORD", includeHistoricalAbo: false, dateOfBirth: newborn);
        await using (var c = _factory.Create())
        {
            foreach (var (scenario, admission) in new[] { (infant, true), (ordinary, false) })
            {
                var specimen = await c.Specimens.SingleAsync(x => x.Id == scenario.SpecimenId);
                if (admission)
                {
                    specimen.NeonatalStandardExpiresUtc = specimen.ExpiresUtc;
                    specimen.ExpiresUtc = _factory.Clock.UtcNow.AddDays(30);
                }

                (await c.PatientBloodTypeHistory.SingleAsync(h => h.PatientId == scenario.PatientId && h.IsCurrent)).SpecimenId = specimen.Id;
            }

            await c.SaveChangesAsync();
        }

        foreach (var scenario in new[] { infant, ordinary })
        {
            await RecordCompatibleCrossmatchAsync(scenario);
            await AllocateAsync(scenario);
        }

        await using (var c = _factory.Create())
        {
            var desk = await Issuing(c).ListPendingSecondAboAsync();
            Assert.DoesNotContain(desk, d => d.PatientId == infant.PatientId);
            Assert.Contains(desk, d => d.PatientId == ordinary.PatientId);
        }

        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(infant));
            Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.Results.Where(r => r.Severity != RuleSeverity.Pass).Select(r => r.Message) ?? []));

            var refused = await Issuing(c).IssueUnitAsync(IssueReq(ordinary));
            Assert.Contains(refused.Evaluation!.HardStops, r => r.Code == SecondAboDeterminationRule.IssueCode);
        }
    }

    // TEST-BB-877 (OCD-074 part three): an infant whose type stands for the admission, with a current verified negative
    // screen, is issued group O red cells without a crossmatch, and the issue is marked; the same infant needs a
    // crossmatch for a group A unit (OCD-082), and an infant on an ordinary specimen still needs one.
    [Fact]
    public async Task Infant_NegativeScreen_GroupO_IssuedWithoutCrossmatch()
    {
        var newborn = DateOnly.FromDateTime(_factory.Clock.UtcNow).AddDays(-10);
        var groupO = await SeedAsync("NEO-XM-O", patientAbo: AboGroup.A, includeHistoricalAbo: false, dateOfBirth: newborn);
        var groupA = await SeedAsync("NEO-XM-A", patientAbo: AboGroup.A, unitAbo: AboGroup.A, includeHistoricalAbo: false, dateOfBirth: newborn);
        var ordinary = await SeedAsync("NEO-XM-ORD", patientAbo: AboGroup.A, includeHistoricalAbo: false, dateOfBirth: newborn);
        await using (var c = _factory.Create())
        {
            foreach (var (scenario, admission) in new[] { (groupO, true), (groupA, true), (ordinary, false) })
            {
                var specimen = await c.Specimens.SingleAsync(x => x.Id == scenario.SpecimenId);
                if (admission)
                {
                    specimen.NeonatalStandardExpiresUtc = specimen.ExpiresUtc;
                    specimen.ExpiresUtc = _factory.Clock.UtcNow.AddDays(30);
                }

                (await c.PatientBloodTypeHistory.SingleAsync(h => h.PatientId == scenario.PatientId && h.IsCurrent)).SpecimenId = specimen.Id;
                await ExmScreenSeed.AddVerifiedNegativeScreenAsync(c, scenario.PatientId, _factory.Clock.UtcNow, scenario.SpecimenId);
            }

            await c.SaveChangesAsync();
        }

        foreach (var scenario in new[] { groupO, groupA, ordinary })
        {
            await AllocateAsync(scenario);
        }

        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(groupO));
            Assert.True(issued.Succeeded, issued.Error ?? string.Join("; ", issued.Evaluation?.Results.Where(r => r.Severity != RuleSeverity.Pass).Select(r => r.Message) ?? []));
            Assert.True(issued.Value!.NeonatalNoCrossmatch);
            Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(BloodUnit) && a.EntityId == groupO.UnitId
                && a.NewValueJson!.Contains("\"NeonatalNoCrossmatch\":true")));

            var nonO = await Issuing(c).IssueUnitAsync(IssueReq(groupA));
            Assert.Contains(nonO.Evaluation!.HardStops, r => r.Code == CrossmatchValidityRule.Code);
            var notAdmission = await Issuing(c).IssueUnitAsync(IssueReq(ordinary));
            Assert.Contains(notAdmission.Evaluation!.HardStops, r => r.Code == CrossmatchValidityRule.Code);
        }
    }

    [Fact]
    public async Task EmergencyRelease_WithoutSecondAbo_RequiresOverride()
    {
        var s = await SeedAsync("EM1ABO", includeHistoricalAbo: false);
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var blocked = await Issuing(c).IssueUnitAsync(IssueReq(s, IssueType.EmergencyRelease));
        Assert.False(blocked.Succeeded);
        Assert.True(blocked.RequiresOverride);
        Assert.Contains(blocked.Evaluation!.Warnings, r => r.Code == SecondAboDeterminationRule.IssueCode);

        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.EmergencyRelease,
            overrideReason: "Uncrossmatched O units; second type pending", authorizedBy: "dr-authorizer"));
        Assert.True(issued.Succeeded);
        Assert.Contains(issued.Evaluation!.Warnings, r => r.Code == SecondAboDeterminationRule.IssueCode);
    }

    [Fact]
    public async Task EmergencyRelease_NonO_Unit_WarnsGroupO()
    {
        var s = await SeedAsync("EM-A", patientAbo: AboGroup.A, unitAbo: AboGroup.A);
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var blocked = await Issuing(c).IssueUnitAsync(IssueReq(s, IssueType.EmergencyRelease));
        Assert.True(blocked.RequiresOverride);
        Assert.Contains(blocked.Evaluation!.Warnings, r => r.Code == EmergencyUncrossmatchedAboRule.AboCode);
    }

    [Fact]
    public async Task EmergencyRelease_OPos_ToChildbearingFemale_WarnsRh()
    {
        var s = await SeedAsync(
            "EM-F",
            includeCurrentAbo: false,
            includeHistoricalAbo: false,
            unitRh: RhType.Positive,
            sex: Sex.Female,
            dateOfBirth: new DateOnly(2001, 1, 15));
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var blocked = await Issuing(c).IssueUnitAsync(IssueReq(s, IssueType.EmergencyRelease));
        Assert.True(blocked.RequiresOverride);
        Assert.Contains(blocked.Evaluation!.Warnings, r => r.Code == EmergencyUncrossmatchedAboRule.RhCode);
    }

    [Fact]
    public async Task EmergencyRelease_UnknownPatientType_RequiresOverride()
    {
        var s = await SeedAsync("EM-UNK", includeCurrentAbo: false, includeHistoricalAbo: false);
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var blocked = await Issuing(c).IssueUnitAsync(IssueReq(s, IssueType.EmergencyRelease));
        Assert.True(blocked.RequiresOverride);
        Assert.Contains(blocked.Evaluation!.Warnings, r => r.Code == IssueGate.PatientAboRhCode);

        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.EmergencyRelease,
            overrideReason: "Trauma, untyped recipient, group O issued", authorizedBy: "dr-authorizer"));
        Assert.True(issued.Succeeded);
    }

    [Fact]
    public async Task SecondAboWorklist_ListsReservedPatientsMissingSecondType()
    {
        var missing = await SeedAsync("WL-MISS", includeHistoricalAbo: false);
        var ready = await SeedAsync("WL-OK");
        await AllocateAsync(missing);
        await AllocateAsync(ready);

        await using var c = _factory.Create();
        var items = await Issuing(c).ListPendingSecondAboAsync();
        Assert.Contains(items, i => i.PatientId == missing.PatientId);
        Assert.DoesNotContain(items, i => i.PatientId == ready.PatientId);
    }

    // TEST-BB-728: the Second ABO/Rh board names an open ABO/Rh order (line not yet verified, order not closed); a
    // verified line, a cancelled order, or another test leaves the patient with nothing ordered.
    [Fact]
    public async Task SecondAboWorklist_NamesAnOpenAboRhOrder()
    {
        var open = await SeedAsync("WL-ABO-OPEN", includeHistoricalAbo: false);
        var verified = await SeedAsync("WL-ABO-VER", includeHistoricalAbo: false);
        var cancelled = await SeedAsync("WL-ABO-CANC", includeHistoricalAbo: false);
        var other = await SeedAsync("WL-ABO-OTH", includeHistoricalAbo: false);
        foreach (var s in new[] { open, verified, cancelled, other })
        {
            await AllocateAsync(s);
        }

        var now = _factory.Clock.UtcNow;
        await using (var c = _factory.Create())
        {
            var location = new OrderingLocation { Code = "LOC-WL-ABO", Name = "ED", IsActive = true };
            c.OrderingLocations.Add(location);
            await c.SaveChangesAsync();
            async Task OrderAsync(Scenario s, string number, OrderStatus status, OrderType lineType, ResultStatus? lineResult)
            {
                var encounter = new Encounter { PatientId = s.PatientId, VisitNumber = $"VIS-{number}", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = now.AddDays(-1) };
                c.Encounters.Add(encounter);
                await c.SaveChangesAsync();
                var order = new Order
                {
                    OrderNumber = number, PatientId = s.PatientId, EncounterId = encounter.Id, OrderingLocationId = location.Id,
                    OrderCategory = OrderCategory.Test, OrderName = "ABO/Rh", OrderType = OrderType.AboRh, Priority = OrderPriority.Routine,
                    Status = status, Source = OrderSource.Manual, OrderedUtc = now.AddHours(-1)
                };
                c.Orders.Add(order);
                await c.SaveChangesAsync();
                c.Set<OrderLine>().Add(new OrderLine
                {
                    OrderId = order.Id, LineNumber = 1, LineName = "ABO/Rh", TestCode = lineType == OrderType.AboRh ? "ABORH" : "ABSC",
                    OrderType = lineType, ResultStatus = lineResult
                });
                await c.SaveChangesAsync();
            }

            await OrderAsync(open, "ORD-WL-OPEN", OrderStatus.Received, OrderType.AboRh, ResultStatus.Entered);
            await OrderAsync(verified, "ORD-WL-VER", OrderStatus.InProcess, OrderType.AboRh, ResultStatus.Verified);
            await OrderAsync(cancelled, "ORD-WL-CANC", OrderStatus.Cancelled, OrderType.AboRh, null);
            await OrderAsync(other, "ORD-WL-OTH", OrderStatus.New, OrderType.AntibodyScreen, null);
        }

        await using var read = _factory.Create();
        var items = await Issuing(read).ListPendingSecondAboAsync();
        Assert.Equal("ORD-WL-OPEN", items.Single(i => i.PatientId == open.PatientId).AboOrderNumber);
        Assert.Null(items.Single(i => i.PatientId == verified.PatientId).AboOrderNumber);
        Assert.Null(items.Single(i => i.PatientId == cancelled.PatientId).AboOrderNumber);
        Assert.Null(items.Single(i => i.PatientId == other.PatientId).AboOrderNumber);
    }

    // TEST-BB-652: a reserved unit carries the needed-by time of the open order it is reserved for, marked passed or needed
    // soon (OCD-075 window), and among units that can be issued now the earliest needed-by comes first; a unit reserved
    // without an order, or for a cancelled one, has no time.
    [Fact]
    public async Task ListReadyToIssue_ShowsWhenTheOrderIsNeeded_EarliestFirst()
    {
        var soon = await SeedAsync("NBY-SOON");
        var late = await SeedAsync("NBY-LATE");
        var none = await SeedAsync("NBY-NONE");
        var cancelled = await SeedAsync("NBY-CANC");
        var far = await SeedAsync("NBY-FAR");
        foreach (var s in new[] { none, soon, late, cancelled, far })
        {
            await AllocateAsync(s);
        }

        var now = _factory.Clock.UtcNow;
        await using (var c = _factory.Create())
        {
            var location = new OrderingLocation { Code = "LOC-RDY-NB", Name = "OR", IsActive = true };
            c.OrderingLocations.Add(location);
            await c.SaveChangesAsync();
            async Task ForOrderAsync(Scenario s, string number, DateTime neededBy, OrderStatus status = OrderStatus.InProcess)
            {
                var encounter = new Encounter { PatientId = s.PatientId, VisitNumber = $"VIS-{number}", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = now.AddDays(-1) };
                c.Encounters.Add(encounter);
                await c.SaveChangesAsync();
                var order = new Order
                {
                    OrderNumber = number, PatientId = s.PatientId, EncounterId = encounter.Id, OrderingLocationId = location.Id,
                    OrderCategory = OrderCategory.Product, OrderName = "Red cells", OrderType = OrderType.Other, Priority = OrderPriority.Routine,
                    Status = status, Source = OrderSource.Manual, OrderedUtc = now.AddHours(-2), NeededByUtc = neededBy
                };
                c.Orders.Add(order);
                await c.SaveChangesAsync();
                var allocation = await c.Allocations.SingleAsync(a => a.BloodProductId == s.UnitId && a.Status == AllocationStatus.Reserved);
                allocation.OrderId = order.Id;
                await c.SaveChangesAsync();
            }

            await ForOrderAsync(soon, "ORD-RDY-SOON", now.AddMinutes(20));
            await ForOrderAsync(late, "ORD-RDY-LATE", now.AddMinutes(-5));
            await ForOrderAsync(cancelled, "ORD-RDY-CANC", now.AddMinutes(-30), OrderStatus.Cancelled);
            // Needed after the specimen (2 days) expires.
            await ForOrderAsync(far, "ORD-RDY-FAR", now.AddDays(3));
            c.SystemSettings.Add(new SystemSetting { Key = FacilityPolicyKeys.BloodOrderNeededBySoonMinutes, Value = "60" });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var rows = (await Issuing(ctx).ListReadyToIssueAllocationsAsync())
            .Where(r => new[] { soon.UnitId, late.UnitId, none.UnitId, cancelled.UnitId, far.UnitId }.Contains(r.BloodUnitId)).ToList();
        var soonRow = rows.Single(r => r.BloodUnitId == soon.UnitId);
        Assert.Equal(("ORD-RDY-SOON", true, false, 20), (soonRow.OrderNumber, soonRow.NeededBySoon, soonRow.NeededByPassed, soonRow.NeededInMinutes ?? 0));
        var lateRow = rows.Single(r => r.BloodUnitId == late.UnitId);
        Assert.Equal((true, false, (int?)null), (lateRow.NeededByPassed, lateRow.NeededBySoon, lateRow.NeededInMinutes));
        Assert.Null(rows.Single(r => r.BloodUnitId == none.UnitId).NeededByUtc);
        Assert.Null(rows.Single(r => r.BloodUnitId == cancelled.UnitId).NeededByUtc);
        // All are in the same group (none crossmatched yet), so the needed-by time decides their order.
        Assert.Single(rows.Select(ReadyToIssueAttentionRule.CanIssueRoutinely).Distinct());
        Assert.Equal(new[] { late.UnitId, soon.UnitId, far.UnitId }, rows.Take(3).Select(r => r.BloodUnitId).ToArray());

        // TEST-BB-655: the unit whose order is needed after its specimen expires is flagged and counted on the card; the
        // one needed in 20 minutes, and the one already late, are not.
        Assert.True(rows.Single(r => r.BloodUnitId == far.UnitId).ExpiresBeforeNeeded);
        Assert.False(soonRow.ExpiresBeforeNeeded);
        Assert.False(lateRow.ExpiresBeforeNeeded);
        Assert.Contains("1 crossmatch ends before it is needed", ReadyToIssueAttentionRule.CardNote(rows));
    }

    // TEST-BB-671: the issue desk's antibody summary words each antibody as the chart does: one known only from another
    // facility's report names it, one no longer detectable says so.
    [Fact]
    public async Task ListReadyToIssue_AntibodySummary_NamesOutsideReports()
    {
        var s = await SeedAsync("ABS-OUT");
        await AllocateAsync(s);
        await using (var c = _factory.Create())
        {
            c.AntibodyHistory.AddRange(
                new AntibodyHistory { PatientId = s.PatientId, AntibodySpecificity = "Anti-Jka", IsActive = true, ReportingFacility = "St. Mary's Hospital", IdentifiedOn = new DateOnly(2025, 2, 1) },
                new AntibodyHistory { PatientId = s.PatientId, AntibodySpecificity = "Anti-E", IsActive = false });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var row = (await Issuing(ctx).ListReadyToIssueAllocationsAsync()).Single(r => r.BloodUnitId == s.UnitId);
        Assert.True(row.HasAntibodyHistory);
        Assert.Contains("Anti-Jka (reported by St. Mary's Hospital, identified 2025-02-01, not yet confirmed here)", row.AntibodySummary);
        Assert.Contains("Anti-E (historical / currently undetectable)", row.AntibodySummary);
    }

    [Fact]
    public async Task ListReadyToIssue_FlagsCellularUnitWhenSecondAboIsMissing()
    {
        var missing = await SeedAsync("RDY-2ND", includeHistoricalAbo: false);
        var ready = await SeedAsync("RDY-2OK");
        var plasma = await SeedAsync(
            "RDY-FFP", includeHistoricalAbo: false, requiresCrossmatch: false, componentClass: ComponentClass.Plasma);
        await AllocateAsync(missing);
        await AllocateAsync(ready);
        await AllocateAsync(plasma);

        await using var c = _factory.Create();
        var rows = await Issuing(c).ListReadyToIssueAllocationsAsync();
        Assert.True(rows.Single(r => r.BloodUnitId == missing.UnitId).NeedsSecondAbo);
        Assert.False(rows.Single(r => r.BloodUnitId == ready.UnitId).NeedsSecondAbo);
        Assert.False(rows.Single(r => r.BloodUnitId == plasma.UnitId).NeedsSecondAbo);
        Assert.False(rows.Single(r => r.BloodUnitId == ready.UnitId).SpecimenBlocksIssue);
    }

    [Fact]
    public async Task ListReadyToIssue_FlagsOpenAntibodyWorkupWithoutRemovingTheRow()
    {
        var open = await SeedAsync("RDY-ABID");
        var clear = await SeedAsync("RDY-NOAB");
        await AllocateAsync(open);
        await AllocateAsync(clear);

        await using (var c = _factory.Create())
        {
            c.AntibodyIdentificationWorkups.AddRange(
                new AntibodyIdentificationWorkup
                {
                    PatientId = open.PatientId,
                    PrimaryLotId = 1,
                    Status = AntibodyWorkupStatus.PendingInterpretation
                },
                new AntibodyIdentificationWorkup
                {
                    PatientId = clear.PatientId,
                    PrimaryLotId = 1,
                    Status = AntibodyWorkupStatus.Completed
                });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var rows = await Issuing(ctx).ListReadyToIssueAllocationsAsync();
        var flagged = rows.Single(r => r.BloodUnitId == open.UnitId);
        Assert.True(flagged.OpenAntibodyWorkup);
        Assert.Equal(
            AntibodyIdentificationHistoryPostRule.ReadyToIssueNotice(true),
            flagged.OpenAntibodyWorkupNotice);
        var quiet = rows.Single(r => r.BloodUnitId == clear.UnitId);
        Assert.False(quiet.OpenAntibodyWorkup);
        Assert.Null(quiet.OpenAntibodyWorkupNotice);
    }

    [Fact]
    public async Task ListReadyToIssue_MarksRecordedReactionWithoutRemovingTheRow()
    {
        var recorded = await SeedAsync("RDY-RXN");
        var clear = await SeedAsync("RDY-NORXN");
        await AllocateAsync(recorded);
        await AllocateAsync(clear);

        await using (var c = _factory.Create())
        {
            var priorUnit = new BloodUnit
            {
                UnitNumber = "U-RDY-RXN-PRIOR",
                ProductTypeId = recorded.ProductTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Status = UnitStatus.Transfused,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(10)
            };
            var quietPrior = new BloodUnit
            {
                UnitNumber = "U-RDY-NORXN-PRIOR",
                ProductTypeId = clear.ProductTypeId,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                Status = UnitStatus.Transfused,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(10)
            };
            c.BloodUnits.AddRange(priorUnit, quietPrior);
            await c.SaveChangesAsync();

            var issue = new Issue
            {
                BloodProductId = priorUnit.Id,
                PatientId = recorded.PatientId,
                IssuedUtc = _factory.Clock.UtcNow.AddDays(-1),
                IssuedBy = "tech-test",
                Status = IssueStatus.Transfused
            };
            var quietIssue = new Issue
            {
                BloodProductId = quietPrior.Id,
                PatientId = clear.PatientId,
                IssuedUtc = _factory.Clock.UtcNow.AddDays(-2),
                IssuedBy = "tech-test",
                Status = IssueStatus.Transfused
            };
            c.Issues.AddRange(issue, quietIssue);
            await c.SaveChangesAsync();

            c.TransfusionEvents.AddRange(
                new TransfusionEvent
                {
                    IssueId = issue.Id,
                    BloodProductId = priorUnit.Id,
                    PatientId = recorded.PatientId,
                    ReactionSuspected = true,
                    FinalDisposition = TransfusionDisposition.Stopped,
                    DocumentedBy = "tech-test"
                },
                new TransfusionEvent
                {
                    IssueId = quietIssue.Id,
                    BloodProductId = quietPrior.Id,
                    PatientId = clear.PatientId,
                    ReactionSuspected = false,
                    DocumentedBy = "tech-test"
                });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var rows = await Issuing(ctx).ListReadyToIssueAllocationsAsync();
        var flagged = rows.Single(r => r.BloodUnitId == recorded.UnitId);
        Assert.Equal(ReactionHistoryBanner.RecordedReaction, flagged.ReactionNotice);
        Assert.Null(rows.Single(r => r.BloodUnitId == clear.UnitId).ReactionNotice);
    }

    [Fact]
    public async Task ListReadyToIssue_MarksThisPatientsAutologousAndDirectedUnits()
    {
        var autologous = await SeedAsync("RDY-AUTO");
        var directed = await SeedAsync("RDY-DIR");
        var allogeneic = await SeedAsync("RDY-ALLO");
        await AllocateAsync(autologous);
        await AllocateAsync(directed);
        await AllocateAsync(allogeneic);

        await using (var c = _factory.Create())
        {
            var own = await c.BloodUnits.SingleAsync(u => u.Id == autologous.UnitId);
            own.DonationRestriction = DonationRestriction.Autologous;
            own.ReservedPatientId = autologous.PatientId;
            var gift = await c.BloodUnits.SingleAsync(u => u.Id == directed.UnitId);
            gift.DonationRestriction = DonationRestriction.Directed;
            gift.ReservedPatientId = directed.PatientId;
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var rows = await Issuing(ctx).ListReadyToIssueAllocationsAsync();
        Assert.Equal(
            ProductSelectionOrder.PatientAutologousCue,
            rows.Single(r => r.BloodUnitId == autologous.UnitId).DonationRestrictionNotice);
        Assert.Equal(
            ProductSelectionOrder.PatientDirectedCue,
            rows.Single(r => r.BloodUnitId == directed.UnitId).DonationRestrictionNotice);
        Assert.Null(rows.Single(r => r.BloodUnitId == allogeneic.UnitId).DonationRestrictionNotice);
    }

    [Fact]
    public async Task ListReadyToIssue_ExpiredSpecimen_DoesNotOfferIssue()
    {
        var expired = await SeedAsync("RDY-SPEX");
        var current = await SeedAsync("RDY-SPOK");
        await RecordCompatibleCrossmatchAsync(expired);
        await RecordCompatibleCrossmatchAsync(current);
        await AllocateAsync(expired);
        await AllocateAsync(current);

        await using (var c = _factory.Create())
        {
            var specimen = await c.Specimens.SingleAsync(s => s.Id == expired.SpecimenId);
            specimen.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
            await c.SaveChangesAsync();
        }

        await using (var ctx = _factory.Create())
        {
            var rows = await Issuing(ctx).ListReadyToIssueAllocationsAsync();
            var blocked = rows.Single(r => r.BloodUnitId == expired.UnitId);
            Assert.True(blocked.SpecimenBlocksIssue);
            Assert.True(blocked.SpecimenExpired);
            Assert.Contains("expired", blocked.SpecimenBlockReason, StringComparison.OrdinalIgnoreCase);
            Assert.False(rows.Single(r => r.BloodUnitId == current.UnitId).SpecimenBlocksIssue);

            var issued = await Issuing(ctx).IssueUnitAsync(IssueReq(expired));
            Assert.True(issued.Evaluation!.IsHardStopped);
            Assert.Contains(issued.Evaluation.HardStops, r => r.Code == SpecimenExpirationRule.ExpiredCode);
        }

        await using (var c = _factory.Create())
        {
            c.Specimens.Add(new Specimen
            {
                AccessionNumber = "ACC-RDY-SPNEW",
                PatientId = expired.PatientId,
                SpecimenType = "EDTA",
                CollectedUtc = _factory.Clock.UtcNow.AddMinutes(-30),
                ReceivedUtc = _factory.Clock.UtcNow.AddMinutes(-20),
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(2),
                Status = SpecimenStatus.Accepted
            });
            await c.SaveChangesAsync();
        }

        await using var later = _factory.Create();
        var refreshed = (await Issuing(later).ListReadyToIssueAllocationsAsync())
            .Single(r => r.BloodUnitId == expired.UnitId);
        Assert.False(refreshed.SpecimenBlocksIssue);
        Assert.False(refreshed.SpecimenExpired);
    }

    [Fact]
    public async Task ListReadyToIssue_OutdatedUnit_DoesNotOfferIssue()
    {
        var outdated = await SeedAsync("RDY-UNITEX");
        var current = await SeedAsync("RDY-UNITOK");
        await RecordCompatibleCrossmatchAsync(outdated);
        await RecordCompatibleCrossmatchAsync(current);
        await AllocateAsync(outdated);
        await AllocateAsync(current);

        await using (var c = _factory.Create())
        {
            var unit = await c.BloodUnits.SingleAsync(u => u.Id == outdated.UnitId);
            unit.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var rows = await Issuing(ctx).ListReadyToIssueAllocationsAsync();
        var blocked = rows.Single(r => r.BloodUnitId == outdated.UnitId);
        Assert.True(blocked.UnitOutdated);
        Assert.Contains("outdated", blocked.UnitBlockReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(rows.Single(r => r.BloodUnitId == current.UnitId).UnitOutdated);

        var issued = await Issuing(ctx).IssueUnitAsync(IssueReq(outdated));
        Assert.True(issued.Evaluation!.IsHardStopped);
        Assert.Contains(issued.Evaluation.HardStops, r => r.Code == BloodUnitExpirationRule.ExpiredCode);

        var emergency = await Issuing(ctx).IssueUnitAsync(IssueReq(outdated, IssueType.EmergencyRelease));
        Assert.True(emergency.Evaluation!.IsHardStopped);
        Assert.Contains(emergency.Evaluation.HardStops, r => r.Code == BloodUnitExpirationRule.ExpiredCode);
        Assert.False(blocked.ExpiresSoon);
    }

    [Fact]
    public async Task ListReadyToIssue_MarksUnitInsideTheIssueNearExpiryWindow()
    {
        var soon = await SeedAsync("RDY-SOON");
        var later = await SeedAsync("RDY-LATER");
        var thawed = await SeedAsync("RDY-THAW", requiresCrossmatch: false, componentClass: ComponentClass.Plasma);
        await AllocateAsync(soon);
        await AllocateAsync(later);
        await AllocateAsync(thawed);

        var thawedExpires = _factory.Clock.UtcNow.AddHours(10);
        await using (var c = _factory.Create())
        {
            (await c.BloodUnits.SingleAsync(u => u.Id == soon.UnitId)).ExpiresUtc = _factory.Clock.UtcNow.AddHours(2);
            (await c.BloodUnits.SingleAsync(u => u.Id == later.UnitId)).ExpiresUtc = _factory.Clock.UtcNow.AddDays(3);
            var thawedUnit = await c.BloodUnits.SingleAsync(u => u.Id == thawed.UnitId);
            thawedUnit.ExpiresUtc = thawedExpires;
            var allocation = await c.Allocations.SingleAsync(a => a.BloodProductId == thawed.UnitId);
            c.AutoThawRequests.Add(new AutoThawRequest
            {
                AllocationId = allocation.Id,
                BloodUnitId = thawed.UnitId,
                Status = AutoThawRequestStatus.Completed,
                ThawUtc = _factory.Clock.UtcNow,
                PreviousExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                NewExpiresUtc = thawedExpires,
                CompletedUtc = _factory.Clock.UtcNow,
                CompletedBy = "tech",
                CreatedBy = "tech"
            });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var rows = await Issuing(ctx).ListReadyToIssueAllocationsAsync();
        var shortDated = rows.Single(r => r.BloodUnitId == soon.UnitId);
        Assert.True(shortDated.ExpiresSoon);
        Assert.False(shortDated.UnitOutdated);
        Assert.False(rows.Single(r => r.BloodUnitId == later.UnitId).ExpiresSoon);
        Assert.False(rows.Single(r => r.BloodUnitId == thawed.UnitId).ExpiresSoon);
    }

    [Fact]
    public async Task ListReadyToIssue_ShowsElectronicCrossmatchBlockingReason()
    {
        var missingSecond = await SeedAsync("RDY-EXM1", includeHistoricalAbo: false);
        var concordant = await SeedAsync("RDY-EXM2");
        await AllocateAsync(missingSecond);
        await AllocateAsync(concordant);

        await using (var off = _factory.Create())
        {
            var rows = await Issuing(off).ListReadyToIssueAllocationsAsync();
            Assert.Contains(
                "disabled in facility policy",
                rows.Single(r => r.BloodUnitId == missingSecond.UnitId).ElectronicCrossmatchNotice,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "disabled in facility policy",
                rows.Single(r => r.BloodUnitId == concordant.UnitId).ElectronicCrossmatchNotice,
                StringComparison.OrdinalIgnoreCase);
        }

        await using (var c = _factory.Create())
        {
            c.SystemSettings.Add(new SystemSetting
            {
                Key = FacilityPolicyKeys.AllowElectronicCrossmatch,
                Value = "true",
                Category = "Issue"
            });
            await c.SaveChangesAsync();
            await ExmScreenSeed.AddVerifiedNegativeScreenAsync(c, missingSecond.PatientId, _factory.Clock.UtcNow, missingSecond.SpecimenId);
            await ExmScreenSeed.AddVerifiedNegativeScreenAsync(c, concordant.PatientId, _factory.Clock.UtcNow, concordant.SpecimenId);
        }

        try
        {
            await using var on = _factory.Create();
            var rows = await Issuing(on).ListReadyToIssueAllocationsAsync();
            Assert.Contains(
                "two concordant",
                rows.Single(r => r.BloodUnitId == missingSecond.UnitId).ElectronicCrossmatchNotice,
                StringComparison.OrdinalIgnoreCase);
            Assert.Null(rows.Single(r => r.BloodUnitId == concordant.UnitId).ElectronicCrossmatchNotice);
        }
        finally
        {
            await using var restore = _factory.Create();
            var setting = await restore.SystemSettings.SingleAsync(s => s.Key == FacilityPolicyKeys.AllowElectronicCrossmatch);
            setting.Value = "false";
            await restore.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ElectronicCrossmatch_WithoutSecondAbo_IsBlocked()
    {
        var s = await SeedAsync("ONEABO", requiresCrossmatch: false, includeHistoricalAbo: false);
        await using var ctx = _factory.Create();
        var result = await Compatibility(ctx).RecordCrossmatchAsync(
            new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Electronic));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ElectronicCrossmatchEligibilityRule.SecondTypeCode);
    }

    [Fact]
    public async Task Transfusion_WithoutWardReceipt_IsHardStopped()
    {
        var s = await SeedAsync("NORD");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
        }

        await using var ctx = _factory.Create();
        var tx = await Issuing(ctx).DocumentTransfusionAsync(issueId, TxReq(s));
        Assert.False(tx.Succeeded);
        Assert.Contains(tx.Evaluation!.HardStops, r => r.Code == WardReceiptRule.Code);
    }

    [Fact]
    public async Task WardReceipt_VisualFail_AndDuplicate_Fail()
    {
        var s = await SeedAsync("WRDFAIL");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
        }

        await using var ctx = _factory.Create();
        var issuing = Issuing(ctx);
        var visual = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", VisualInspectionAcceptable: false, Appearance: UnitAppearance.Acceptable));
        Assert.False(visual.Succeeded);
        Assert.Contains("visual inspection", visual.Error, StringComparison.OrdinalIgnoreCase);

        var first = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable));
        Assert.True(first.Succeeded);
        Assert.Equal(UnitAppearance.Acceptable, first.Value!.WardAppearance);

        var dup = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest("another-nurse", Appearance: UnitAppearance.Acceptable));
        Assert.False(dup.Succeeded);
        Assert.Contains("already acknowledged", dup.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WardReceipt_HemolysisAppearance_IsHardStopped()
    {
        var s = await SeedAsync("WRDHEM");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
        }

        await using var ctx = _factory.Create();
        var issuing = Issuing(ctx);
        var visual = await issuing.RecordWardReceiptAsync(
            issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Hemolysis));
        Assert.False(visual.Succeeded);
        Assert.Contains(visual.Evaluation!.HardStops, r => r.Code == WardAppearanceRule.Code);
        Assert.Null((await ctx.Issues.FindAsync(issueId))!.WardReceivedUtc);
    }

    [Fact]
    public async Task Return_WithoutWardReceipt_Succeeds()
    {
        var s = await SeedAsync("RETNR");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
        }

        await using var ctx = _factory.Create();
        var returned = await Issuing(ctx).ReturnUnitAsync(issueId, new ReturnUnitRequest("Never arrived on the ward", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true));
        Assert.True(returned.Succeeded);
    }

    [Fact]
    public async Task Issue_UnknownSecondVerifier_IsHardStopped()
    {
        var s = await SeedAsync("2NDUNK");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(
            IssueReq(s) with { SecondVerifier = "not-a-user" });
        Assert.False(issued.Succeeded);
        Assert.Contains(issued.Evaluation!.HardStops, r => r.Code == SecondVerifierDirectoryRule.Code);
    }

    [Fact]
    public async Task Issue_ActiveSecondVerifier_Succeeds()
    {
        var s = await SeedAsync("2NDOK");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using (var c = _factory.Create())
        {
            c.Users.Add(new User
            {
                UserName = "tech2",
                DisplayName = "Tech Two",
                IsActive = true
            });
            await c.SaveChangesAsync();
        }

        await using var ctx = _factory.Create();
        var issued = await Issuing(ctx).IssueUnitAsync(IssueReq(s) with { SecondVerifier = "TECH2" });
        Assert.True(issued.Succeeded);
        Assert.Equal("TECH2", issued.Value!.SecondVerifier);
    }

    [Fact]
    public async Task Issue_SetsCoolerAndAppearsOnInTransitWorklist()
    {
        var s = await SeedAsync("COOLER");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with { CoolerId = "CLR-7" });
            Assert.True(issued.Succeeded);
            issueId = issued.Value!.Id;
            Assert.Equal("CLR-7", issued.Value.CoolerId);
            Assert.NotNull(issued.Value.InTransitDueUtc);

            var transit = await Issuing(c).ListInTransitAsync();
            var row = Assert.Single(transit, t => t.IssueId == issueId);
            Assert.Equal("CLR-7", row.CoolerId);
            Assert.False(row.IsOverdue);
            Assert.Equal(s.UnitNumber, row.UnitNumber);
            Assert.False(string.IsNullOrWhiteSpace(row.PatientDisplayName));
            Assert.False(string.IsNullOrWhiteSpace(row.CurrentBloodType));
            Assert.NotNull(row.SpecimenExpiresUtc);
            Assert.False(row.SpecimenExpired);
            Assert.False(row.HasAntibodyHistory);
        }

        await using (var c = _factory.Create())
        {
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
            var transit = await Issuing(c).ListInTransitAsync();
            Assert.DoesNotContain(transit, t => t.IssueId == issueId);
        }
    }

    [Fact]
    public async Task ListReadyToIssue_MarksUnresolvedAboRhDiscrepancy()
    {
        var cellular = await SeedAsync("RDY-DISC");
        var plasma = await SeedAsync(
            "RDY-DISC-FFP", requiresCrossmatch: false, componentClass: ComponentClass.Plasma);
        var concordant = await SeedAsync("RDY-DISC-OK");
        await AllocateAsync(cellular);
        await AllocateAsync(plasma);
        await AllocateAsync(concordant);

        await using (var c = _factory.Create())
        {
            c.PatientBloodTypeHistory.AddRange(
                new PatientBloodTypeHistory
                {
                    PatientId = cellular.PatientId,
                    Abo = AboGroup.A,
                    RhD = RhType.Positive,
                    Source = BloodTypeSource.HistoricalImport,
                    IsCurrent = false
                },
                new PatientBloodTypeHistory
                {
                    PatientId = plasma.PatientId,
                    Abo = AboGroup.A,
                    RhD = RhType.Positive,
                    Source = BloodTypeSource.HistoricalImport,
                    IsCurrent = false
                });
            await c.SaveChangesAsync();
        }

        await using var read = _factory.Create();
        var rows = await Issuing(read).ListReadyToIssueAllocationsAsync();
        var cellularRow = rows.Single(r => r.BloodUnitId == cellular.UnitId);
        Assert.True(cellularRow.AboRhDiscrepancyBlocksIssue);
        Assert.Contains("crossmatch-required", cellularRow.AboRhDiscrepancyNotice);

        var plasmaRow = rows.Single(r => r.BloodUnitId == plasma.UnitId);
        Assert.False(plasmaRow.AboRhDiscrepancyBlocksIssue);
        Assert.Contains("disagrees", plasmaRow.AboRhDiscrepancyNotice);

        var concordantRow = rows.Single(r => r.BloodUnitId == concordant.UnitId);
        Assert.False(concordantRow.AboRhDiscrepancyBlocksIssue);
        Assert.Null(concordantRow.AboRhDiscrepancyNotice);
    }

    [Fact]
    public async Task ListReadyToIssue_MarksAntigenWarningWithoutRemovingTheRow()
    {
        var untested = await SeedAsync("RDY-AG");
        var negative = await SeedAsync("RDY-AG-NEG");
        var clear = await SeedAsync("RDY-AG-CLR");
        await AllocateAsync(untested);
        await AllocateAsync(negative);
        await AllocateAsync(clear);

        await using (var c = _factory.Create())
        {
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

            c.AntibodyHistory.AddRange(
                new AntibodyHistory
                {
                    PatientId = untested.PatientId,
                    AntibodySpecificity = "anti-K",
                    BloodAttributeDefinitionId = kell.Id,
                    IsActive = true
                },
                new AntibodyHistory
                {
                    PatientId = negative.PatientId,
                    AntibodySpecificity = "anti-K",
                    BloodAttributeDefinitionId = kell.Id,
                    IsActive = true
                });
            c.UnitBloodAttributes.Add(new UnitBloodAttribute
            {
                BloodProductId = negative.UnitId,
                BloodAttributeDefinitionId = kell.Id,
                AttributeKind = BloodAttributeKind.Antigen,
                Result = AntigenResult.Negative
            });
            await c.SaveChangesAsync();
        }

        await using var read = _factory.Create();
        var rows = await Issuing(read).ListReadyToIssueAllocationsAsync();
        var untestedRow = rows.Single(r => r.BloodUnitId == untested.UnitId);
        Assert.Contains("not tested", untestedRow.AntigenNotice);
        Assert.Contains(untested.UnitId, rows.Select(r => r.BloodUnitId));
        Assert.Null(rows.Single(r => r.BloodUnitId == negative.UnitId).AntigenNotice);
        Assert.Null(rows.Single(r => r.BloodUnitId == clear.UnitId).AntigenNotice);
    }

    [Fact]
    public async Task ListReadyToIssue_MarksUnmetSpecialRequirementWithoutOfferingIssue()
    {
        var missing = await SeedAsync("RDY-SR");
        var meets = await SeedAsync("RDY-SR-OK");
        var acknowledgment = await SeedAsync("RDY-SR-ACK");
        var ended = await SeedAsync("RDY-SR-END");
        await AllocateAsync(missing);
        await AllocateAsync(meets);
        await AllocateAsync(acknowledgment);
        await AllocateAsync(ended);

        await using (var c = _factory.Create())
        {
            var attr = new ProductAttribute { Code = SpecialRequirementCatalog.Irradiated, Name = "Irradiated" };
            c.ProductAttributes.Add(attr);
            await c.SaveChangesAsync();
            c.ProductAttributeAssignments.Add(new ProductAttributeAssignment
            {
                ProductTypeId = meets.ProductTypeId,
                ProductAttributeId = attr.Id
            });
            c.SpecialTransfusionRequirements.AddRange(
                new SpecialTransfusionRequirement
                {
                    PatientId = missing.PatientId,
                    RequirementType = SpecialTransfusionRequirementType.Irradiated,
                    Reason = "Required",
                    EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
                    IsActive = true,
                    EnteredBy = "tech-test"
                },
                new SpecialTransfusionRequirement
                {
                    PatientId = meets.PatientId,
                    RequirementType = SpecialTransfusionRequirementType.Irradiated,
                    Reason = "Required",
                    EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
                    IsActive = true,
                    EnteredBy = "tech-test"
                },
                new SpecialTransfusionRequirement
                {
                    PatientId = acknowledgment.PatientId,
                    RequirementType = SpecialTransfusionRequirementType.Other,
                    Reason = "Acknowledge at issue",
                    EffectiveUtc = _factory.Clock.UtcNow.AddDays(-1),
                    IsActive = true,
                    EnteredBy = "tech-test"
                },
                new SpecialTransfusionRequirement
                {
                    PatientId = ended.PatientId,
                    RequirementType = SpecialTransfusionRequirementType.Irradiated,
                    Reason = "Ended",
                    EffectiveUtc = _factory.Clock.UtcNow.AddDays(-2),
                    ExpiresUtc = _factory.Clock.UtcNow,
                    IsActive = true,
                    EnteredBy = "tech-test"
                });
            await c.SaveChangesAsync();
        }

        await using var read = _factory.Create();
        var rows = await Issuing(read).ListReadyToIssueAllocationsAsync();
        var missingRow = rows.Single(r => r.BloodUnitId == missing.UnitId);
        Assert.Contains("IRRAD", missingRow.SpecialRequirementNotice);
        Assert.Null(rows.Single(r => r.BloodUnitId == meets.UnitId).SpecialRequirementNotice);
        Assert.Null(rows.Single(r => r.BloodUnitId == acknowledgment.UnitId).SpecialRequirementNotice);
        Assert.Null(rows.Single(r => r.BloodUnitId == ended.UnitId).SpecialRequirementNotice);
    }

    [Fact]
    public async Task ListReadyToIssue_MarksMissingCrossmatchAndKeepsIncompatibleIssuable()
    {
        var missing = await SeedAsync("RDY-NOXM");
        var plasma = await SeedAsync(
            "RDY-NOXM-FFP", requiresCrossmatch: false, componentClass: ComponentClass.Plasma);
        var current = await SeedAsync("RDY-XM");
        var incompatible = await SeedAsync("RDY-INCXM");
        var expired = await SeedAsync("RDY-EXXM");
        var other = await SeedAsync("RDY-OTHERXM");
        var both = await SeedAsync("RDY-BOTHXM");
        await AllocateAsync(missing);
        await AllocateAsync(plasma);
        await AllocateAsync(current);
        await AllocateAsync(incompatible);
        await AllocateAsync(expired);
        await AllocateAsync(other);
        await AllocateAsync(both);
        await RecordCompatibleCrossmatchAsync(current);
        await RecordCompatibleCrossmatchAsync(expired);
        await RecordCompatibleCrossmatchAsync(both);

        await using (var c = _factory.Create())
        {
            var xm = await Compatibility(c).RecordCrossmatchAsync(
                new RecordCrossmatchRequest(
                    incompatible.UnitId, incompatible.PatientId, incompatible.SpecimenId,
                    CrossmatchMethod.Serologic, CrossmatchResult.Incompatible));
            Assert.True(xm.Succeeded, xm.Error);
            await Compatibility(c).RecordCrossmatchAsync(
                new RecordCrossmatchRequest(
                    both.UnitId, both.PatientId, both.SpecimenId,
                    CrossmatchMethod.Serologic, CrossmatchResult.Incompatible));

            var expiredRow = await c.Crossmatches.SingleAsync(x => x.BloodProductId == expired.UnitId);
            expiredRow.ExpiresUtc = _factory.Clock.UtcNow;
            c.Crossmatches.Add(new Crossmatch
            {
                BloodProductId = other.UnitId,
                PatientId = missing.PatientId,
                SpecimenId = missing.SpecimenId,
                Result = CrossmatchResult.Compatible,
                PerformedUtc = _factory.Clock.UtcNow,
                PerformedBy = "tech-test",
                ExpiresUtc = _factory.Clock.UtcNow.AddHours(12)
            });
            await c.SaveChangesAsync();
        }

        await using var read = _factory.Create();
        var rows = await Issuing(read).ListReadyToIssueAllocationsAsync();

        var missingRow = rows.Single(r => r.BloodUnitId == missing.UnitId);
        Assert.True(missingRow.CrossmatchBlocksRoutineIssue);
        Assert.Contains("compatible, unexpired crossmatch", missingRow.CrossmatchNotice);

        var plasmaRow = rows.Single(r => r.BloodUnitId == plasma.UnitId);
        Assert.False(plasmaRow.CrossmatchBlocksRoutineIssue);
        Assert.Null(plasmaRow.CrossmatchNotice);

        var currentRow = rows.Single(r => r.BloodUnitId == current.UnitId);
        Assert.False(currentRow.CrossmatchBlocksRoutineIssue);
        Assert.Null(currentRow.CrossmatchNotice);

        var incompatibleRow = rows.Single(r => r.BloodUnitId == incompatible.UnitId);
        Assert.False(incompatibleRow.CrossmatchBlocksRoutineIssue);
        Assert.Contains("incompatible", incompatibleRow.CrossmatchNotice);

        var expiredRowResult = rows.Single(r => r.BloodUnitId == expired.UnitId);
        Assert.True(expiredRowResult.CrossmatchBlocksRoutineIssue);

        var otherRow = rows.Single(r => r.BloodUnitId == other.UnitId);
        Assert.True(otherRow.CrossmatchBlocksRoutineIssue);

        var bothRow = rows.Single(r => r.BloodUnitId == both.UnitId);
        Assert.False(bothRow.CrossmatchBlocksRoutineIssue);
        Assert.Null(bothRow.CrossmatchNotice);
    }

    private CompatibilityService CompatibilityWithExceptions(BloodBankDbContext c) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            BloodAttrCompat(c), AntibodyScreenCompat(c), c, _factory.Clock, _factory.CurrentUser,
            new EfRepository<Issue>(c), new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            permissions: new FixedPermissionEvaluator(1, PermissionCodes.CompatibilityCrossmatch),
            workups: new EfRepository<AntibodyIdentificationWorkup>(c),
            retypes: new EfRepository<ProductRetypeResult>(c),
            exceptionDefinitions: new EfRepository<ExceptionDefinition>(c),
            overrides: new EfRepository<Override>(c));

    /// <summary>Emergency release, then the pre-transfusion specimen expires. Returns the issue id.</summary>
    private async Task<long> EmergencyReleaseThenExpireSpecimenAsync(Scenario s, bool collectedAfterIssue = false, bool withDefinition = true)
    {
        await AllocateAsync(s);
        await using var c = _factory.Create();
        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.EmergencyRelease,
            overrideReason: "Uncrossmatched release for hemorrhage", authorizedBy: "dr-authorizer"));
        Assert.True(issued.Succeeded, issued.Error);
        var specimen = await c.Specimens.SingleAsync(x => x.Id == s.SpecimenId);
        specimen.CollectedUtc = collectedAfterIssue ? issued.Value!.IssuedUtc.AddMinutes(1) : issued.Value!.IssuedUtc.AddMinutes(-30);
        specimen.ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-5);
        var definition = await c.ExceptionDefinitions.FirstOrDefaultAsync(e => e.RuleCode == CrossmatchSpecimenRule.RetrospectiveCode);
        if (definition is null)
        {
            definition = new ExceptionDefinition
            {
                RuleCode = CrossmatchSpecimenRule.RetrospectiveCode, Name = "Retro XM on expired specimen",
                MinSecurityLevel = 0, IsOverridable = true
            };
            c.ExceptionDefinitions.Add(definition);
        }

        definition.IsActive = withDefinition;

        await c.SaveChangesAsync();
        return issued.Value.Id;
    }

    private static RecordCrossmatchRequest RetroXm(Scenario s, string? reason = null, string? authorizedBy = null) =>
        new(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Compatible,
            OverrideReason: reason, AuthorizedBy: authorizedBy);

    // TEST-BB-364 (OCD-050): the retrospective crossmatch on an expired pre-transfusion specimen is an exception, not a hard stop.
    [Fact]
    public async Task RetrospectiveXm_OnExpiredPreTransfusionSpecimen_AsksForAnException_ThenRecordsIt()
    {
        var s = await SeedAsync("RETROEXC");
        var issueId = await EmergencyReleaseThenExpireSpecimenAsync(s);

        await using (var c = _factory.Create())
        {
            var asked = await CompatibilityWithExceptions(c).RecordCrossmatchAsync(RetroXm(s));
            Assert.False(asked.Succeeded);
            Assert.Empty(asked.Evaluation!.HardStops);
            Assert.Contains(asked.Evaluation.Warnings, w => w.Code == CrossmatchSpecimenRule.RetrospectiveCode);
            Assert.False(await c.Crossmatches.AnyAsync(x => x.BloodProductId == s.UnitId));
        }

        await using (var c = _factory.Create())
        {
            var halfway = await CompatibilityWithExceptions(c).RecordCrossmatchAsync(RetroXm(s, reason: "Pre-transfusion sample; patient transfused since"));
            Assert.False(halfway.Succeeded);
            Assert.Contains(halfway.Evaluation!.Warnings, w => w.Code == OverrideFieldsRule.Code);
        }

        await using (var c = _factory.Create())
        {
            var recorded = await CompatibilityWithExceptions(c).RecordCrossmatchAsync(
                RetroXm(s, reason: "Pre-transfusion sample; patient transfused since", authorizedBy: "dr-supervisor"));
            Assert.True(recorded.Succeeded, recorded.Error);
            Assert.Contains(recorded.Evaluation!.Warnings, w => w.Code == CrossmatchSpecimenRule.RetrospectiveCode);
        }

        await using var v = _factory.Create();
        var xm = await v.Crossmatches.SingleAsync(x => x.BloodProductId == s.UnitId);
        var exception = await v.Overrides.SingleAsync(o => o.RuleCode == CrossmatchSpecimenRule.RetrospectiveCode);
        Assert.Equal(nameof(Crossmatch), exception.ContextType);
        Assert.Equal(xm.Id, exception.ContextId);
        Assert.Equal("dr-supervisor", exception.AuthorizedBy);
        Assert.Null(exception.ReviewOutcome);
        Assert.Equal(xm.Id, (await v.Issues.SingleAsync(i => i.Id == issueId)).RetrospectiveCrossmatchId);
        Assert.True(await v.AuditEvents.AnyAsync(a => a.EventType == AuditEventType.Override && a.EntityType == nameof(Crossmatch) && a.EntityId == xm.Id));
    }

    [Fact]
    public async Task RetrospectiveXm_OnSpecimenCollectedAfterIssue_ThatExpired_IsStillAHardStop()
    {
        var s = await SeedAsync("RETROPOST");
        await EmergencyReleaseThenExpireSpecimenAsync(s, collectedAfterIssue: true);

        await using var c = _factory.Create();
        var r = await CompatibilityWithExceptions(c).RecordCrossmatchAsync(RetroXm(s, "reason", "dr-supervisor"));
        Assert.False(r.Succeeded);
        Assert.Contains(r.Evaluation!.HardStops, h => h.Code == CrossmatchSpecimenRule.Code);
        Assert.False(await c.Overrides.AnyAsync(o => o.RuleCode == CrossmatchSpecimenRule.RetrospectiveCode));
    }

    [Fact]
    public async Task RetrospectiveXm_Exception_WithoutAnActiveDefinition_IsRefused()
    {
        var s = await SeedAsync("RETRONODEF");
        await EmergencyReleaseThenExpireSpecimenAsync(s, withDefinition: false);

        await using var c = _factory.Create();
        var r = await CompatibilityWithExceptions(c).RecordCrossmatchAsync(RetroXm(s, "reason", "dr-supervisor"));
        Assert.False(r.Succeeded);
        Assert.Contains(r.Evaluation!.HardStops, h => h.Code == "EXC-DEF-MISSING");
        Assert.False(await c.Crossmatches.AnyAsync(x => x.BloodProductId == s.UnitId));
    }

    // TEST-BB-363: a crossmatch recorded directly on an expired specimen is refused and nothing is saved.
    [Fact]
    public async Task RecordCrossmatch_OnAnExpiredSpecimen_IsRefused_AndNothingIsSaved()
    {
        var s = await SeedAsync("XMSPECEXP");
        await using (var aged = _factory.Create())
        {
            var specimen = await aged.Specimens.SingleAsync(x => x.Id == s.SpecimenId);
            specimen.ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-5);
            await aged.SaveChangesAsync();
        }

        await using var c = _factory.Create();
        var result = await Compatibility(c).RecordCrossmatchAsync(
            new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Serologic, CrossmatchResult.Compatible));

        Assert.False(result.Succeeded);
        var stop = Assert.Single(result.Evaluation!.HardStops);
        Assert.Equal(CrossmatchSpecimenRule.Code, stop.Code);
        Assert.Contains("ACC-XMSPECEXP", stop.Message);
        Assert.False(await c.Crossmatches.AnyAsync(x => x.BloodProductId == s.UnitId));
    }

    // TEST-BB-360: once the only compatible crossmatch expires, the issue desk no longer shows the unit as ready.
    [Fact]
    public async Task ListReadyToIssue_AnExpiredCrossmatch_ShowsCrossmatchExpired_NotReady()
    {
        var s = await SeedAsync("RDYXMEXP");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        await using (var aged = _factory.Create())
        {
            var xm = await aged.Crossmatches.SingleAsync(x => x.BloodProductId == s.UnitId);
            xm.ExpiresUtc = _factory.Clock.UtcNow.AddMinutes(-1);
            await aged.SaveChangesAsync();
        }

        await using var c = _factory.Create();
        var row = Assert.Single(await Issuing(c).ListReadyToIssueAllocationsAsync(), r => r.BloodUnitId == s.UnitId);
        Assert.Equal(ProductAllocationDisplayStatus.CrossmatchExpired, row.DisplayStatus);
        Assert.True(row.CrossmatchBlocksRoutineIssue);
        Assert.Null(row.CrossmatchExpiresUtc);
    }

    [Fact]
    public async Task ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate()
    {
        var s = await SeedAsync("RDYISS");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        await using (var placed = _factory.Create())
        {
            var location = new InventoryLocation
            {
                Code = "FRIDGE-RDY",
                Name = "Main Blood Bank Refrigerator"
            };
            placed.InventoryLocations.Add(location);
            await placed.SaveChangesAsync();
            var stored = await placed.BloodUnits.FindAsync(s.UnitId);
            stored!.CurrentLocationId = location.Id;
            await placed.SaveChangesAsync();
        }

        await using var c = _factory.Create();
        var row = Assert.Single(await Issuing(c).ListReadyToIssueAllocationsAsync(), r => r.BloodUnitId == s.UnitId);
        Assert.Equal(s.UnitNumber, row.UnitNumber);
        Assert.Equal("Test RBC", row.ProductName);
        Assert.Equal(_factory.Clock.UtcNow.AddDays(20), row.UnitExpiresUtc);
        Assert.Equal("O+", row.UnitBloodType);
        Assert.Equal("O+", row.CurrentBloodType);
        Assert.Equal(_factory.Clock.UtcNow.AddDays(2), row.CrossmatchExpiresUtc);
        Assert.Equal("Main Blood Bank Refrigerator", row.StorageLocationName);
        Assert.Equal("ACC-RDYISS", row.SpecimenAccession);
        Assert.Equal(s.Mrn, row.MedicalRecordNumber);
        Assert.Equal(s.DateOfBirth, row.DateOfBirth);
        Assert.Equal(s.PatientId, row.PatientId);
        Assert.False(string.IsNullOrWhiteSpace(row.PatientDisplayName));
        Assert.Equal(CrossmatchResult.Compatible, row.LatestCrossmatchResult);
        Assert.Equal(ProductAllocationDisplayStatus.ReadyForIssue, row.DisplayStatus);
        Assert.False(row.NeedsSecondAbo);

        Assert.True((await Issuing(c).IssueUnitAsync(IssueReq(s))).Succeeded);
        Assert.DoesNotContain(await Issuing(c).ListReadyToIssueAllocationsAsync(), r => r.BloodUnitId == s.UnitId);
    }

    [Fact]
    public async Task ListReadyToIssue_KeepsTheLocationIdWhenTheNameIsBlank()
    {
        var s = await SeedAsync("RDYBLANK");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        long locationId;
        const string code = "F2RDYBLANK";
        await using (var placed = _factory.Create())
        {
            var location = new InventoryLocation { Code = code, Name = "   " };
            placed.InventoryLocations.Add(location);
            await placed.SaveChangesAsync();
            locationId = location.Id;
            var stored = await placed.BloodUnits.FindAsync(s.UnitId);
            stored!.CurrentLocationId = location.Id;
            await placed.SaveChangesAsync();
        }

        await using var read = _factory.Create();
        var row = Assert.Single(await Issuing(read).ListReadyToIssueAllocationsAsync(), r => r.BloodUnitId == s.UnitId);
        Assert.Equal($"Location {locationId}", row.StorageLocationName);
        Assert.DoesNotContain(code, row.StorageLocationName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListOutstandingIssued_SurfacesUnitAndStaysAfterWardReceipt()
    {
        var s = await SeedAsync("OUTISS");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with { CoolerId = "CLR-OUT", IssuedTo = "RN Alvarez", IssuedToLocation = " Trauma Bay 2 " });
            Assert.True(issued.Succeeded);
            issueId = issued.Value!.Id;
            var saved = await c.Issues.SingleAsync(i => i.Id == issueId);
            saved.IssuedBy = " tech-test ";
            saved.EmergencyReleaseDetails = " Uncrossmatched release for hemorrhage ";
            saved.SecondVerifier = " witness-tech ";
            var releaseOverride = new Override
            {
                Action = OverrideAction.EmergencyRelease,
                ContextType = nameof(Issue),
                ContextId = saved.BloodProductId,
                RuleCode = "ISS-XM-REQUIRED",
                Reason = "Uncrossmatched release for hemorrhage",
                AuthorizedBy = " dr-authorizer ",
                OverriddenUtc = _factory.Clock.UtcNow
            };
            c.Overrides.Add(releaseOverride);
            await c.SaveChangesAsync();
            saved.OverrideId = releaseOverride.Id;
            await c.SaveChangesAsync();

            var row = Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
            Assert.Equal(s.UnitNumber, row.UnitNumber);
            Assert.Equal("Test RBC", row.ProductName);
            Assert.Equal(_factory.Clock.UtcNow.AddDays(20), row.UnitExpiresUtc);
            Assert.Equal("O+", row.UnitBloodType);
            Assert.Equal("ACC-OUTISS", row.SpecimenAccession);
            Assert.Equal(_factory.Clock.UtcNow.AddDays(2), row.CrossmatchExpiresUtc);
            Assert.Equal(CrossmatchResult.Compatible, row.LatestCrossmatchResult);
            Assert.Equal("RN Alvarez", row.IssuedTo);
            Assert.Equal("CLR-OUT", row.CoolerId);
            Assert.Equal("Trauma Bay 2", row.IssuedToLocation);
            Assert.Equal(IssueType.Standard, row.IssueType);
            Assert.Equal("tech-test", row.IssuedBy);
            Assert.Equal("Uncrossmatched release for hemorrhage", row.EmergencyReleaseDetails);
            Assert.Equal("witness-tech", row.SecondVerifier);
            Assert.Equal("dr-authorizer", row.EmergencyReleaseAuthorizer);
            Assert.Equal(UnitAppearance.Acceptable, row.IssueAppearance);
            Assert.Null(row.PtagCode);
            Assert.Equal(CrossmatchClinicalStatus.Compatible, row.CrossmatchStatus);
            Assert.Null(row.WardAppearance);
            var transit = Assert.Single(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId);
            Assert.Equal("Test RBC", transit.ProductName);
            Assert.Equal(_factory.Clock.UtcNow.AddDays(20), transit.UnitExpiresUtc);
            Assert.Equal("O+", transit.UnitBloodType);
            Assert.Equal("ACC-OUTISS", transit.SpecimenAccession);
            Assert.Equal(_factory.Clock.UtcNow.AddDays(2), transit.CrossmatchExpiresUtc);
            Assert.Equal(CrossmatchResult.Compatible, transit.LatestCrossmatchResult);
            Assert.Equal("RN Alvarez", transit.IssuedTo);
            Assert.Equal(IssueType.Standard, transit.IssueType);
            Assert.Equal("tech-test", transit.IssuedBy);
            Assert.Equal("Uncrossmatched release for hemorrhage", transit.EmergencyReleaseDetails);
            Assert.Equal("witness-tech", transit.SecondVerifier);
            Assert.Equal("dr-authorizer", transit.EmergencyReleaseAuthorizer);
            Assert.Equal(UnitAppearance.Acceptable, transit.IssueAppearance);
            Assert.Null(transit.PtagCode);
            Assert.Equal(CrossmatchClinicalStatus.Compatible, transit.CrossmatchStatus);
            saved.EmergencyReleaseDetails = " ";
            saved.SecondVerifier = " ";
            releaseOverride.AuthorizedBy = " ";
            await c.SaveChangesAsync();
            Assert.Null(Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId).EmergencyReleaseDetails);
            Assert.Null(Assert.Single(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId).EmergencyReleaseDetails);
            Assert.Null(Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId).SecondVerifier);
            Assert.Null(Assert.Single(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId).SecondVerifier);
            Assert.Null(Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId).EmergencyReleaseAuthorizer);
            Assert.Null(Assert.Single(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId).EmergencyReleaseAuthorizer);
            saved.EmergencyReleaseDetails = "Uncrossmatched release for hemorrhage";
            saved.SecondVerifier = "witness-tech";
            releaseOverride.AuthorizedBy = "dr-authorizer";
            saved.IssueAppearance = UnitAppearance.Hemolysis;
            saved.PtagCode = " PTAG-OUT ";
            saved.CrossmatchStatus = CrossmatchClinicalStatus.NotCrossmatchedEmergency;
            await c.SaveChangesAsync();
            Assert.Equal(UnitAppearance.Hemolysis, Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId).IssueAppearance);
            Assert.Equal(UnitAppearance.Hemolysis, Assert.Single(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId).IssueAppearance);
            Assert.Equal("PTAG-OUT", Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId).PtagCode);
            Assert.Equal("PTAG-OUT", Assert.Single(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId).PtagCode);
            Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency, Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId).CrossmatchStatus);
            Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency, Assert.Single(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId).CrossmatchStatus);
            Assert.Equal(s.Mrn, row.MedicalRecordNumber);
            Assert.Equal(s.DateOfBirth, row.DateOfBirth);
            Assert.False(string.IsNullOrWhiteSpace(row.PatientDisplayName));
            Assert.False(string.IsNullOrWhiteSpace(row.CurrentBloodType));
            Assert.Null(row.WardReceivedUtc);
        }

        await using (var c = _factory.Create())
        {
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
            Assert.DoesNotContain(await Issuing(c).ListInTransitAsync(), t => t.IssueId == issueId);
            var row = Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
            Assert.Equal(s.UnitNumber, row.UnitNumber);
            Assert.Equal("Test RBC", row.ProductName);
            Assert.Equal("O+", row.UnitBloodType);
            Assert.Equal("ACC-OUTISS", row.SpecimenAccession);
            Assert.Equal(_factory.Clock.UtcNow.AddDays(2), row.CrossmatchExpiresUtc);
            Assert.Equal(CrossmatchResult.Compatible, row.LatestCrossmatchResult);
            Assert.Equal("RN Alvarez", row.IssuedTo);
            Assert.Equal("CLR-OUT", row.CoolerId);
            Assert.Equal("Trauma Bay 2", row.IssuedToLocation);
            Assert.Equal(IssueType.Standard, row.IssueType);
            Assert.Equal("tech-test", row.IssuedBy);
            Assert.Equal("Uncrossmatched release for hemorrhage", row.EmergencyReleaseDetails);
            Assert.Equal("witness-tech", row.SecondVerifier);
            Assert.Equal("dr-authorizer", row.EmergencyReleaseAuthorizer);
            Assert.Equal(UnitAppearance.Hemolysis, row.IssueAppearance);
            Assert.Equal("PTAG-OUT", row.PtagCode);
            Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency, row.CrossmatchStatus);
            Assert.Equal(UnitAppearance.Acceptable.ToString(), row.WardAppearance);
            Assert.NotNull(row.WardReceivedUtc);
            var blanked = await c.Issues.SingleAsync(i => i.Id == issueId);
            blanked.IssuedBy = " ";
            blanked.EmergencyReleaseDetails = " ";
            blanked.SecondVerifier = " ";
            var blankedOverride = await c.Overrides.SingleAsync(o => o.Id == blanked.OverrideId);
            blankedOverride.AuthorizedBy = " ";
            blanked.IssueAppearance = UnitAppearance.Acceptable;
            blanked.PtagCode = " ";
            blanked.CrossmatchStatus = CrossmatchClinicalStatus.Compatible;
            blanked.WardAppearance = UnitAppearance.Hemolysis;
            await c.SaveChangesAsync();
            var cleared = Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
            Assert.Null(cleared.IssuedBy);
            Assert.Null(cleared.EmergencyReleaseDetails);
            Assert.Null(cleared.SecondVerifier);
            Assert.Null(cleared.EmergencyReleaseAuthorizer);
            Assert.Equal(UnitAppearance.Acceptable, cleared.IssueAppearance);
            Assert.Null(cleared.PtagCode);
            Assert.Equal(CrossmatchClinicalStatus.Compatible, cleared.CrossmatchStatus);
            Assert.Equal(UnitAppearance.Hemolysis.ToString(), cleared.WardAppearance);
        }

        await using var ctx = _factory.Create();
        Assert.True((await Issuing(ctx).ReturnUnitAsync(issueId, new ReturnUnitRequest("Not needed after receipt", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true))).Succeeded);
        Assert.DoesNotContain(await Issuing(ctx).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
    }

    [Fact]
    public async Task Transfusion_MissingPatientIdentifiers_IsHardStopped()
    {
        var s = await SeedAsync("TXIDMISS");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
        }

        await using var ctx = _factory.Create();
        var missing = await Issuing(ctx).DocumentTransfusionAsync(
            issueId, new DocumentTransfusionRequest(TransfusionDisposition.Completed, ReactionSuspected: false));
        Assert.False(missing.Succeeded);
        Assert.Contains(missing.Evaluation!.HardStops, r => r.Code == PatientIdentityMatchRule.Code);
        Assert.Equal(UnitStatus.Issued, (await ctx.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    [Fact]
    public async Task Transfusion_MismatchedPatientIdentifiers_IsHardStopped()
    {
        var s = await SeedAsync("TXIDBAD");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(IssueReq(s))).Value!.Id;
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
        }

        await using var ctx = _factory.Create();
        var mismatch = await Issuing(ctx).DocumentTransfusionAsync(
            issueId, TxReq(s) with { PatientIdentifier1Value = "WRONG-MRN" });
        Assert.False(mismatch.Succeeded);
        Assert.Contains(mismatch.Evaluation!.HardStops, r => r.Code == PatientIdentityMatchRule.Code);
        Assert.Equal(UnitStatus.Issued, (await ctx.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    [Fact]
    public async Task Transfusion_IsbtUnit_RequiresIdentityAndMatchingScan()
    {
        var s = await SeedAsync("TXSCAN");
        await AttachIsbtIdentityAsync(s.UnitId);
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        var scan = MatchingScan(s.UnitId);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with { VerifiedScan = scan });
            Assert.True(issued.Succeeded, issued.Error);
            issueId = issued.Value!.Id;
            Assert.True((await Issuing(c).RecordWardReceiptAsync(
                issueId, new WardReceiptRequest("ward-nurse", VerifiedScan: scan, Appearance: UnitAppearance.Acceptable))).Succeeded);
        }

        await using var ctx = _factory.Create();
        var issuing = Issuing(ctx);

        var noScan = await issuing.DocumentTransfusionAsync(issueId, TxReq(s));
        Assert.False(noScan.Succeeded);
        Assert.Contains(IsbtErrorCodes.UnitScanMismatch, noScan.Error);

        var ok = await issuing.DocumentTransfusionAsync(issueId, TxReq(s, bedsideScan: scan));
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal(UnitStatus.Transfused, (await ctx.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    [Fact]
    public async Task Transfusion_RequireSecondVerifier_WithoutElectronicId_NeedsDirectoryUser()
    {
        var s = await SeedAsync("TXDUAL");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.True(issued.Succeeded, issued.Error);
            issueId = issued.Value!.Id;
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable))).Succeeded);
        }

        await using var ctx = _factory.Create();
        // The fixture runs with the second verifier off; turn it on for this test only.
        var required = await ctx.SystemSettings.SingleAsync(x => x.Key == FacilityPolicyKeys.RequireSecondVerifier);
        required.Value = "true";
        ctx.Users.Add(new User { UserName = "ward-nurse", DisplayName = "Ward Nurse", IsActive = true });
        await ctx.SaveChangesAsync();

        try
        {
            var withoutVerifier = await Issuing(ctx).DocumentTransfusionAsync(issueId, TxReq(s));
            Assert.False(withoutVerifier.Succeeded);
            Assert.Contains(withoutVerifier.Evaluation!.HardStops, r => r.Code == DualIdentificationRule.Code);
            Assert.Equal(UnitStatus.Issued, (await ctx.BloodUnits.FindAsync(s.UnitId))!.Status);

            var withVerifier = await Issuing(ctx).DocumentTransfusionAsync(
                issueId, TxReq(s, secondVerifier: "ward-nurse"));
            Assert.True(withVerifier.Succeeded, withVerifier.Error);
            Assert.Equal(UnitStatus.Transfused, (await ctx.BloodUnits.FindAsync(s.UnitId))!.Status);
        }
        finally
        {
            required.Value = "false";
            await ctx.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task WardReceipt_IsbtUnit_RequiresMatchingScan()
    {
        var s = await SeedAsync("WARDSCAN");
        await AttachIsbtIdentityAsync(s.UnitId);
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        var scan = MatchingScan(s.UnitId);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with { VerifiedScan = scan });
            Assert.True(issued.Succeeded, issued.Error);
            issueId = issued.Value!.Id;
        }

        await using var ctx = _factory.Create();
        var issuing = Issuing(ctx);

        var missing = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", Appearance: UnitAppearance.Acceptable));
        Assert.False(missing.Succeeded);
        Assert.Contains(IsbtErrorCodes.UnitScanMismatch, missing.Error);

        var mismatch = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest(
            "ward-nurse",
            VerifiedScan: scan with { Din = "W9999999999999" }, Appearance: UnitAppearance.Acceptable));
        Assert.False(mismatch.Succeeded);
        Assert.True(mismatch.Evaluation!.IsHardStopped);
        Assert.Contains(mismatch.Evaluation.HardStops, r => r.Code == IsbtErrorCodes.UnitScanMismatch);

        var ok = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", VerifiedScan: scan, Appearance: UnitAppearance.Acceptable));
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal("ward-nurse", ok.Value!.WardReceivedBy);
        var stored = await ctx.Issues.FindAsync(issueId);
        Assert.False(string.IsNullOrWhiteSpace(stored!.WardScanJson));
    }

    // TEST-BB-394 (OCD-054): a back-entered issue's ward receipt and transfusion can identify the unit by the DIN on the
    // paper record instead of a scan, only for that issue and only for a role holding issue.downtime-backentry.
    [Fact]
    public async Task DowntimeBackEntry_WardReceiptAndTransfusion_UseThePaperDin_InPlaceOfTheScan()
    {
        var paper = await SeedAsync("BE-BEDSIDE");
        await RecordCompatibleCrossmatchAsync(paper);
        await AllocateAsync(paper);
        await AttachIsbtIdentityAsync(paper.UnitId);
        var live = await SeedAsync("BE-LIVE");
        await RecordCompatibleCrossmatchAsync(live);
        await AllocateAsync(live);
        await AttachIsbtIdentityAsync(live.UnitId);
        var din = $"W{paper.UnitId:D12}";
        await using (var c = _factory.Create())
        {
            // The crossmatch was done before the paper issue.
            foreach (var xm in await c.Crossmatches.Where(x => x.BloodProductId == paper.UnitId).ToListAsync())
            {
                xm.PerformedUtc = _factory.Clock.UtcNow.AddMinutes(-90);
            }

            c.Users.Add(new User { UserName = "be-witness", DisplayName = "Back-entry Witness", IsActive = true });
            await c.SaveChangesAsync();
        }

        long paperIssueId;
        long liveIssueId;
        await using (var c = _factory.Create())
        {
            var backEntered = await Issuing(c).IssueUnitAsync(IssueReq(paper) with
            {
                IssuedUtc = _factory.Clock.UtcNow.AddHours(-1),
                LateEntryReason = "Issued on paper during LIS downtime",
                DowntimeBackEntry = true,
                BackEntryDin = din
            });
            Assert.True(backEntered.Succeeded, backEntered.Error ?? string.Join("; ", backEntered.Evaluation?.Results.Where(r => r.Severity != RuleSeverity.Pass).Select(r => r.Code) ?? []));
            paperIssueId = backEntered.Value!.Id;
            var ordinary = await Issuing(c).IssueUnitAsync(IssueReq(live) with { VerifiedScan = MatchingScan(live.UnitId) });
            Assert.True(ordinary.Succeeded, ordinary.Error);
            liveIssueId = ordinary.Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            // The ordinary issue must still be scanned.
            var notBackEntry = await Issuing(c).DocumentTransfusionAsync(liveIssueId, TxReq(live) with { BackEntryDin = $"W{live.UnitId:D12}" });
            Assert.Contains(notBackEntry.Evaluation!.HardStops, r => r.Code == DowntimeBackEntryRule.NotBackEntryCode);

            var noRole = await Issuing(c, new FixedPermissionEvaluator(3, PermissionCodes.TransfusionDocument))
                .DocumentTransfusionAsync(paperIssueId, TxReq(paper) with { BackEntryDin = din });
            Assert.Contains(noRole.Evaluation!.HardStops, r => r.Code == DowntimeBackEntryRule.PermissionCode);

            var wrongDin = await Issuing(c).DocumentTransfusionAsync(paperIssueId, TxReq(paper) with { BackEntryDin = "W999999999999" });
            Assert.Contains(wrongDin.Evaluation!.HardStops, r => r.Code == DowntimeBackEntryRule.DinCode);

            // Neither a scan nor the paper DIN: the scan is still required.
            Assert.False((await Issuing(c).DocumentTransfusionAsync(paperIssueId, TxReq(paper))).Succeeded);
            Assert.False(await c.TransfusionEvents.AnyAsync(t => t.IssueId == paperIssueId));

            // TEST-BB-395: on In transit the back-entered issue asks for its receipt, not "overdue".
            var transit = Assert.Single(await Issuing(c).ListInTransitAsync(), r => r.IssueId == paperIssueId);
            Assert.True(transit.DowntimeBackEntry);
            Assert.False(transit.IsOverdue);

            WardReceiptRequest Ward(DateTime? at) => new(
                "RN Paper", Appearance: UnitAppearance.Acceptable, BackEntryDin: din.ToLowerInvariant(), ReceivedUtc: at);
            var issuedAt = _factory.Clock.UtcNow.AddHours(-1);
            foreach (var bad in new DateTime?[] { null, issuedAt.AddMinutes(-5), _factory.Clock.UtcNow.AddHours(1) })
            {
                var refused = await Issuing(c).RecordWardReceiptAsync(paperIssueId, Ward(bad));
                Assert.Contains(refused.Evaluation!.HardStops, r => r.Code == DowntimeBackEntryRule.ReceiptTimeCode);
            }

            var ward = await Issuing(c).RecordWardReceiptAsync(paperIssueId, Ward(issuedAt.AddMinutes(10)));
            Assert.True(ward.Succeeded, ward.Error ?? string.Join("; ", ward.Evaluation?.HardStops.Select(r => r.Message) ?? []));
            Assert.Equal(issuedAt.AddMinutes(10), ward.Value!.WardReceivedUtc);
        }

        await using (var c = _factory.Create())
        {
            var receiptAudit = await c.AuditEvents.Where(a => a.EntityType == nameof(Issue) && a.EntityId == paperIssueId
                && a.NewValueJson!.Contains("FromPaperRecord")).SingleAsync();
            Assert.Contains("\"FromPaperRecord\":true", receiptAudit.NewValueJson);
            Assert.Contains("\"Late\":false", receiptAudit.NewValueJson);
        }

        await using (var c = _factory.Create())
        {
            var documented = await Issuing(c).DocumentTransfusionAsync(paperIssueId, TxReq(paper) with
            {
                StartUtc = _factory.Clock.UtcNow.AddMinutes(-50),
                StopUtc = _factory.Clock.UtcNow.AddMinutes(-20),
                BackEntryDin = din,
                // The paper DIN is not electronic identification, so the second verifier is still required.
                SecondVerifier = "be-witness"
            });
            Assert.True(documented.Succeeded, documented.Error ?? string.Join("; ", documented.Evaluation?.HardStops.Select(r => r.Code + " " + r.Message) ?? []));
            Assert.Equal(DowntimeBackEntryRule.PaperDinMethod, documented.Value!.UnitIdentificationMethod);
            Assert.Null(documented.Value.BedsideScanVerificationJson);
        }
    }

    private static ComponentScanVerificationRequest MatchingScan(long unitId) =>
        new($"W{unitId:D12}", "E0206000", null, "DEMO", "2250200");

    private async Task AttachIsbtIdentityAsync(long unitId)
    {
        await using var c = _factory.Create();
        var unit = await c.BloodUnits.FindAsync(unitId);
        Assert.NotNull(unit);
        var din = $"W{unitId:D12}";
        unit!.Din = din;
        unit.ProductCodeData = "E0206000";
        unit.AboRhdCode = "DEMO";
        unit.ExpirationEncoded = "2250200";
        unit.ComponentIdentity = $"{din}|E0206000";
        await c.SaveChangesAsync();
    }

    [Fact]
    public async Task InTransit_Overdue_AndReturnDropsFromWorklist()
    {
        var s = await SeedAsync("TRANSITOV");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            var issued = await Issuing(c).IssueUnitAsync(
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddHours(-5), CoolerId = "CLR-LATE" , LateEntryReason = "Simulated earlier issue for the test" });
            Assert.True(issued.Succeeded);
            issueId = issued.Value!.Id;
            var transit = await Issuing(c).ListInTransitAsync();
            var row = Assert.Single(transit, t => t.IssueId == issueId);
            Assert.True(row.IsOverdue);
        }

        await using var ctx = _factory.Create();
        Assert.True((await Issuing(ctx).ReturnUnitAsync(issueId, new ReturnUnitRequest("Cooler returned unused", TemperatureAcceptable: true, SealIntegrityAcceptable: true, VisualInspectionAcceptable: true))).Succeeded);
        var after = await Issuing(ctx).ListInTransitAsync();
        Assert.DoesNotContain(after, t => t.IssueId == issueId);
    }

    [Fact]
    public async Task ThawedPlasma_IssuesWithoutCrossmatchOrNearExpiryOverride()
    {
        var s = await SeedAsync(
            "THAW-FFP",
            requiresCrossmatch: true,
            componentClass: ComponentClass.Plasma);
        var expires = _factory.Clock.UtcNow.AddHours(24);
        await using var c = _factory.Create();
        var unit = await c.BloodUnits.FindAsync(s.UnitId);
        unit!.Status = UnitStatus.Assigned;
        unit.ExpiresUtc = expires;
        var allocation = new Allocation
        {
            BloodProductId = s.UnitId,
            PatientId = s.PatientId,
            SpecimenId = s.SpecimenId,
            Status = AllocationStatus.Reserved,
            AllocatedUtc = _factory.Clock.UtcNow,
            AllocatedBy = "tech",
            CreatedBy = "tech"
        };
        c.Allocations.Add(allocation);
        await c.SaveChangesAsync();
        c.AutoThawRequests.Add(new AutoThawRequest
        {
            AllocationId = allocation.Id,
            BloodUnitId = s.UnitId,
            Status = AutoThawRequestStatus.Completed,
            ThawUtc = _factory.Clock.UtcNow,
            PreviousExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            NewExpiresUtc = expires,
            CompletedUtc = _factory.Clock.UtcNow,
            CompletedBy = "tech",
            CreatedBy = "tech"
        });
        await c.SaveChangesAsync();

        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));

        Assert.True(issued.Succeeded, issued.Error);
        Assert.DoesNotContain(
            issued.Evaluation?.Warnings ?? [],
            r => r.Code == BloodUnitExpirationRule.NearExpiryCode);
        Assert.Equal(UnitStatus.Issued, (await c.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    // TEST-BB-387 (OCD-053): the Ready to issue board offers emergency release on a specimen-blocked row only for an eligible unit.
    [Fact]
    public async Task ReadyToIssue_ExpiredSpecimen_OffersEmergencyOnlyForGroupORedCells()
    {
        var groupO = await SeedAsync("PRE-BOARD-O");
        var groupA = await SeedAsync("PRE-BOARD-A", patientAbo: AboGroup.A, unitAbo: AboGroup.A);
        await AllocateAsync(groupO);
        await AllocateAsync(groupA);
        await using (var setup = _factory.Create())
        {
            foreach (var specimen in await setup.Specimens.Where(x => x.Id == groupO.SpecimenId || x.Id == groupA.SpecimenId).ToListAsync())
            {
                specimen.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
            }

            await setup.SaveChangesAsync();
        }

        await using var c = _factory.Create();
        var rows = await Issuing(c).ListReadyToIssueAllocationsAsync();

        var oRow = Assert.Single(rows, r => r.BloodUnitId == groupO.UnitId);
        Assert.True(oRow.SpecimenBlocksIssue);
        Assert.True(oRow.PreSpecimenEmergencyAllowed);
        Assert.Contains("OCD-053", oRow.SpecimenBlockReason);

        var aRow = Assert.Single(rows, r => r.BloodUnitId == groupA.UnitId);
        Assert.True(aRow.SpecimenBlocksIssue);
        Assert.False(aRow.PreSpecimenEmergencyAllowed);
        Assert.Contains("only group O red cells", aRow.SpecimenBlockReason);
    }

    // TEST-BB-386 (OCD-053): emergency release of group O red cells goes ahead without a usable specimen, as an override that names the rule.
    [Fact]
    public async Task EmergencyRelease_WithAnExpiredSpecimen_GroupORedCells_IssuesUnderOverride()
    {
        var s = await SeedAsync("PRE-SPEC");
        await AllocateAsync(s);
        await using (var setup = _factory.Create())
        {
            var specimen = await setup.Specimens.SingleAsync(x => x.Id == s.SpecimenId);
            specimen.ExpiresUtc = _factory.Clock.UtcNow.AddHours(-1);
            await setup.SaveChangesAsync();
        }

        await using var c = _factory.Create();
        var routine = await Issuing(c).IssueUnitAsync(IssueReq(s));
        Assert.False(routine.Succeeded);
        Assert.Contains(routine.Evaluation!.HardStops, h => h.Code == SpecimenExpirationRule.ExpiredCode);

        var asked = await Issuing(c).IssueUnitAsync(IssueReq(s, IssueType.EmergencyRelease));
        Assert.False(asked.Succeeded);
        Assert.Empty(asked.Evaluation!.HardStops);
        Assert.Contains(asked.Evaluation.Warnings, w => w.Code == EmergencyPreSpecimenRule.Code);

        var issued = await Issuing(c).IssueUnitAsync(IssueReq(
            s, IssueType.EmergencyRelease, overrideReason: "Trauma bay, no sample yet", authorizedBy: "dr-trauma"));
        Assert.True(issued.Succeeded, issued.Error);
        var exception = await c.Overrides.AsNoTracking().SingleAsync(o => o.Id == issued.Value!.OverrideId);
        Assert.Contains(EmergencyPreSpecimenRule.Code, exception.RuleCode);
        Assert.Equal("dr-trauma", exception.AuthorizedBy);
    }

    // TEST-BB-383 (OCD-052): the active compatibility table makes a gate warning a hard stop for its component class.
    [Fact]
    public async Task Issue_ActiveCompatibilityTable_MakesAWarningAHardStop_AndNeverSoftens()
    {
        var s = await SeedAsync("COMPAT-TABLE");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long versionId;
        await using (var setup = _factory.Create())
        {
            var unit = await setup.BloodUnits.SingleAsync(u => u.Id == s.UnitId);
            unit.ExpiresUtc = _factory.Clock.UtcNow.AddHours(3);
            var version = new CompatibilityRuleVersion
            {
                Version = "TABLE-GATE", PolicyVersion = "P-GATE", EffectiveDate = new DateOnly(2026, 11, 1),
                Notes = "Gate test", IsActive = true
            };
            setup.CompatibilityRuleVersions.Add(version);
            await setup.SaveChangesAsync();
            setup.CompatibilityRules.AddRange(
                new CompatibilityRule
                {
                    CompatibilityRuleVersionId = version.Id, RuleCode = BloodUnitExpirationRule.NearExpiryCode,
                    ComponentClass = ComponentClass.RedBloodCells, Severity = "HardStop", Description = "Near-expiry red cells need a fresh unit.", IsActive = true
                },
                // A row asking to soften the ABO hard stop is ignored by the gate.
                new CompatibilityRule
                {
                    CompatibilityRuleVersionId = version.Id, RuleCode = AboCompatibilityRule.AboCode,
                    ComponentClass = ComponentClass.RedBloodCells, Severity = "Warning", Description = "Softening attempt.", IsActive = true
                });
            await setup.SaveChangesAsync();
            versionId = version.Id;
        }

        try
        {
            await using var c = _factory.Create();
            var blocked = await Issuing(c).IssueUnitAsync(IssueReq(s));
            Assert.False(blocked.Succeeded);
            var stop = Assert.Single(blocked.Evaluation!.HardStops, h => h.Code == BloodUnitExpirationRule.NearExpiryCode);
            Assert.EndsWith(CompatibilityTablePolicy.EscalatedNote, stop.Message);
            Assert.NotEqual(UnitStatus.Issued, (await c.BloodUnits.AsNoTracking().SingleAsync(u => u.Id == s.UnitId)).Status);
        }
        finally
        {
            await using var cleanup = _factory.Create();
            var version = await cleanup.CompatibilityRuleVersions.SingleAsync(v => v.Id == versionId);
            version.IsActive = false;
            await cleanup.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Issue_SucceedsWithoutPtagScan_WhenLayoutOmitsTheField()
    {
        var s = await SeedAsync("PTAG-OFF");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        await SavePtagLayoutAsync(includePtagId: false);

        await using var c = _factory.Create();
        var issued = await IssuingWithPtag(c).IssueUnitAsync(IssueReq(s));

        Assert.True(issued.Succeeded, issued.Error);
        Assert.Null(issued.Value!.PtagCode);
    }

    [Fact]
    public async Task Issue_RequiresMatchingPtagScan_WhenTheFieldIsPlaced()
    {
        var s = await SeedAsync("PTAG-ON");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);
        await SavePtagLayoutAsync(includePtagId: true);

        await using (var missing = _factory.Create())
        {
            var blocked = await IssuingWithPtag(missing).IssueUnitAsync(IssueReq(s));
            Assert.False(blocked.Succeeded);
            Assert.Contains("P-tag ID", blocked.Error);
        }

        await using (var wrong = _factory.Create())
        {
            wrong.PtagIdentifiers.Add(new PtagIdentifier
            {
                Code = "PABCDEF12",
                BloodProductId = s.UnitId,
                PatientId = s.PatientId
            });
            await wrong.SaveChangesAsync();
            var blocked = await IssuingWithPtag(wrong).IssueUnitAsync(IssueReq(s) with { PtagId = "PWRONG" });
            Assert.False(blocked.Succeeded);
            Assert.Contains("does not match", blocked.Error);
            Assert.NotEqual(UnitStatus.Issued, (await wrong.BloodUnits.FindAsync(s.UnitId))!.Status);
        }

        await using var ok = _factory.Create();
        var issued = await IssuingWithPtag(ok).IssueUnitAsync(IssueReq(s) with { PtagId = "pabcdef12" });
        Assert.True(issued.Succeeded, issued.Error);
        Assert.Equal("PABCDEF12", issued.Value!.PtagCode);
        var consumed = await ok.PtagIdentifiers.SingleAsync(p => p.Code == "PABCDEF12");
        Assert.Equal(issued.Value.Id, consumed.IssueId);
        Assert.NotNull(consumed.ConsumedUtc);
    }

    private IssuingService IssuingWithPtag(BloodBankDbContext c)
    {
        var audit = new AuditWriter(c, _factory.Clock, _factory.CurrentUser);
        return new IssuingService(
            new InventoryRepository(c), new EfRepository<Issue>(c), new EfRepository<Allocation>(c),
            new EfRepository<Crossmatch>(c), new EfRepository<Return>(c), new EfRepository<TransfusionEvent>(c),
            new EfRepository<Override>(c), new EfRepository<Patient>(c), new EfRepository<Specimen>(c),
            new EfRepository<ProductType>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<ExceptionDefinition>(c),
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<ProductAttribute>(c),
            new EfRepository<ProductAttributeAssignment>(c),
            new EfRepository<Order>(c),
            new EfRepository<User>(c),
            new EfRepository<InventoryLocation>(c),
            BloodAttrCompat(c),
            AntibodyScreenCompat(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            new ReactionInvestigationService(
                new EfRepository<ReactionInvestigation>(c),
                new EfRepository<TransfusionEvent>(c),
                new InventoryRepository(c),
                c, _factory.Clock, _factory.CurrentUser, audit),
            new FixedPermissionEvaluator(3),
            c, _factory.Clock, _factory.CurrentUser, audit,
            workups: new EfRepository<AntibodyIdentificationWorkup>(c),
            requirementDefinitions: new EfRepository<SpecialRequirementDefinition>(c),
            antibodies: new EfRepository<AntibodyHistory>(c),
            orderActions: new EfRepository<OrderAction>(c),
            labelLayouts: new EfRepository<LabelLayout>(c),
            ptagIdentifiers: new EfRepository<PtagIdentifier>(c));
    }

    private async Task SavePtagLayoutAsync(bool includePtagId)
    {
        await using var c = _factory.Create();
        var existing = await c.LabelLayouts.Where(l => l.Kind == LabelLayoutKind.Ptag).ToListAsync();
        c.LabelLayouts.RemoveRange(existing);
        var document = LabelLayoutDefaults.Ptag();
        if (includePtagId)
        {
            document.Page1.Add(new PlacedLabelField
            {
                Key = LabelFields.PtagId,
                X = 20,
                Y = 520,
                FontHeight = 28,
                Barcode = true
            });
        }

        c.LabelLayouts.Add(new LabelLayout
        {
            Kind = LabelLayoutKind.Ptag,
            DocumentJson = JsonSerializer.Serialize(document, LabelFields.JsonOptions)
        });
        await c.SaveChangesAsync();
    }

    [Fact]
    public async Task IssuedBoards_NameTheStorageLocationWhenThePlaceIsALocationCode()
    {
        const string key = "LOC221";
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
                Name = "Test RBC",
                ComponentClass = ComponentClass.RedBloodCells
            };
            c.Patients.Add(patient);
            c.ProductTypes.Add(product);
            var named = new InventoryLocation { Code = $"F1{key}", Name = " Issue fridge " };
            var blank = new InventoryLocation { Code = $"F2{key}", Name = "   " };
            var coded = new InventoryLocation { Code = $"OR{key}", Name = "Main OR" };
            var alsoNamed = new InventoryLocation { Code = $"X{key}", Name = $"OR{key}" };
            c.InventoryLocations.AddRange(named, blank, coded, alsoNamed);
            await c.SaveChangesAsync();
            blankId = blank.Id;

            BloodUnit Unit(string suffix) => new()
            {
                UnitNumber = $"U-{key}-{suffix}",
                ProductTypeId = product.Id,
                Abo = AboGroup.O,
                RhD = RhType.Positive,
                ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
                Status = UnitStatus.Issued
            };

            var fridgeUnit = Unit("FR");
            var blankUnit = Unit("BL");
            var wardUnit = Unit("WD");
            var ambiguousUnit = Unit("AM");
            var transfusedUnit = Unit("TX");
            c.BloodUnits.AddRange(fridgeUnit, blankUnit, wardUnit, ambiguousUnit, transfusedUnit);
            await c.SaveChangesAsync();

            Issue Issued(BloodUnit unit, string place, bool retrospective = false) => new()
            {
                BloodProductId = unit.Id,
                PatientId = patient.Id,
                IssuedUtc = _factory.Clock.UtcNow,
                IssuedBy = "tech",
                IssuedToLocation = place,
                Status = IssueStatus.Issued,
                TestsIncompleteAtIssue = retrospective,
                CrossmatchStatus = CrossmatchClinicalStatus.NotPerformed
            };

            var fridgeIssue = Issued(fridgeUnit, $" f1{key.ToLower()} ", retrospective: true);
            var blankIssue = Issued(blankUnit, $"F2{key}");
            var wardIssue = Issued(wardUnit, " 4W Med/Surg ");
            var ambiguousIssue = Issued(ambiguousUnit, $"or{key.ToLower()}");
            var transfusedIssue = new Issue
            {
                BloodProductId = transfusedUnit.Id,
                PatientId = patient.Id,
                IssuedUtc = _factory.Clock.UtcNow,
                IssuedBy = "tech",
                IssuedToLocation = "4W Oncology",
                Status = IssueStatus.Transfused
            };
            c.Issues.AddRange(fridgeIssue, blankIssue, wardIssue, ambiguousIssue, transfusedIssue);
            await c.SaveChangesAsync();
            namedIssueId = fridgeIssue.Id;
            c.TransfusionEvents.Add(new TransfusionEvent
            {
                IssueId = transfusedIssue.Id,
                BloodProductId = transfusedUnit.Id,
                PatientId = patient.Id,
                Location = $" f1{key.ToLower()} ",
                DocumentedBy = "tech",
                FinalDisposition = TransfusionDisposition.Completed
            });
            await c.SaveChangesAsync();
        }

        await using var read = _factory.Create();
        var issuing = Issuing(read);
        var outstanding = await issuing.ListOutstandingIssuedAsync();
        var inTransit = await issuing.ListInTransitAsync();
        var retrospective = await issuing.ListPendingRetrospectiveCrossmatchesAsync();

        OutstandingIssueWorkItemDto Board(string suffix) =>
            Assert.Single(outstanding, row => row.UnitNumber == $"U-{key}-{suffix}");

        var fridge = Board("FR");
        var blankRow = Board("BL");
        var ward = Board("WD");
        var ambiguous = Board("AM");
        Assert.Equal("Issue fridge", fridge.IssuedToLocation);
        Assert.DoesNotContain($"F1{key}", fridge.IssuedToLocation, StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"Location {blankId}", blankRow.IssuedToLocation);
        Assert.DoesNotContain($"F2{key}", blankRow.IssuedToLocation, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("4W Med/Surg", ward.IssuedToLocation);
        Assert.Equal($"or{key.ToLower()}", ambiguous.IssuedToLocation);
        Assert.Equal("Issue fridge", Assert.Single(inTransit, row => row.UnitNumber == $"U-{key}-FR").IssuedToLocation);
        Assert.Equal("Issue fridge", Assert.Single(retrospective, row => row.UnitNumber == $"U-{key}-FR").IssuedToLocation);
        Assert.Equal($" f1{key.ToLower()} ", (await read.Issues.FindAsync(namedIssueId))!.IssuedToLocation);

        PatientProductHistoryService History(BloodBankDbContext db, bool withLocations) => new(
            new EfRepository<Allocation>(db),
            new EfRepository<Crossmatch>(db),
            new EfRepository<Issue>(db),
            new EfRepository<Return>(db),
            new EfRepository<TransfusionEvent>(db),
            new EfRepository<BloodUnit>(db),
            new EfRepository<ProductType>(db),
            new EfRepository<Encounter>(db),
            new EfRepository<Order>(db),
            new EfRepository<Specimen>(db),
            new EfRepository<PatientBloodTypeHistory>(db),
            new EfRepository<Override>(db),
            withLocations ? new EfRepository<InventoryLocation>(db) : null);

        var patientId = fridge.PatientId;
        var namedHistory = await History(read, withLocations: true).ListByPatientAsync(patientId);
        Assert.Equal("Issue fridge", Assert.Single(namedHistory, row => row.UnitNumber == $"U-{key}-FR" && row.EventType == PatientProductHistoryEventType.Issued).IssuedToLocation);
        Assert.Equal($"Location {blankId}", Assert.Single(namedHistory, row => row.UnitNumber == $"U-{key}-BL").IssuedToLocation);
        Assert.Equal("4W Med/Surg", Assert.Single(namedHistory, row => row.UnitNumber == $"U-{key}-WD").IssuedToLocation);
        Assert.Equal($"or{key.ToLower()}", Assert.Single(namedHistory, row => row.UnitNumber == $"U-{key}-AM").IssuedToLocation);
        Assert.Equal("Issue fridge", Assert.Single(namedHistory, row => row.EventType == PatientProductHistoryEventType.Transfused).IssuedToLocation);
        Assert.Equal("4W Oncology", Assert.Single(namedHistory, row => row.UnitNumber == $"U-{key}-TX" && row.EventType == PatientProductHistoryEventType.Issued).IssuedToLocation);

        var unread = await History(read, withLocations: false).ListByPatientAsync(patientId);
        var unreadFridge = Assert.Single(unread, row => row.UnitNumber == $"U-{key}-FR" && row.EventType == PatientProductHistoryEventType.Issued);
        Assert.Equal($"f1{key.ToLower()}", unreadFridge.IssuedToLocation);
        Assert.DoesNotContain("Issue fridge", unreadFridge.IssuedToLocation);
    }
}
