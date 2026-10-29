using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
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

    private IssuingService Issuing(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
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
            electronicCrossmatch: new ElectronicCrossmatchEligibilityService(
                new EfRepository<Patient>(c),
                new EfRepository<PatientBloodTypeHistory>(c),
                new EfRepository<AntibodyHistory>(c),
                new EfRepository<AntibodyIdentificationWorkup>(c),
                AntibodyScreenCompat(c),
                new FacilityPolicyService(new EfRepository<SystemSetting>(c))));
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
            AuthorizedBy: authorizedBy);

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
            issued.Value!.Id, new WardReceiptRequest("ward-nurse"));
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
            var receipt = await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"));
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

    [Fact]
    public async Task MarkTransfused_CompletesIssuedUnit()
    {
        var s = await SeedAsync("MARKTX");
        await RecordCompatibleCrossmatchAsync(s);

        await using var c = _factory.Create();
        var reserved = await Compatibility(c).AllocateUnitAsync(
            new AllocateUnitRequest(s.UnitId, s.PatientId, s.SpecimenId));
        Assert.True(reserved.Succeeded, reserved.Error);

        var issued = await Issuing(c).IssueUnitAsync(IssueReq(s));
        Assert.True(issued.Succeeded, issued.Error);

        var marked = await Issuing(c).MarkTransfusedAsync(issued.Value!.Id);
        Assert.True(marked.Succeeded, marked.Error);
        Assert.Equal(TransfusionDisposition.Completed, marked.Value!.FinalDisposition);

        var unit = await c.BloodUnits.FindAsync(s.UnitId);
        Assert.Equal(UnitStatus.Transfused, unit!.Status);
        var issue = await c.Issues.FindAsync(issued.Value.Id);
        Assert.Equal(IssueStatus.Transfused, issue!.Status);
        Assert.NotNull(issue.WardReceivedUtc);
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
        Assert.True((await Issuing(ctx).ReturnUnitAsync(issueId, new ReturnUnitRequest("Never transfused"))).Succeeded);
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
            var returned = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest("Not needed; cooler intact"));
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
            var returned = await Issuing(c).ReturnUnitAsync(issueId, new ReturnUnitRequest("Out of temperature range", TemperatureAcceptable: false));
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
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-31) })).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var available = await Issuing(c).ReturnUnitAsync(
                issueId,
                new ReturnUnitRequest("Still unused", Destination: UnitStatus.Available));
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
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-40) })).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var available = await Issuing(c).ReturnUnitAsync(
                issueId,
                new ReturnUnitRequest("Caller says the window is fine", TimeOutOfStorageAcceptable: true, Destination: UnitStatus.Available));
            Assert.False(available.Succeeded);
            Assert.Contains("30 minutes", available.Error, StringComparison.OrdinalIgnoreCase);

            var row = Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
            Assert.True(row.StorageTimeOutGoverned);
            Assert.False(row.StorageTimeOutAcceptable);
            Assert.Equal("Window elapsed", ReturnReissueRule.TimeOutLabel(row.StorageTimeOutGoverned, row.StorageTimeOutAcceptable));
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
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-40) })).Value!.Id;
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
                    Destination: UnitStatus.Available));
            Assert.True(returned.Succeeded, returned.Error);
            Assert.Contains("\"TimeOutOfStorageAcceptable\":true", returned.Value!.ReissueEvaluationJson, StringComparison.Ordinal);
            Assert.Contains("\"TimeOutGoverned\":false", returned.Value.ReissueEvaluationJson, StringComparison.Ordinal);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Available, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    [Fact]
    public async Task Return_AfterThirtyMinutes_DiscardedAllowed()
    {
        var s = await SeedAsync("RETURN-DISP");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        long issueId;
        await using (var c = _factory.Create())
        {
            issueId = (await Issuing(c).IssueUnitAsync(
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddMinutes(-45) })).Value!.Id;
        }

        await using (var c = _factory.Create())
        {
            var disposed = await Issuing(c).ReturnUnitAsync(
                issueId,
                new ReturnUnitRequest("Broken seal after issue", Destination: UnitStatus.Discarded));
            Assert.True(disposed.Succeeded, disposed.Error);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Discarded, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
    }

    [Fact]
    public async Task Return_FailedVisual_DisposesAndLeavesOutstanding()
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
            Assert.DoesNotContain(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
        }

        await using var verify = _factory.Create();
        Assert.Equal(UnitStatus.Discarded, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
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
        }

        await using var ctx = _factory.Create();
        var result = await Compatibility(ctx).RecordCrossmatchAsync(
            new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Electronic));

        Assert.False(result.Succeeded);
        Assert.True(result.Evaluation!.IsHardStopped);
        Assert.Contains(result.Evaluation.HardStops, r => r.Code == ElectronicCrossmatchEligibilityRule.HistoryCode);
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
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"))).Succeeded);
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
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"))).Succeeded);
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
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"))).Succeeded);
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
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"))).Succeeded);
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
            ctx, _factory.Clock, _factory.CurrentUser, audit);

        var closed = await service.UpdateAsync(inv.Id, new UpdateReactionInvestigationRequest(
            null, null, null, null, null, null, null, null, ReactionInvestigationStatus.Closed, 1));
        Assert.False(closed.Succeeded);
        Assert.Contains(ReactionWorkupCompletenessRule.Code, closed.Error);

        var saved = await service.UpdateAsync(inv.Id, new UpdateReactionInvestigationRequest(
            "FNHTR", ReactionSeverity.Mild, "Fever 1.8C", "Non-hemolytic", null, "Continue observation",
            false, false, ReactionInvestigationStatus.UnderReview, null,
            true, "IDs and ABO concordant", true, true, "A Positive", "O Positive",
            DatWorkupResult.Negative, null, true));
        Assert.True(saved.Succeeded);

        var closeOk = await service.UpdateAsync(inv.Id, new UpdateReactionInvestigationRequest(
            null, null, null, null, null, null, null, null, ReactionInvestigationStatus.Closed, 1));
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
        var visual = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", VisualInspectionAcceptable: false));
        Assert.False(visual.Succeeded);
        Assert.Contains("visual inspection", visual.Error, StringComparison.OrdinalIgnoreCase);

        var first = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"));
        Assert.True(first.Succeeded);
        Assert.Equal(UnitAppearance.Acceptable, first.Value!.WardAppearance);

        var dup = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest("another-nurse"));
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
        var returned = await Issuing(ctx).ReturnUnitAsync(issueId, new ReturnUnitRequest("Never arrived on the ward"));
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
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"))).Succeeded);
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
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"))).Succeeded);
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
        Assert.True((await Issuing(ctx).ReturnUnitAsync(issueId, new ReturnUnitRequest("Not needed after receipt"))).Succeeded);
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
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"))).Succeeded);
        }

        await using var ctx = _factory.Create();
        var missing = await Issuing(ctx).DocumentTransfusionAsync(
            issueId, new DocumentTransfusionRequest(TransfusionDisposition.Completed));
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
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"))).Succeeded);
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
                issueId, new WardReceiptRequest("ward-nurse", VerifiedScan: scan))).Succeeded);
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
            Assert.True((await Issuing(c).RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"))).Succeeded);
        }

        await using var ctx = _factory.Create();
        ctx.SystemSettings.Add(new SystemSetting
        {
            Key = FacilityPolicyKeys.RequireSecondVerifier,
            Value = "true",
            Category = "Transfusion"
        });
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
            var flag = await ctx.SystemSettings.SingleOrDefaultAsync(x => x.Key == FacilityPolicyKeys.RequireSecondVerifier);
            if (flag is not null)
            {
                ctx.SystemSettings.Remove(flag);
                await ctx.SaveChangesAsync();
            }
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

        var missing = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse"));
        Assert.False(missing.Succeeded);
        Assert.Contains(IsbtErrorCodes.UnitScanMismatch, missing.Error);

        var mismatch = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest(
            "ward-nurse",
            VerifiedScan: scan with { Din = "W9999999999999" }));
        Assert.False(mismatch.Succeeded);
        Assert.True(mismatch.Evaluation!.IsHardStopped);
        Assert.Contains(mismatch.Evaluation.HardStops, r => r.Code == IsbtErrorCodes.UnitScanMismatch);

        var ok = await issuing.RecordWardReceiptAsync(issueId, new WardReceiptRequest("ward-nurse", VerifiedScan: scan));
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal("ward-nurse", ok.Value!.WardReceivedBy);
        var stored = await ctx.Issues.FindAsync(issueId);
        Assert.False(string.IsNullOrWhiteSpace(stored!.WardScanJson));
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
                IssueReq(s) with { IssuedUtc = _factory.Clock.UtcNow.AddHours(-5), CoolerId = "CLR-LATE" });
            Assert.True(issued.Succeeded);
            issueId = issued.Value!.Id;
            var transit = await Issuing(c).ListInTransitAsync();
            var row = Assert.Single(transit, t => t.IssueId == issueId);
            Assert.True(row.IsOverdue);
        }

        await using var ctx = _factory.Create();
        Assert.True((await Issuing(ctx).ReturnUnitAsync(issueId, new ReturnUnitRequest("Cooler returned unused"))).Succeeded);
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
