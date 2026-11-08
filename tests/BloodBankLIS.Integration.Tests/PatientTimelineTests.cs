using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-308: read-only patient timeline. Events use each record's own time; changes without a time stay on their event.
public class PatientTimelineTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static PatientTimelineService Timeline(BloodBankDbContext c) =>
        new(new EfRepository<Encounter>(c), new EfRepository<Order>(c), new EfRepository<Specimen>(c),
            new EfRepository<TestResult>(c), new EfRepository<PatientBloodTypeHistory>(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<SpecialTransfusionRequirement>(c), new EfRepository<SpecialRequirementDefinition>(c),
            new EfRepository<Allocation>(c), new EfRepository<Crossmatch>(c), new EfRepository<Issue>(c),
            new EfRepository<Return>(c), new EfRepository<TransfusionEvent>(c), new EfRepository<ReactionInvestigation>(c),
            new EfRepository<BloodUnit>(c), new EfRepository<Override>(c));

    private DateTime T(int hours) => _factory.Clock.UtcNow.AddHours(-100 + hours);

    [Fact]
    public async Task NewPatient_HasAnEmptyTimeline()
    {
        await using var c = _factory.Create();
        var patient = await AddPatientAsync(c, "MRN-TL0");

        Assert.Empty(await Timeline(c).GetAsync(patient.Id));
    }

    [Fact]
    public async Task FullCourse_IsNewestFirst_WithEachRecordsOwnTime()
    {
        await using var c = _factory.Create();
        var f = await SeedCourseAsync(c);

        var events = await Timeline(c).GetAsync(f.Patient.Id);

        Assert.Equal(events.OrderByDescending(e => e.AtUtc).Select(e => e.SourceKey), events.Select(e => e.SourceKey));
        Assert.Equal(T(0), Event(events, $"enc-{f.Encounter.Id}-admit").AtUtc);
        Assert.Equal(T(1), Event(events, $"ord-{f.Order.Id}").AtUtc);
        Assert.Equal(T(2), Event(events, $"spc-{f.Specimen.Id}-collect").AtUtc);
        Assert.Equal(T(3), Event(events, $"spc-{f.Specimen.Id}-receive").AtUtc);
        Assert.Equal(T(10), Event(events, $"iss-{f.Issue.Id}").AtUtc);
        Assert.Equal(T(11), Event(events, $"trf-{f.Transfusion.Id}").AtUtc);
        Assert.Equal(T(12), Event(events, $"rxn-{f.Reaction.Id}").AtUtc);
    }

    [Fact]
    public async Task SafetyEvents_AreCritical_AndChangedRecords_NeedAttention()
    {
        await using var c = _factory.Create();
        var f = await SeedCourseAsync(c);

        var events = await Timeline(c).GetAsync(f.Patient.Id);

        Assert.Equal(TimelineSeverity.Critical, Event(events, $"abs-{f.Antibody.Id}").Severity);
        Assert.Contains("anti-K", Event(events, $"abs-{f.Antibody.Id}").Title);
        Assert.Equal(TimelineSeverity.Critical, Event(events, $"xm-{f.Crossmatch.Id}").Severity);
        Assert.Equal(TimelineSeverity.Critical, Event(events, $"trf-{f.Transfusion.Id}").Severity);
        Assert.Equal(TimelineSeverity.Critical, Event(events, $"rxn-{f.Reaction.Id}").Severity);

        var issue = Event(events, $"iss-{f.Issue.Id}");
        Assert.Equal(TimelineSeverity.Warning, issue.Severity);
        Assert.Contains("EmergencyRelease", issue.Title);
        Assert.Contains("Override ISS-XM-REQUIRED: Uncrossmatched release for hemorrhage", issue.Detail);
        Assert.Contains("authorized by Dr. Lee", issue.Detail);

        var requirement = Event(events, $"req-{f.Requirement.Id}");
        Assert.Equal(TimelineSeverity.Warning, requirement.Severity);
        Assert.Contains("Antigen-negative (K)", requirement.Title);
    }

    [Fact]
    public async Task CorrectedAndInvalidatedResults_KeepTheOriginal_AndShowTheReason()
    {
        await using var c = _factory.Create();
        var f = await SeedCourseAsync(c);

        var events = await Timeline(c).GetAsync(f.Patient.Id);

        var original = Event(events, $"res-{f.Original.Id}-enter");
        Assert.Contains("resulted: A POS", original.Title);
        Assert.Contains("Later replaced by a correction", original.Detail);
        Assert.Equal(T(5), Event(events, $"res-{f.Original.Id}-verify").AtUtc);

        var correction = Event(events, $"res-{f.Correction.Id}-enter");
        Assert.Equal(TimelineSeverity.Warning, correction.Severity);
        Assert.Contains("corrected: O POS", correction.Title);
        Assert.Contains("Reason: Transcription error", correction.Detail);

        var invalidated = Event(events, $"res-{f.Invalidated.Id}-invalidate");
        Assert.Equal(T(8), invalidated.AtUtc);
        Assert.Contains("Reason: Wrong specimen", invalidated.Detail);
        Assert.Equal("tech2", invalidated.By);
    }

    [Fact]
    public async Task CancelledOrder_AndRejectedSpecimen_AreNotGivenAnInventedTime()
    {
        await using var c = _factory.Create();
        var f = await SeedCourseAsync(c);
        var order = await c.Orders.FindAsync(f.Order.Id);
        order!.Status = OrderStatus.Cancelled;
        order.CancellationReason = "Duplicate order";
        var specimen = await c.Specimens.FindAsync(f.Specimen.Id);
        specimen!.Status = SpecimenStatus.Rejected;
        specimen.RejectionReason = "Hemolyzed";
        await c.SaveChangesAsync();

        var events = await Timeline(c).GetAsync(f.Patient.Id);

        var o = Event(events, $"ord-{f.Order.Id}");
        Assert.Equal(T(1), o.AtUtc);
        Assert.Equal(TimelineSeverity.Warning, o.Severity);
        Assert.Contains("Cancelled: Duplicate order", o.Detail);
        var s = Event(events, $"spc-{f.Specimen.Id}-collect");
        Assert.Equal(T(2), s.AtUtc);
        Assert.Contains("Rejected: Hemolyzed", s.Detail);
        Assert.DoesNotContain(events, e => e.SourceKey.Contains("cancel") || e.SourceKey.Contains("reject"));
    }

    [Fact]
    public async Task OnlyThisPatientsRecords_AreShown()
    {
        await using var c = _factory.Create();
        var f = await SeedCourseAsync(c);
        var other = await AddPatientAsync(c, "MRN-TL-OTHER");

        Assert.Empty(await Timeline(c).GetAsync(other.Id));
        Assert.All(await Timeline(c).GetAsync(f.Patient.Id), e => Assert.DoesNotContain("MRN-TL-OTHER", e.Title));
    }

    [Fact]
    public async Task ProductEvents_NameTheUnit_AndLinkToIt()
    {
        await using var c = _factory.Create();
        var f = await SeedCourseAsync(c);

        var events = await Timeline(c).GetAsync(f.Patient.Id);

        var ret = Event(events, $"ret-{f.Return.Id}");
        Assert.Contains("W9999TL000001", ret.Title);
        Assert.Equal($"/inventory/units/{f.Unit.Id}", ret.Href);
        Assert.Contains("Reason: Patient left the floor", ret.Detail);
        Assert.Contains(events, e => e.SourceKey == $"alc-{f.Allocation.Id}" && e.Category == TimelineCategory.Product);
    }

    // TEST-BB-422: a crossmatch recorded with only its result shows that result (incompatible is critical), and the
    // ward receipt has its own event.
    [Fact]
    public async Task RecordedCrossmatchResults_AndWardReceipt_AreShown()
    {
        await using var c = _factory.Create();
        var f = await SeedCourseAsync(c);
        var compatible = new Crossmatch
        {
            BloodProductId = f.Unit.Id, PatientId = f.Patient.Id, SpecimenId = f.Specimen.Id, PerformedUtc = T(9).AddMinutes(5),
            PerformedBy = "tech2", Result = CrossmatchResult.Compatible
        };
        var incompatible = new Crossmatch
        {
            BloodProductId = f.Unit.Id, PatientId = f.Patient.Id, SpecimenId = f.Specimen.Id, PerformedUtc = T(9).AddMinutes(6),
            PerformedBy = "tech2", Result = CrossmatchResult.Incompatible
        };
        c.AddRange(compatible, incompatible);
        f.Issue.IssuedToLocation = "4 West";
        f.Issue.WardReceivedUtc = T(10).AddMinutes(10);
        f.Issue.WardReceivedBy = "RN Park";
        await c.SaveChangesAsync();

        var events = await Timeline(c).GetAsync(f.Patient.Id);

        Assert.EndsWith(": Compatible", Event(events, $"xm-{compatible.Id}").Title);
        var bad = Event(events, $"xm-{incompatible.Id}");
        Assert.EndsWith(": Incompatible", bad.Title);
        Assert.Equal(TimelineSeverity.Critical, bad.Severity);

        var ward = Event(events, $"iss-{f.Issue.Id}-ward");
        Assert.Equal("Unit W9999TL000001 received on the ward", ward.Title);
        Assert.Equal(T(10).AddMinutes(10), ward.AtUtc);
        Assert.Equal("RN Park", ward.By);
        Assert.Contains("At 4 West", ward.Detail);
    }

    private static TimelineEventDto Event(IReadOnlyList<TimelineEventDto> events, string key) =>
        Assert.Single(events, e => e.SourceKey == key);

    // TEST-BB-613: an antibody reported by another facility says so on the timeline.
    [Fact]
    public async Task OutsideAntibody_SaysWhereItWasReported()
    {
        await using var c = _factory.Create();
        var patient = await AddPatientAsync(c, "MRN-TL-OUT");
        c.AntibodyHistory.Add(new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-Jka", IsActive = true, ReportingFacility = "St. Mary's Hospital", IdentifiedOn = new DateOnly(2019, 4, 2) });
        await c.SaveChangesAsync();

        var antibody = Assert.Single(await Timeline(c).GetAsync(patient.Id), e => e.Title.StartsWith("Antibody recorded"));
        Assert.Equal("Reported by St. Mary's Hospital, identified 2019-04-02", antibody.Detail);
    }

    private static async Task<Patient> AddPatientAsync(BloodBankDbContext c, string mrn)
    {
        var patient = new Patient { MedicalRecordNumber = mrn, LastName = "Timeline", FirstName = "Tess", DateOfBirth = new DateOnly(1965, 4, 4), Sex = Sex.Female };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        return patient;
    }

    private sealed record Course(
        Patient Patient, Encounter Encounter, Order Order, Specimen Specimen, TestResult Original, TestResult Correction,
        TestResult Invalidated, AntibodyHistory Antibody, SpecialTransfusionRequirement Requirement, BloodUnit Unit,
        Allocation Allocation, Crossmatch Crossmatch, Issue Issue, Return Return, TransfusionEvent Transfusion,
        ReactionInvestigation Reaction);

    private async Task<Course> SeedCourseAsync(BloodBankDbContext c)
    {
        var patient = await AddPatientAsync(c, "MRN-TL1");
        var location = new OrderingLocation { Code = "TL-WARD", Name = "Ward 9", IsActive = true };
        var product = new ProductType { ProductCode = "RBC-TL", Name = "Red cells", ComponentClass = ComponentClass.RedBloodCells };
        c.AddRange(location, product);
        var encounter = new Encounter
        {
            PatientId = patient.Id, VisitNumber = "V-TL", EncounterType = EncounterType.Inpatient, Status = EncounterStatus.Active, AdmitUtc = T(0)
        };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();

        var order = new Order
        {
            OrderNumber = "ORD-TL1", PatientId = patient.Id, EncounterId = encounter.Id, OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Test, OrderName = "Type and screen", OrderType = OrderType.TypeAndScreen, OrderedUtc = T(1)
        };
        var specimen = new Specimen
        {
            AccessionNumber = "ACC-TL1", PatientId = patient.Id, EncounterId = encounter.Id, SpecimenType = "EDTA",
            CollectedUtc = T(2), ReceivedUtc = T(3), Status = SpecimenStatus.Accepted
        };
        var unit = new BloodUnit
        {
            UnitNumber = "W9999TL000001", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive,
            Status = UnitStatus.Transfused, ExpiresUtc = T(200)
        };
        c.AddRange(order, specimen, unit);
        await c.SaveChangesAsync();

        var correction = new TestResult
        {
            SpecimenId = specimen.Id, PatientId = patient.Id, TestCode = "ABORH", Value = "O POS", Status = ResultStatus.Verified,
            EnteredUtc = T(6), EnteredBy = "tech1", CorrectionReason = "Transcription error"
        };
        c.TestResults.Add(correction);
        await c.SaveChangesAsync();
        var original = new TestResult
        {
            SpecimenId = specimen.Id, PatientId = patient.Id, TestCode = "ABORH", Value = "A POS", Status = ResultStatus.Corrected,
            EnteredUtc = T(4), EnteredBy = "tech1", VerifiedUtc = T(5), VerifiedBy = "tech2", SupersededByResultId = correction.Id
        };
        var invalidated = new TestResult
        {
            SpecimenId = specimen.Id, PatientId = patient.Id, TestCode = "ABSC", Value = "NEG", Status = ResultStatus.Invalidated,
            EnteredUtc = T(7), EnteredBy = "tech1", InvalidatedUtc = T(8), InvalidatedBy = "tech2", InvalidationReason = "Wrong specimen"
        };
        var antibody = new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-K", Status = AntibodyStatus.Identified, IsActive = true };
        var requirement = new SpecialTransfusionRequirement
        {
            PatientId = patient.Id, RequirementType = SpecialTransfusionRequirementType.AntigenNegative, AntigenCode = "K",
            Reason = "Anti-K", EffectiveUtc = T(9), IsActive = true, EnteredBy = "tech2"
        };
        c.AddRange(original, invalidated, antibody, requirement);
        await c.SaveChangesAsync();

        var allocation = new Allocation
        {
            BloodProductId = unit.Id, PatientId = patient.Id, SpecimenId = specimen.Id, Status = AllocationStatus.Consumed,
            AllocatedUtc = T(9), AllocatedBy = "tech2"
        };
        var crossmatch = new Crossmatch
        {
            BloodProductId = unit.Id, PatientId = patient.Id, SpecimenId = specimen.Id, PerformedUtc = T(9), PerformedBy = "tech2",
            ClinicalStatus = CrossmatchClinicalStatus.Incompatible
        };
        var issue = new Issue
        {
            BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = T(10), IssuedBy = "tech2", IssueType = IssueType.EmergencyRelease,
            Status = IssueStatus.Transfused, CrossmatchStatus = CrossmatchClinicalStatus.NotCrossmatchedEmergency, 
        };
        var releaseOverride = new Override
        {
            Action = OverrideAction.EmergencyRelease, ContextType = nameof(Issue), ContextId = unit.Id, RuleCode = "ISS-XM-REQUIRED",
            Reason = "Uncrossmatched release for hemorrhage", AuthorizedBy = "Dr. Lee", OverriddenUtc = T(10)
        };
        c.Add(releaseOverride);
        await c.SaveChangesAsync();
        issue.OverrideId = releaseOverride.Id;
        c.AddRange(allocation, crossmatch, issue);
        await c.SaveChangesAsync();

        var ret = new Return { IssueId = issue.Id, BloodProductId = unit.Id, ReturnedUtc = T(10).AddMinutes(30), ReturnedBy = "rn1", Reason = "Patient left the floor" };
        var transfusion = new TransfusionEvent
        {
            IssueId = issue.Id, BloodProductId = unit.Id, PatientId = patient.Id, StartUtc = T(11), StopUtc = T(11).AddHours(2),
            DocumentedBy = "rn1", ReactionSuspected = true, VolumeTransfused = 300
        };
        c.AddRange(ret, transfusion);
        await c.SaveChangesAsync();

        var reaction = new ReactionInvestigation
        {
            TransfusionEventId = transfusion.Id, PatientId = patient.Id, BloodProductId = unit.Id, ReportedUtc = T(12), ReportedBy = "rn1"
        };
        c.Add(reaction);
        await c.SaveChangesAsync();

        return new Course(patient, encounter, order, specimen, original, correction, invalidated, antibody, requirement, unit,
            allocation, crossmatch, issue, ret, transfusion, reaction);
    }
}
