using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-309: specimen timeline from the specimen record, its audit rows, results, orders, crossmatches, and reservations.
public class SpecimenTimelineTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private SpecimenTimelineService Timeline(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<TestResult>(c), new EfRepository<OrderSpecimen>(c),
            new EfRepository<Order>(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<BloodUnit>(c), new EfRepository<Override>(c), new AuditQuery(c), _factory.Clock);

    private SpecimenService Specimens(BloodBankDbContext c) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c),
            c, _factory.Clock,
            audit: new AuditWriter(c, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", isDevMode: false)),
            currentUser: _factory.CurrentUser);

    [Fact]
    public async Task UnknownSpecimen_ReturnsNull()
    {
        await using var c = _factory.Create();

        Assert.Null(await Timeline(c).GetAsync(987654));
    }

    [Fact]
    public async Task ReceiveAndRelabel_ComeFromTheAuditTrail_WithUserWorkstationAndChange()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c, SpecimenStatus.Collected);
        var start = _factory.Clock.UtcNow;

        _factory.Clock.UtcNow = start.AddMinutes(20);
        Assert.True((await Specimens(c).ReceiveAsync(f.Specimen.Id)).Succeeded);
        _factory.Clock.UtcNow = start.AddMinutes(40);
        Assert.True((await Specimens(c).RelabelAsync(f.Specimen.Id, new RelabelSpecimenRequest("BC-NEW-2", "Label smudged"))).Succeeded);

        var timeline = await Timeline(c).GetAsync(f.Specimen.Id);

        Assert.NotNull(timeline);
        var received = Assert.Single(timeline.Events, e => e.Title.StartsWith("Specimen received", StringComparison.Ordinal));
        Assert.Equal(start.AddMinutes(20), received.AtUtc);
        Assert.Equal("tech-test at WORKSTATION-1", received.By);
        Assert.DoesNotContain(timeline.Events, e => e.SourceKey.EndsWith("-receive", StringComparison.Ordinal));

        var relabel = Assert.Single(timeline.Events, e => e.Title.StartsWith("Specimen relabeled", StringComparison.Ordinal));
        Assert.Equal(TimelineSeverity.Warning, relabel.Severity);
        Assert.Contains("Label smudged", relabel.Title);
        Assert.Equal("Barcode: BC-OLD-1 → BC-NEW-2", relabel.Detail);
        // The scheduled expiry is in the future, so it leads; the relabel is the most recent thing that happened.
        Assert.Equal("Specimen expires", timeline.Events[0].Title);
        Assert.Equal(relabel, timeline.Events[1]);
        Assert.DoesNotContain(timeline.Events, e => e.Detail?.Contains("ModifiedUtc", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Rejection_NeedsAttention_AndShowsTheReason()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c, SpecimenStatus.Collected);

        Assert.True((await Specimens(c).RejectAsync(f.Specimen.Id, "Hemolyzed")).Succeeded);

        var timeline = await Timeline(c).GetAsync(f.Specimen.Id);

        Assert.Equal(SpecimenStatus.Rejected, timeline!.Status);
        var reject = Assert.Single(timeline.Events, e => e.Detail?.Contains("RejectionReason: Hemolyzed", StringComparison.Ordinal) == true);
        Assert.Equal(TimelineSeverity.Warning, reject.Severity);
    }

    [Fact]
    public async Task WithoutAuditRows_TheRecordedReceiptTime_IsStillShown()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c, SpecimenStatus.Accepted, receivedUtc: _factory.Clock.UtcNow.AddHours(-1));

        var timeline = await Timeline(c).GetAsync(f.Specimen.Id);

        var received = Assert.Single(timeline!.Events, e => e.SourceKey == $"spc-{f.Specimen.Id}-receive");
        Assert.Equal(_factory.Clock.UtcNow.AddHours(-1), received.AtUtc);
        Assert.Contains(timeline.Events, e => e.SourceKey == $"spc-{f.Specimen.Id}-collect" && e.By == "Nurse Jo");
    }

    [Fact]
    public async Task Expiry_IsScheduledUntilReached_ThenNeedsAttention()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c, SpecimenStatus.Accepted);

        var before = await Timeline(c).GetAsync(f.Specimen.Id);
        var scheduled = Assert.Single(before!.Events, e => e.SourceKey.EndsWith("-expiry", StringComparison.Ordinal));
        Assert.Equal("Specimen expires", scheduled.Title);
        Assert.Equal(TimelineSeverity.Info, scheduled.Severity);

        _factory.Clock.UtcNow = f.Specimen.ExpiresUtc!.Value.AddMinutes(1);
        var after = await Timeline(c).GetAsync(f.Specimen.Id);
        var expired = Assert.Single(after!.Events, e => e.SourceKey.EndsWith("-expiry", StringComparison.Ordinal));
        Assert.Equal("Specimen expired", expired.Title);
        Assert.Equal(TimelineSeverity.Warning, expired.Severity);
    }

    [Fact]
    public async Task OrdersResultsCrossmatchesAndReservations_OnThisTubeOnly()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c, SpecimenStatus.Accepted);
        var other = new Specimen
        {
            AccessionNumber = "ACC-ST-OTHER", PatientId = f.Specimen.PatientId, SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-3), Status = SpecimenStatus.Accepted
        };
        var product = new ProductType { ProductCode = "RBC-ST", Name = "Red cells", ComponentClass = ComponentClass.RedBloodCells };
        c.AddRange(other, product);
        await c.SaveChangesAsync();
        var unit = new BloodUnit { UnitNumber = "W9999ST000001", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Negative, Status = UnitStatus.Assigned, ExpiresUtc = _factory.Clock.UtcNow.AddDays(9) };
        c.Add(unit);
        await c.SaveChangesAsync();
        c.OrderSpecimens.Add(new OrderSpecimen { OrderId = f.Order.Id, SpecimenId = f.Specimen.Id });
        c.TestResults.AddRange(
            new TestResult { SpecimenId = f.Specimen.Id, PatientId = f.Specimen.PatientId, TestCode = "ABSC", Value = "POS", Status = ResultStatus.Verified, EnteredUtc = _factory.Clock.UtcNow.AddMinutes(-30), EnteredBy = "tech1" },
            new TestResult { SpecimenId = other.Id, PatientId = f.Specimen.PatientId, TestCode = "DAT", Value = "NEG", Status = ResultStatus.Verified, EnteredUtc = _factory.Clock.UtcNow.AddMinutes(-20), EnteredBy = "tech1" });
        c.Crossmatches.Add(new Crossmatch { BloodProductId = unit.Id, PatientId = f.Specimen.PatientId, SpecimenId = f.Specimen.Id, PerformedUtc = _factory.Clock.UtcNow.AddMinutes(-10), PerformedBy = "tech1", ClinicalStatus = CrossmatchClinicalStatus.Incompatible });
        c.Allocations.Add(new Allocation { BloodProductId = unit.Id, PatientId = f.Specimen.PatientId, SpecimenId = f.Specimen.Id, Status = AllocationStatus.Reserved, AllocatedUtc = _factory.Clock.UtcNow.AddMinutes(-5), AllocatedBy = "tech1" });
        await c.SaveChangesAsync();

        var timeline = await Timeline(c).GetAsync(f.Specimen.Id);

        Assert.Contains(timeline!.Events, e => e.Title == "Serves order ORD-ST1: Type and screen" && e.Href!.EndsWith("?tab=orders", StringComparison.Ordinal));
        Assert.Contains(timeline.Events, e => e.Title == "ABSC resulted: POS");
        Assert.DoesNotContain(timeline.Events, e => e.Title.StartsWith("DAT", StringComparison.Ordinal));
        var xm = Assert.Single(timeline.Events, e => e.SourceKey.StartsWith("xm-", StringComparison.Ordinal));
        Assert.Equal(TimelineSeverity.Critical, xm.Severity);
        Assert.Equal("Crossmatch W9999ST000001: Incompatible", xm.Title);
        Assert.Contains(timeline.Events, e => e.Title == "Unit W9999ST000001 reserved on this specimen");
    }

    [Fact]
    public async Task ExpiryOverride_ShowsRuleReasonAndAuthorizer()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c, SpecimenStatus.Accepted);
        c.Overrides.Add(new Override
        {
            Action = OverrideAction.WarningOverride, ContextType = nameof(Specimen), ContextId = f.Specimen.Id, RuleCode = "SPC-EXP-OVERRIDE",
            Reason = "Not transfused or pregnant in 3 months", AuthorizedBy = "Dr. Kim", OverriddenUtc = _factory.Clock.UtcNow
        });
        await c.SaveChangesAsync();

        var timeline = await Timeline(c).GetAsync(f.Specimen.Id);

        var o = Assert.Single(timeline!.Events, e => e.SourceKey.StartsWith("ovr-", StringComparison.Ordinal));
        Assert.Equal(TimelineSeverity.Warning, o.Severity);
        Assert.Equal("Override SPC-EXP-OVERRIDE", o.Title);
        Assert.Equal("Reason: Not transfused or pregnant in 3 months · authorized by Dr. Kim", o.Detail);
    }

    [Theory]
    [InlineData(null, "{\"Barcode\":\"B\"}", "Barcode: B")]
    [InlineData("{\"Barcode\":\"A\"}", "{\"Barcode\":\"B\"}", "Barcode: A → B")]
    [InlineData("{\"Barcode\":null}", "{\"Barcode\":\"B\"}", "Barcode: blank → B")]
    [InlineData("{\"Barcode\":\"A\"}", "{\"Barcode\":\"A\"}", null)]
    [InlineData("not json", "also not json", null)]
    [InlineData(null, null, null)]
    [InlineData(null, "{\"A\":1,\"B\":2,\"C\":3,\"D\":4,\"E\":5,\"F\":6}", "A: 1 · B: 2 · C: 3 · D: 4 · and 2 more (see the audit trail)")]
    public void Changes_DescribesOnlyWhatChanged(string? oldJson, string? newJson, string? expected) =>
        Assert.Equal(expected, SpecimenTimelineService.Changes(oldJson, newJson));

    private sealed record Fixture(Specimen Specimen, Order Order);

    private async Task<Fixture> SeedAsync(BloodBankDbContext c, SpecimenStatus status, DateTime? receivedUtc = null)
    {
        var patient = new Patient { MedicalRecordNumber = "MRN-ST1", LastName = "Tube", FirstName = "Tia", DateOfBirth = new DateOnly(1975, 7, 7), Sex = Sex.Female };
        var location = new OrderingLocation { Code = "ST-WARD", Name = "Ward 3", IsActive = true };
        c.AddRange(patient, location);
        await c.SaveChangesAsync();
        var encounter = new Encounter { PatientId = patient.Id, VisitNumber = "V-ST", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = _factory.Clock.UtcNow.AddDays(-1) };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();
        var specimen = new Specimen
        {
            AccessionNumber = "ACC-ST1", PatientId = patient.Id, EncounterId = encounter.Id, SpecimenType = "EDTA", Barcode = "BC-OLD-1",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2), ReceivedUtc = receivedUtc, ExpiresUtc = _factory.Clock.UtcNow.AddDays(3),
            Status = status, Collector = "Nurse Jo"
        };
        var order = new Order
        {
            OrderNumber = "ORD-ST1", PatientId = patient.Id, EncounterId = encounter.Id, OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Test, OrderName = "Type and screen", OrderType = OrderType.TypeAndScreen, OrderedUtc = _factory.Clock.UtcNow.AddHours(-3)
        };
        c.AddRange(specimen, order);
        await c.SaveChangesAsync();
        return new Fixture(specimen, order);
    }
}
