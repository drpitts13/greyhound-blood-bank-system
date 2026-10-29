using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class Phase10PatientWorkspaceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public Phase10PatientWorkspaceTests(SqliteContextFactory factory) => _factory = factory;

    private static EncounterService Encounters(BloodBankDbContext c)
    {
        var clock = new FixedClock(DateTime.UtcNow);
        var providers = new OrderingProviderService(new EfRepository<OrderingProvider>(c), c);
        return new(new EfRepository<Encounter>(c), new EfRepository<Patient>(c), new EfRepository<OrderingProvider>(c), providers, c, clock);
    }

    private OrderService Orders(BloodBankDbContext c) =>
        new(new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), new FixedClock(DateTime.UtcNow), c,
            ruleEngine: null,
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            results: new EfRepository<TestResult>(c));

    private static PatientProductHistoryService History(BloodBankDbContext c) =>
        new(new EfRepository<Allocation>(c), new EfRepository<Crossmatch>(c), new EfRepository<Issue>(c),
            new EfRepository<Return>(c), new EfRepository<TransfusionEvent>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), new EfRepository<Encounter>(c), new EfRepository<Order>(c),
            new EfRepository<Specimen>(c), new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<Override>(c));

    private async Task<(Patient patient, OrderingLocation location)> SeedPatientAsync(BloodBankDbContext c)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Test",
            FirstName = "Patient",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Sex = Sex.Female
        };
        c.Patients.Add(patient);
        var loc = new OrderingLocation { Code = $"LOC-{Guid.NewGuid():N}", Name = "Test OR", IsActive = true };
        c.OrderingLocations.Add(loc);
        await c.SaveChangesAsync();
        return (patient, loc);
    }

    [Fact]
    public async Task CreateEncounter_AndOrder_WithSpecimen_Succeeds()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-TEST", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, "4W", "4W", null, null, null, null));
        Assert.True(enc.Succeeded);

        var specimen = new Specimen
        {
            AccessionNumber = $"ACC-{Guid.NewGuid():N}",
            PatientId = patient.Id,
            EncounterId = enc.Value!.Id,
            SpecimenType = "EDTA",
            CollectedUtc = DateTime.UtcNow.AddHours(-1),
            ExpiresUtc = DateTime.UtcNow.AddDays(3),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();

        var provider = new OrderingProvider { ProviderId = "P1", Name = "Dr. Test", IsActive = true };
        c.OrderingProviders.Add(provider);
        await c.SaveChangesAsync();

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value.Id, loc.Id, $"ORD-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Stat, DateTime.UtcNow, provider.Id, OrderSource.Manual, null, null));
        Assert.True(order.Succeeded);
        Assert.True(await c.AuditEvents.AnyAsync(a =>
            a.EventType == AuditEventType.OrderChange
            && a.EntityType == nameof(Order)
            && a.EntityId == order.Value!.Id
            && a.Reason == "Order created."));

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Single(list);
        Assert.Single(list[0].Lines);
        Assert.Equal("ABORH", list[0].Lines[0].TestCode);
        Assert.Equal(specimen.AccessionNumber, list[0].AccessionNumber);
    }

    [Fact]
    public async Task CreateOrder_WithoutEncounter_Fails()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            0, loc.Id, $"ORD-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));

        Assert.False(order.Succeeded);
        Assert.Contains("visit", order.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateOrder_WithMultipleTestsAndProducts_Succeeds()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-MULTI", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        var rbc = new ProductType { ProductCode = "RBC-M", Name = "RBC", ComponentClass = ComponentClass.RedBloodCells, RequiresCrossmatch = false };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-MULTI-{Guid.NewGuid():N}",
            [
                new OrderLineInputDto(OrderCategory.Test, "ABORH", null),
                new OrderLineInputDto(OrderCategory.Test, "ABSC", null),
                new OrderLineInputDto(OrderCategory.Product, null, rbc.Id)
            ],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(order.Succeeded);
        Assert.Equal(OrderCategory.Mixed, order.Value!.OrderCategory);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Equal(3, list[0].Lines.Count);
    }

    [Fact]
    public async Task CreateOrder_WithTestGrouper_ExpandsMemberTests()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-TNS", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        c.TestGroupers.Add(new TestGrouper
        {
            Code = "TNS",
            Name = "Type and Screen",
            MemberTestsJson = TestGrouperMembers.ToJson([
                new TestGrouperMember("ABORH", 1),
                new TestGrouperMember("ABSC", 2)
            ]),
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = DateTime.UtcNow,
            Version = 1
        });
        await c.SaveChangesAsync();

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-TNS-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "TNS", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(order.Succeeded);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        var tests = list[0].Lines.Where(l => l.LineCategory == OrderCategory.Test).Select(l => l.TestCode).ToList();
        Assert.Equal(["ABORH", "ABSC"], tests);
    }

    [Fact]
    public async Task ProductHistory_IsPatientScoped()
    {
        await using var c = _factory.Create();
        var (p1, _) = await SeedPatientAsync(c);
        var (p2, _) = await SeedPatientAsync(c);
        var rbc = new ProductType { ProductCode = "RBC-T", Name = "RBC", ComponentClass = ComponentClass.RedBloodCells };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();

        var unit1 = new BloodUnit
        {
            UnitNumber = $"U-{Guid.NewGuid():N}",
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = DateTime.UtcNow.AddDays(10),
            Status = UnitStatus.Issued
        };
        var unit2 = new BloodUnit
        {
            UnitNumber = $"U-{Guid.NewGuid():N}",
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = DateTime.UtcNow.AddDays(10),
            Status = UnitStatus.Issued
        };
        c.BloodUnits.AddRange(unit1, unit2);
        await c.SaveChangesAsync();

        c.Issues.Add(new Issue
        {
            BloodProductId = unit1.Id,
            PatientId = p1.Id,
            IssuedUtc = DateTime.UtcNow,
            IssuedBy = "tech",
            Status = IssueStatus.Issued
        });
        c.Issues.Add(new Issue
        {
            BloodProductId = unit2.Id,
            PatientId = p2.Id,
            IssuedUtc = DateTime.UtcNow,
            IssuedBy = "tech",
            Status = IssueStatus.Issued
        });
        await c.SaveChangesAsync();

        var h1 = await History(c).ListByPatientAsync(p1.Id);
        var h2 = await History(c).ListByPatientAsync(p2.Id);
        Assert.Single(h1);
        Assert.Single(h2);
        Assert.Equal(PatientProductHistoryEventType.Issued, h1[0].EventType);
        Assert.Null(h1[0].CompatibilityStatus);
        Assert.Null(h1[0].AccessionNumber);
        Assert.Equal(unit1.ExpiresUtc, h1[0].UnitExpiresUtc);
        Assert.Equal("tech", h1[0].PerformedBy);
        Assert.Null(h1[0].ReturnReason);
        Assert.Null(h1[0].ReissueEligible);
        Assert.Null(h1[0].IssuedTo);
        Assert.Null(h1[0].CoolerId);
        Assert.Equal(IssueType.Standard.ToString(), h1[0].IssueType);
        Assert.Null(h1[0].EmergencyReleaseReason);
        Assert.Null(h1[0].SecondVerifier);
        Assert.Null(h1[0].EmergencyReleaseAuthorizer);
        Assert.Equal(UnitAppearance.Acceptable.ToString(), h1[0].IssueAppearance);
        Assert.Null(h1[0].WardAppearance);
        Assert.Null(h1[0].PtagCode);
        Assert.Equal(CrossmatchClinicalStatus.NotPerformed.ToString(), h1[0].IssueCrossmatchStatus);

        var specimen = new Specimen
        {
            AccessionNumber = "ACC-HIST",
            PatientId = p1.Id,
            SpecimenType = "EDTA",
            CollectedUtc = DateTime.UtcNow.AddHours(-2),
            ReceivedUtc = DateTime.UtcNow.AddHours(-1),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        await c.SaveChangesAsync();
        var allocation = new Allocation
        {
            BloodProductId = unit1.Id,
            PatientId = p1.Id,
            SpecimenId = specimen.Id,
            Status = AllocationStatus.Consumed,
            AllocatedUtc = DateTime.UtcNow.AddMinutes(-10),
            AllocatedBy = "tech"
        };
        c.Allocations.Add(allocation);
        await c.SaveChangesAsync();
        var issued = await c.Issues.SingleAsync(i => i.PatientId == p1.Id);
        issued.AllocationId = allocation.Id;
        issued.IssuedTo = "RN Alvarez";
        issued.CoolerId = "CLR-7";
        issued.IssueType = IssueType.EmergencyRelease;
        issued.EmergencyReleaseDetails = " Uncrossmatched release for hemorrhage ";
        issued.SecondVerifier = " witness-tech ";
        issued.IssueAppearance = UnitAppearance.Hemolysis;
        issued.WardReceivedUtc = DateTime.UtcNow;
        issued.WardAppearance = UnitAppearance.Hemolysis;
        issued.PtagCode = " PTAG-HIST ";
        issued.CrossmatchStatus = CrossmatchClinicalStatus.NotCrossmatchedEmergency;
        var releaseOverride = new Override
        {
            Action = OverrideAction.EmergencyRelease,
            ContextType = nameof(Issue),
            ContextId = issued.BloodProductId,
            RuleCode = "ISS-XM-REQUIRED",
            Reason = "Uncrossmatched release for hemorrhage",
            AuthorizedBy = " dr-authorizer ",
            OverriddenUtc = DateTime.UtcNow
        };
        c.Overrides.Add(releaseOverride);
        await c.SaveChangesAsync();
        issued.OverrideId = releaseOverride.Id;
        await c.SaveChangesAsync();

        h1 = (await History(c).ListByPatientAsync(p1.Id)).ToList();
        Assert.Equal("ACC-HIST", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).AccessionNumber);
        Assert.Equal("RN Alvarez", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).IssuedTo);
        Assert.Equal("CLR-7", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).CoolerId);
        Assert.Equal(IssueType.EmergencyRelease.ToString(), Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).IssueType);
        Assert.Equal("Uncrossmatched release for hemorrhage", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).EmergencyReleaseReason);
        Assert.Equal("witness-tech", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).SecondVerifier);
        Assert.Equal("dr-authorizer", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).EmergencyReleaseAuthorizer);
        Assert.Equal(UnitAppearance.Hemolysis.ToString(), Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).IssueAppearance);
        Assert.Equal(UnitAppearance.Hemolysis.ToString(), Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).WardAppearance);
        Assert.Equal("PTAG-HIST", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).PtagCode);
        Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency.ToString(), Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).IssueCrossmatchStatus);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).CoolerId);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).IssueType);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).EmergencyReleaseReason);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).SecondVerifier);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).EmergencyReleaseAuthorizer);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).IssueAppearance);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).WardAppearance);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).PtagCode);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).IssueCrossmatchStatus);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).IssuedTo);
        Assert.Equal("ACC-HIST", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).AccessionNumber);
        Assert.Equal(unit1.ExpiresUtc, Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).UnitExpiresUtc);
        Assert.Equal("tech", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Allocated).PerformedBy);

        c.Returns.Add(new Return
        {
            IssueId = issued.Id,
            BloodProductId = unit1.Id,
            ReturnedUtc = DateTime.UtcNow,
            ReturnedBy = "tech-return",
            Reason = "Not transfused",
            ReissueEligible = false
        });
        await c.SaveChangesAsync();
        h1 = (await History(c).ListByPatientAsync(p1.Id)).ToList();
        var returned = Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Returned);
        Assert.Equal("tech-return", returned.PerformedBy);
        Assert.Equal("Not transfused", returned.ReturnReason);
        Assert.False(returned.ReissueEligible);
        Assert.Equal("RN Alvarez", returned.IssuedTo);
        Assert.Equal("CLR-7", returned.CoolerId);
        Assert.Equal(IssueType.EmergencyRelease.ToString(), returned.IssueType);
        Assert.Equal("Uncrossmatched release for hemorrhage", returned.EmergencyReleaseReason);
        Assert.Equal("witness-tech", returned.SecondVerifier);
        Assert.Equal("dr-authorizer", returned.EmergencyReleaseAuthorizer);
        Assert.Equal(UnitAppearance.Hemolysis.ToString(), returned.IssueAppearance);
        Assert.Equal(UnitAppearance.Hemolysis.ToString(), returned.WardAppearance);
        Assert.Equal("PTAG-HIST", returned.PtagCode);
        Assert.Equal(CrossmatchClinicalStatus.NotCrossmatchedEmergency.ToString(), returned.IssueCrossmatchStatus);
        issued.SecondVerifier = " ";
        releaseOverride.AuthorizedBy = " ";
        issued.IssueAppearance = UnitAppearance.Acceptable;
        issued.WardReceivedUtc = null;
        issued.PtagCode = " ";
        await c.SaveChangesAsync();
        h1 = (await History(c).ListByPatientAsync(p1.Id)).ToList();
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).SecondVerifier);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Returned).SecondVerifier);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).EmergencyReleaseAuthorizer);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Returned).EmergencyReleaseAuthorizer);
        Assert.Equal(UnitAppearance.Acceptable.ToString(), Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).IssueAppearance);
        Assert.Equal(UnitAppearance.Acceptable.ToString(), Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Returned).IssueAppearance);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).WardAppearance);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Returned).WardAppearance);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).PtagCode);
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Returned).PtagCode);
        issued.SecondVerifier = "witness-tech";
        releaseOverride.AuthorizedBy = "dr-authorizer";
        issued.IssueAppearance = UnitAppearance.Hemolysis;
        issued.WardReceivedUtc = DateTime.UtcNow;
        issued.PtagCode = "PTAG-HIST";
        await c.SaveChangesAsync();

        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = unit1.Id,
            PatientId = p1.Id,
            SpecimenId = 0,
            Result = CrossmatchResult.Compatible,
            PerformedUtc = DateTime.UtcNow.AddMinutes(-5),
            PerformedBy = "tech"
        });
        await c.SaveChangesAsync();
        h1 = (await History(c).ListByPatientAsync(p1.Id)).ToList();
        Assert.Equal("Compatible", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).CompatibilityStatus);
        Assert.Equal("Compatible", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Crossmatched).CompatibilityStatus);

        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = unit1.Id,
            PatientId = p1.Id,
            SpecimenId = 0,
            Result = CrossmatchResult.Incompatible,
            PerformedUtc = DateTime.UtcNow.AddMinutes(1),
            PerformedBy = "tech"
        });
        await c.SaveChangesAsync();
        h1 = (await History(c).ListByPatientAsync(p1.Id)).ToList();
        Assert.Equal("Incompatible", Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).CompatibilityStatus);

        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = unit1.Id,
            PatientId = p1.Id,
            SpecimenId = 0,
            Result = CrossmatchResult.NotPerformed,
            PerformedUtc = DateTime.UtcNow.AddMinutes(2),
            PerformedBy = "tech"
        });
        await c.SaveChangesAsync();
        h1 = (await History(c).ListByPatientAsync(p1.Id)).ToList();
        Assert.Null(Assert.Single(h1, h => h.EventType == PatientProductHistoryEventType.Issued).CompatibilityStatus);
        Assert.Contains(h1, h => h.EventType == PatientProductHistoryEventType.Crossmatched && h.CompatibilityStatus == "Compatible");
        Assert.Contains(h1, h => h.EventType == PatientProductHistoryEventType.Crossmatched && h.CompatibilityStatus is null);
    }

    [Fact]
    public async Task UpdateOrder_ChangesEditableFields()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-EDIT", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        Assert.True(enc.Succeeded);

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-EDIT-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(order.Succeeded);

        var updated = await Orders(c).UpdateAsync(patient.Id, order.Value!.Id, new UpdateOrderRequest(
            enc.Value.Id, loc.Id,
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null), new OrderLineInputDto(OrderCategory.Test, "ABSC", null)],
            OrderPriority.Stat, null));
        Assert.True(updated.Succeeded);
        Assert.Equal(OrderPriority.Stat, updated.Value!.Priority);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Equal(2, list[0].Lines.Count);
    }

    [Fact]
    public async Task UpdateOrder_WhenCancelled_Fails()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-CANCEL", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-CANCEL-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Test, "ABORH", null)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        await Orders(c).CancelAsync(patient.Id, order.Value!.Id, new CancelOrderRequest("No longer needed"));

        var updated = await Orders(c).UpdateAsync(patient.Id, order.Value.Id, new UpdateOrderRequest(
            enc.Value.Id, loc.Id,
            [new OrderLineInputDto(OrderCategory.Test, "ABSC", null)],
            OrderPriority.Routine, null));
        Assert.False(updated.Succeeded);
        Assert.Contains("cannot be edited", updated.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductOrder_RequiringCrossmatch_DoesNotAddCrossmatchLineUntilScreenComplete()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-XM", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        var rbc = new ProductType
        {
            ProductCode = "RBC-XM",
            Name = "RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        var ffp = new ProductType
        {
            ProductCode = "FFP-NOXM",
            Name = "FFP",
            ComponentClass = ComponentClass.Plasma,
            RequiresCrossmatch = false
        };
        c.ProductTypes.AddRange(rbc, ffp);
        await c.SaveChangesAsync();

        var productOrder = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-RBC-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Product, null, rbc.Id)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(productOrder.Succeeded);

        var list = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Single(list);
        Assert.Single(list[0].Lines);
        Assert.DoesNotContain(list[0].Lines, l => l.TestCode == "XM");

        var ffpOrder = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value.Id, loc.Id, $"ORD-FFP-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Product, null, ffp.Id)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(ffpOrder.Succeeded);

        list = await Orders(c).ListByPatientAsync(patient.Id);
        Assert.Equal(2, list.Count);
        Assert.Single(list.First(o => o.OrderNumber.StartsWith("ORD-FFP", StringComparison.Ordinal)).Lines);
    }

    [Fact]
    public async Task OrderCreate_ProducesAuditEvent()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-AUDIT", EncounterType.Outpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        var rbc = new ProductType { ProductCode = "RBC-A", Name = "RBC", ComponentClass = ComponentClass.RedBloodCells };
        c.ProductTypes.Add(rbc);
        await c.SaveChangesAsync();
        var beforeOrders = await c.AuditEvents.CountAsync(e => e.EntityType == nameof(Order));

        var order = await Orders(c).CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value!.Id, loc.Id, $"ORD-AUDIT-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Product, null, rbc.Id)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(order.Succeeded);

        var afterOrders = await c.AuditEvents.CountAsync(e => e.EntityType == nameof(Order));
        Assert.True(afterOrders > beforeOrders);
    }

    [Fact]
    public async Task ProductOrder_AutolinksCompletedTypeAndScreen_AndRejectsIncompleteSpecimen()
    {
        await using var c = _factory.Create();
        var (patient, loc) = await SeedPatientAsync(c);
        var enc = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            "VIS-TS", EncounterType.Inpatient, EncounterStatus.Active, DateTime.UtcNow, null,
            null, null, null, null, null, null, null, null));
        var rbc = new ProductType { ProductCode = "RBC-TS", Name = "RBC", ComponentClass = ComponentClass.RedBloodCells };
        c.ProductTypes.Add(rbc);
        c.TestDefinitions.AddRange(
            new TestDefinition { Code = "ABORH", Name = "ABO/Rh", Category = TestCategory.AboRh, ResultValueType = ResultValueType.AboRh, IsActive = true, IsDraft = false, EffectiveUtc = DateTime.UtcNow, Version = 1 },
            new TestDefinition { Code = "ABSC", Name = "Antibody Screen", Category = TestCategory.AntibodyScreen, ResultValueType = ResultValueType.Coded, IsActive = true, IsDraft = false, EffectiveUtc = DateTime.UtcNow, Version = 1 });
        var incomplete = new Specimen
        {
            PatientId = patient.Id,
            AccessionNumber = "ACC-INCOMPLETE",
            SpecimenType = "EDTA",
            CollectedUtc = DateTime.UtcNow.AddHours(-3),
            Status = SpecimenStatus.Accepted,
            EncounterId = enc.Value!.Id
        };
        var complete = new Specimen
        {
            PatientId = patient.Id,
            AccessionNumber = "ACC-COMPLETE",
            SpecimenType = "EDTA",
            CollectedUtc = DateTime.UtcNow.AddHours(-1),
            Status = SpecimenStatus.Accepted,
            EncounterId = enc.Value.Id
        };
        c.Specimens.AddRange(incomplete, complete);
        await c.SaveChangesAsync();
        c.TestResults.AddRange(
            new TestResult { SpecimenId = complete.Id, PatientId = patient.Id, TestCode = "ABORH", Value = "O Pos", Status = ResultStatus.Verified, Source = ResultSource.Manual },
            new TestResult { SpecimenId = complete.Id, PatientId = patient.Id, TestCode = "ABSC", Value = "Negative", Status = ResultStatus.Verified, Source = ResultSource.Manual });
        await c.SaveChangesAsync();

        var antibody = new AntibodyScreenCompatLoader(
            new EfRepository<TestResult>(c),
            new EfRepository<TestDefinition>(c),
            new EfRepository<AntibodyHistory>(c),
            specimens: new EfRepository<Specimen>(c));
        var orders = new OrderService(
            new EfRepository<Order>(c), new EfRepository<OrderLine>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Encounter>(c), new EfRepository<OrderingLocation>(c), new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c), new EfRepository<OrderingProvider>(c), new EfRepository<ProductType>(c),
            new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c), new FixedClock(DateTime.UtcNow), c,
            antibodyScreen: antibody);

        var created = await orders.CreateAsync(patient.Id, new CreateOrderRequest(
            enc.Value.Id, loc.Id, $"ORD-TS-{Guid.NewGuid():N}",
            [new OrderLineInputDto(OrderCategory.Product, null, rbc.Id)],
            OrderPriority.Routine, DateTime.UtcNow, null, OrderSource.Manual, null, null));
        Assert.True(created.Succeeded, created.Error);
        var listed = await orders.ListByPatientAsync(patient.Id);
        Assert.Equal(complete.Id, listed[0].SpecimenId);

        var link = await orders.LinkSpecimenAsync(patient.Id, created.Value!.Id, new LinkOrderSpecimenRequest(incomplete.Id));
        Assert.False(link.Succeeded);
        Assert.Contains("type and screen", link.Error!, StringComparison.OrdinalIgnoreCase);
    }
}
