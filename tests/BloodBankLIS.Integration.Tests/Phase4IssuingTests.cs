using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Application.Issuing;
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
            workups: new EfRepository<AntibodyIdentificationWorkup>(c));

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
            antibodies: new EfRepository<AntibodyHistory>(c));
    }

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
        DateOnly? dateOfBirth = null)
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
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = requiresCrossmatch
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
            Status = UnitStatus.Available
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
                overrideReason: "Uncrossmatched release for hemorrhage", authorizedBy: "dr-authorizer"));
            Assert.True(issued.Succeeded);
            issueId = issued.Value!.Id;

            var pending = await Issuing(c).ListPendingRetrospectiveCrossmatchesAsync();
            Assert.Contains(pending, p => p.IssueId == issueId);
            var retro = pending.Single(p => p.IssueId == issueId);
            Assert.Equal(s.Mrn, retro.MedicalRecordNumber);
            Assert.Equal(s.UnitNumber, retro.UnitNumber);
            Assert.Equal(s.SpecimenId, retro.SpecimenId);
            Assert.Equal("ACC-RETROXM", retro.AccessionNumber);
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
            Assert.Contains(pending, p => p.IssueId == issueId);
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
    public async Task ListReadyToIssue_SurfacesUnitPatientAndXmStatusAfterAllocate()
    {
        var s = await SeedAsync("RDYISS");
        await RecordCompatibleCrossmatchAsync(s);
        await AllocateAsync(s);

        await using var c = _factory.Create();
        var row = Assert.Single(await Issuing(c).ListReadyToIssueAllocationsAsync(), r => r.BloodUnitId == s.UnitId);
        Assert.Equal(s.UnitNumber, row.UnitNumber);
        Assert.Equal(s.Mrn, row.MedicalRecordNumber);
        Assert.Equal(s.DateOfBirth, row.DateOfBirth);
        Assert.Equal(s.PatientId, row.PatientId);
        Assert.False(string.IsNullOrWhiteSpace(row.PatientDisplayName));
        Assert.Equal(CrossmatchResult.Compatible, row.LatestCrossmatchResult);
        Assert.Equal(ProductAllocationDisplayStatus.ReadyForIssue, row.DisplayStatus);

        Assert.True((await Issuing(c).IssueUnitAsync(IssueReq(s))).Succeeded);
        Assert.DoesNotContain(await Issuing(c).ListReadyToIssueAllocationsAsync(), r => r.BloodUnitId == s.UnitId);
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
            var issued = await Issuing(c).IssueUnitAsync(IssueReq(s) with { CoolerId = "CLR-OUT" });
            Assert.True(issued.Succeeded);
            issueId = issued.Value!.Id;

            var row = Assert.Single(await Issuing(c).ListOutstandingIssuedAsync(), i => i.IssueId == issueId);
            Assert.Equal(s.UnitNumber, row.UnitNumber);
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
            Assert.NotNull(row.WardReceivedUtc);
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
}
