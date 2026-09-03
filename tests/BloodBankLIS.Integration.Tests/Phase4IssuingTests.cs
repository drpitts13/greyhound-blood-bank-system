using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
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
            new EfRepository<AntibodyHistory>(c));

    private CompatibilityService Compatibility(BloodBankDbContext c) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            BloodAttrCompat(c), AntibodyScreenCompat(c), c, _factory.Clock, _factory.CurrentUser);

    private IssuingService Issuing(BloodBankDbContext c)
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
            BloodAttrCompat(c),
            new FacilityPolicyService(new EfRepository<SystemSetting>(c)),
            new ReactionInvestigationService(
                new EfRepository<ReactionInvestigation>(c),
                new EfRepository<TransfusionEvent>(c),
                new InventoryRepository(c),
                c, _factory.Clock, _factory.CurrentUser, audit),
            new FixedPermissionEvaluator(3),
            c, _factory.Clock, _factory.CurrentUser, audit);
    }

    private sealed record Scenario(long PatientId, long SpecimenId, long UnitId, long ProductTypeId, string Mrn);

    private static IssueUnitRequest IssueReq(
        Scenario s,
        IssueType issueType = IssueType.Standard,
        string? overrideReason = null,
        string? authorizedBy = null) =>
        new(s.UnitId, s.PatientId,
            PatientIdentifier1Value: s.Mrn,
            PatientIdentifier2Value: "1975-06-01",
            IssueType: issueType,
            OverrideReason: overrideReason,
            AuthorizedBy: authorizedBy);

    /// <summary>
    /// Seeds a patient with a known current ABO/Rh, an accepted specimen, a product
    /// type, and an Available unit. Caller controls compatibility via the ABO/Rh args.
    /// </summary>
    private async Task<Scenario> SeedAsync(
        string key,
        AboGroup patientAbo = AboGroup.O, RhType patientRh = RhType.Positive,
        AboGroup unitAbo = AboGroup.O, RhType unitRh = RhType.Positive,
        bool requiresCrossmatch = true)
    {
        await using var c = _factory.Create();

        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}",
            LastName = "Issue",
            FirstName = "Test",
            DateOfBirth = new DateOnly(1975, 6, 1)
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

        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = patientAbo,
            RhD = patientRh,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

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

        return new Scenario(patient.Id, specimen.Id, unit.Id, productType.Id, $"MRN-{key}");
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
            issueId = issued.Value.Id;
        }

        await using (var verify = _factory.Create())
        {
            Assert.Equal(UnitStatus.Issued, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
            Assert.Equal(AllocationStatus.Consumed, await verify.Allocations.Where(a => a.BloodProductId == s.UnitId).Select(a => a.Status).SingleAsync());
        }

        await using (var c = _factory.Create())
        {
            var transfused = await Issuing(c).DocumentTransfusionAsync(issueId, new DocumentTransfusionRequest(TransfusionDisposition.Completed));
            Assert.True(transfused.Succeeded);
        }

        await using (var verify = _factory.Create())
        {
            Assert.Equal(UnitStatus.Transfused, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
            Assert.Equal(IssueStatus.Transfused, (await verify.Issues.FindAsync(issueId))!.Status);
        }
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
            issueId = issued.Value.Id;
        }

        await using (var verify = _factory.Create())
        {
            var ovr = await verify.Overrides.Where(o => o.ContextType == nameof(Issue)).SingleAsync();
            Assert.Equal(OverrideAction.EmergencyRelease, ovr.Action);
            Assert.Contains(CrossmatchValidityRule.Code, ovr.RuleCode);

            var overrideAudit = await verify.AuditEvents.Where(a => a.EventType == AuditEventType.Override).SingleAsync();
            Assert.Equal("Massive hemorrhage, uncrossmatched O units required", overrideAudit.Reason);

            Assert.Equal(UnitStatus.Issued, (await verify.BloodUnits.FindAsync(s.UnitId))!.Status);
            _ = issueId;
        }
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
        Assert.Contains(result.Evaluation.HardStops, r => r.Code == ElectronicCrossmatchEligibilityRule.Code);
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
            var tx = await Issuing(c).DocumentTransfusionAsync(
                issueId, new DocumentTransfusionRequest(TransfusionDisposition.Completed, ReactionSuspected: true));
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
            var tx = await Issuing(c).DocumentTransfusionAsync(
                issueId, new DocumentTransfusionRequest(TransfusionDisposition.Stopped, ReactionSuspected: true));
            Assert.True(tx.Succeeded);
        }

        await using var verify = _factory.Create();
        var unit = await verify.BloodUnits.FindAsync(s.UnitId);
        Assert.Equal(UnitStatus.Quarantine, unit!.Status);
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
            await Issuing(c).DocumentTransfusionAsync(
                issueId, new DocumentTransfusionRequest(TransfusionDisposition.Completed, ReactionSuspected: true));
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
    public async Task ElectronicCrossmatch_WithoutSecondAbo_IsBlocked()
    {
        var s = await SeedAsync("ONEABO", requiresCrossmatch: false);
        await using var ctx = _factory.Create();
        var result = await Compatibility(ctx).RecordCrossmatchAsync(
            new RecordCrossmatchRequest(s.UnitId, s.PatientId, s.SpecimenId, CrossmatchMethod.Electronic));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Evaluation!.HardStops, r => r.Code == ElectronicCrossmatchEligibilityRule.Code);
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
}
