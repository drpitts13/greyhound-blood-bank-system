using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class DowntimePatientSummaryTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public DowntimePatientSummaryTests(SqliteContextFactory factory) => _factory = factory;

    private DowntimePatientSummaryService Service(BloodBankDbContext c) =>
        new(
            new EfRepository<Patient>(c),
            new EfRepository<Specimen>(c),
            new EfRepository<Allocation>(c),
            new EfRepository<Issue>(c),
            new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new EfRepository<AntibodyHistory>(c),
            new EfRepository<SpecialTransfusionRequirement>(c),
            new EfRepository<SpecialRequirementDefinition>(c),
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, new StaticEnvironmentInfo("Development", isDevMode: false)),
            c,
            _factory.Clock,
            new EfRepository<RhigEvaluation>(c));

    private Patient NewPatient(string key, string last) => new()
    {
        MedicalRecordNumber = $"MRN-DTP-{last}-{key}",
        LastName = $"{last}{key}",
        FirstName = "Downtime",
        DateOfBirth = new DateOnly(1975, 6, 1)
    };

    private Specimen NewSpecimen(long patientId, string accession, DateTime? expires, SpecimenStatus status = SpecimenStatus.Accepted) => new()
    {
        AccessionNumber = accession,
        PatientId = patientId,
        SpecimenType = "EDTA",
        CollectedUtc = _factory.Clock.UtcNow.AddHours(-6),
        ExpiresUtc = expires,
        Status = status
    };

    // TEST-BB-604: an antibody known only from another facility's report says so on the antibody card; the same antibody
    // also found here reads plain.
    [Fact]
    public async Task AntibodyCard_MarksOutsideReports()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var patient = NewPatient(key, "Outside");
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        c.AntibodyHistory.AddRange(
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-Jka", IsActive = true, ReportingFacility = "St. Mary's Hospital" },
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-K", IsActive = true, ReportingFacility = "St. Mary's Hospital" },
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-K", IsActive = true });
        await c.SaveChangesAsync();

        var card = await Service(c).GetAntibodyCardAsync(patient.Id);
        Assert.Equal(["anti-Jka (reported by St. Mary's Hospital)", "anti-K"], card!.Antibodies);
    }

    // TEST-BB-478: the antibody card carries the patient's type and active antibodies (not inactive ones), its print is
    // audited against the patient; a patient with nothing to carry gets no card, and an unknown patient none either.
    [Fact]
    public async Task AntibodyCard_CarriesTypeAndActiveAntibodies_AndPrintIsAudited()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var withAntibody = NewPatient(key, "Card");
        var nothing = NewPatient(key, "Plain");
        c.Patients.AddRange(withAntibody, nothing);
        await c.SaveChangesAsync();
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory { PatientId = withAntibody.Id, Abo = AboGroup.O, RhD = RhType.Negative, IsCurrent = true });
        c.AntibodyHistory.AddRange(
            new AntibodyHistory { PatientId = withAntibody.Id, AntibodySpecificity = "anti-Fya", IsActive = true },
            new AntibodyHistory { PatientId = withAntibody.Id, AntibodySpecificity = "anti-E", IsActive = false });
        await c.SaveChangesAsync();
        var service = Service(c);

        var card = await service.GetAntibodyCardAsync(withAntibody.Id);
        Assert.NotNull(card);
        Assert.Equal(new Domain.ValueObjects.AboRh(AboGroup.O, RhType.Negative).ToString(), card!.AboRh);
        Assert.Equal(new[] { "anti-Fya" }, card.Antibodies);

        var printed = await service.RecordAntibodyCardPrintedAsync(withAntibody.Id);
        Assert.True(printed.Succeeded, printed.Error);
        var audit = await c.AuditEvents.SingleAsync(a => a.EntityType == nameof(Patient) && a.EntityId == withAntibody.Id && a.Reason == "Patient antibody card printed.");
        Assert.Equal(AuditEventType.Print, audit.EventType);
        Assert.Contains("anti-Fya", audit.NewValueJson);

        var empty = await service.RecordAntibodyCardPrintedAsync(nothing.Id);
        Assert.False(empty.Succeeded);
        Assert.Contains("nothing to put on a card", empty.Error);
        Assert.False(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(Patient) && a.EntityId == nothing.Id && a.Reason == "Patient antibody card printed."));
        Assert.False((await service.RecordAntibodyCardPrintedAsync(long.MaxValue)).Succeeded);
    }

    [Fact]
    public async Task Summary_ListsPatientsBeingWorkedFor_WithTypeAntibodiesRequirementsSpecimensAndUnits()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var now = _factory.Clock.UtcNow;

        var withSpecimen = NewPatient(key, "Specimen");
        var withUnits = NewPatient(key, "Units");
        var expiredOnly = NewPatient(key, "Expired");
        var rejectedOnly = NewPatient(key, "Rejected");
        var transfusedOnly = NewPatient(key, "Transfused");
        c.Patients.AddRange(withSpecimen, withUnits, expiredOnly, rejectedOnly, transfusedOnly);
        var product = new ProductType { ProductCode = $"RBC-DTP-{key}", Name = "Downtime RBC", ComponentClass = ComponentClass.RedBloodCells };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();

        c.Specimens.AddRange(
            NewSpecimen(withSpecimen.Id, $"ACC-DTP-A-{key}", now.AddDays(2)),
            NewSpecimen(expiredOnly.Id, $"ACC-DTP-X-{key}", now.AddHours(-1)),
            NewSpecimen(rejectedOnly.Id, $"ACC-DTP-R-{key}", now.AddDays(2), SpecimenStatus.Rejected));

        // Two concordant determinations on different specimens for one patient; one for the other.
        c.PatientBloodTypeHistory.AddRange(
            new PatientBloodTypeHistory { PatientId = withSpecimen.Id, Abo = AboGroup.A, RhD = RhType.Negative, IsCurrent = false, SpecimenId = 9001 },
            new PatientBloodTypeHistory { PatientId = withSpecimen.Id, Abo = AboGroup.A, RhD = RhType.Negative, IsCurrent = true, SpecimenId = 9002 },
            new PatientBloodTypeHistory { PatientId = withUnits.Id, Abo = AboGroup.O, RhD = RhType.Positive, IsCurrent = true, SpecimenId = 9003 });
        c.AntibodyHistory.AddRange(
            new AntibodyHistory { PatientId = withSpecimen.Id, AntibodySpecificity = "anti-K", IsActive = true },
            new AntibodyHistory { PatientId = withSpecimen.Id, AntibodySpecificity = "anti-E", IsActive = false, DeactivationReason = "Entered in error" });

        var informational = new SpecialRequirementDefinition
        {
            Code = $"INFO-{key}",
            Name = "Note",
            Category = SpecialRequirementCategory.Informational,
            EnforcementKind = SpecialRequirementEnforcementKind.Informational,
            Instruction = "Call the hematology fellow before issue",
            IsActive = true
        };
        c.SpecialRequirementDefinitions.Add(informational);
        await c.SaveChangesAsync();
        c.SpecialTransfusionRequirements.AddRange(
            new SpecialTransfusionRequirement
            {
                PatientId = withSpecimen.Id, RequirementType = SpecialTransfusionRequirementType.Irradiated,
                Reason = "Stem cell transplant", EffectiveUtc = now.AddDays(-10)
            },
            new SpecialTransfusionRequirement
            {
                PatientId = withSpecimen.Id, RequirementType = SpecialTransfusionRequirementType.Irradiated,
                Reason = "Ended", EffectiveUtc = now.AddDays(-30), ExpiresUtc = now.AddDays(-1)
            },
            new SpecialTransfusionRequirement
            {
                PatientId = withSpecimen.Id, RequirementType = SpecialTransfusionRequirementType.Other,
                RequirementDefinitionId = informational.Id, Reason = "Team request", EffectiveUtc = now.AddDays(-1)
            });

        BloodUnit Unit(string suffix, UnitStatus status) => new()
        {
            UnitNumber = $"W-DTP-{suffix}-{key}", ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive,
            ExpiresUtc = now.AddDays(10), Status = status
        };
        var reservedUnit = Unit("RES", UnitStatus.Assigned);
        var issuedUnit = Unit("ISS", UnitStatus.Issued);
        var transfusedUnit = Unit("TRF", UnitStatus.Transfused);
        c.BloodUnits.AddRange(reservedUnit, issuedUnit, transfusedUnit);
        await c.SaveChangesAsync();

        c.Allocations.Add(new Allocation
        {
            BloodProductId = reservedUnit.Id, PatientId = withUnits.Id, Status = AllocationStatus.Reserved,
            AllocatedUtc = now.AddHours(-1), ExpiresUtc = now.AddHours(20)
        });
        c.Issues.AddRange(
            new Issue { BloodProductId = issuedUnit.Id, PatientId = withUnits.Id, IssuedUtc = now.AddMinutes(-30), Status = IssueStatus.Issued, IssuedToLocation = "ICU" },
            new Issue { BloodProductId = transfusedUnit.Id, PatientId = transfusedOnly.Id, IssuedUtc = now.AddHours(-5), Status = IssueStatus.Transfused });
        await c.SaveChangesAsync();

        var summary = await Service(c).GetAsync();
        var mine = summary.Patients.Where(p => p.MedicalRecordNumber.EndsWith(key, StringComparison.Ordinal)).ToList();

        // Only a current specimen, a reserved unit, or an issued unit brings a patient onto the sheet.
        Assert.Equal(
            new[] { withSpecimen.MedicalRecordNumber, withUnits.MedicalRecordNumber },
            mine.Select(p => p.MedicalRecordNumber).Order(StringComparer.Ordinal).ToArray());

        var a = mine.Single(p => p.PatientId == withSpecimen.Id);
        Assert.Equal("A-", a.AboRh);
        Assert.True(a.SecondDeterminationOnFile);
        Assert.Equal(new[] { "anti-K" }, a.Antibodies);
        Assert.Single(a.SpecialRequirements);
        Assert.Equal(new[] { "Call the hematology fellow before issue" }, a.Informational);
        Assert.Equal($"ACC-DTP-A-{key}", Assert.Single(a.Specimens).AccessionNumber);
        Assert.Empty(a.Units);

        var o = mine.Single(p => p.PatientId == withUnits.Id);
        Assert.Equal("O+", o.AboRh);
        Assert.False(o.SecondDeterminationOnFile);
        Assert.Empty(o.Antibodies);
        Assert.Empty(o.Specimens);
        Assert.Collection(o.Units,
            reserved =>
            {
                Assert.Equal(reservedUnit.UnitNumber, reserved.UnitNumber);
                Assert.False(reserved.Issued);
                Assert.Equal("Downtime RBC", reserved.Product);
                Assert.Equal(now.AddHours(20), reserved.ReservationEndsUtc);
            },
            issued =>
            {
                Assert.Equal(issuedUnit.UnitNumber, issued.UnitNumber);
                Assert.True(issued.Issued);
                Assert.Equal("ICU", issued.IssuedToLocation);
            });
    }

    // TEST-BB-434: from the chart, one patient's sheet: that patient only, even with nothing current; the print names them.
    [Fact]
    public async Task OnePatient_IsOnTheSheetEvenWithNothingCurrent_AndThePrintIsAuditedAgainstThem()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var quiet = NewPatient(key, "Quiet");
        var busy = NewPatient(key, "Busy");
        c.Patients.AddRange(quiet, busy);
        await c.SaveChangesAsync();
        c.Specimens.Add(NewSpecimen(busy.Id, $"ACC-DTP-B-{key}", _factory.Clock.UtcNow.AddDays(1)));
        c.AntibodyHistory.Add(new AntibodyHistory { PatientId = quiet.Id, AntibodySpecificity = "anti-Fya", IsActive = true });
        await c.SaveChangesAsync();

        var one = await Service(c).GetAsync(quiet.Id);
        var row = Assert.Single(one.Patients);
        Assert.Equal(quiet.Id, row.PatientId);
        Assert.Equal(new[] { "anti-Fya" }, row.Antibodies);
        Assert.Empty(row.Specimens);
        Assert.Equal("Not on file", row.AboRh);

        // The busy patient's own sheet carries only their specimen.
        Assert.Equal($"ACC-DTP-B-{key}", Assert.Single(Assert.Single((await Service(c).GetAsync(busy.Id)).Patients).Specimens).AccessionNumber);
        // An id that is no patient gives an empty sheet rather than everyone.
        Assert.Empty((await Service(c).GetAsync(long.MaxValue)).Patients);

        Assert.Equal(1, await Service(c).RecordPrintedAsync(quiet.Id));
        var printed = await c.AuditEvents
            .Where(e => e.EventType == AuditEventType.Print && e.EntityType == "DowntimePatientSummary" && e.EntityId == quiet.Id)
            .SingleAsync();
        Assert.Contains("one patient", printed.NewValueJson);
    }

    // TEST-BB-580: a patient with an RhIG evaluation still needing work is on the sheet with what is outstanding; one whose
    // RhIG was given is not added for it.
    [Fact]
    public async Task Summary_CarriesRhigStillNeedingWork()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var waiting = NewPatient(key, "Rhwait");
        var done = NewPatient(key, "Rhdone");
        c.Patients.AddRange(waiting, done);
        await c.SaveChangesAsync();
        var now = _factory.Clock.UtcNow;
        c.RhigEvaluations.AddRange(
            new RhigEvaluation
            {
                PatientId = waiting.Id, Context = RhigContext.Postpartum, EventUtc = now.AddHours(-8), Decision = RhigDecision.Candidate,
                VialsRecommended = 1, RecordedBy = "tech", RecordedUtc = now, CompletedBy = "lead", CompletedUtc = now
            },
            new RhigEvaluation
            {
                PatientId = done.Id, Context = RhigContext.Postpartum, EventUtc = now.AddHours(-8), Decision = RhigDecision.Candidate,
                VialsRecommended = 1, RecordedBy = "tech", RecordedUtc = now, CompletedBy = "lead", CompletedUtc = now,
                GivenLot = "L1", GivenVials = 1, GivenUtc = now, GivenBy = "tech"
            });
        await c.SaveChangesAsync();

        var sheet = await Service(c).GetAsync();
        var row = Assert.Single(sheet.Patients, r => r.PatientId == waiting.Id);
        Assert.Equal($"Delivery {now.AddHours(-8):yyyy-MM-dd}: candidate, 1 vial recommended; RhIG not yet given", Assert.Single(row.Rhig!));
        Assert.DoesNotContain(sheet.Patients, r => r.PatientId == done.Id);
        Assert.Empty(Assert.Single((await Service(c).GetAsync(done.Id)).Patients).Rhig!);
    }

    [Fact]
    public async Task Print_IsAudited_WithThePatientsOnTheSheet()
    {
        await using var c = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var patient = NewPatient(key, "Printed");
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        c.Specimens.Add(NewSpecimen(patient.Id, $"ACC-DTP-P-{key}", _factory.Clock.UtcNow.AddDays(1)));
        await c.SaveChangesAsync();

        var count = await Service(c).RecordPrintedAsync();

        Assert.True(count >= 1);
        var printed = await c.AuditEvents
            .Where(e => e.EventType == AuditEventType.Print && e.EntityType == "DowntimePatientSummary")
            .OrderByDescending(e => e.Id)
            .FirstAsync();
        Assert.Equal("Downtime patient summary printed.", printed.Reason);
        Assert.Contains(patient.Id.ToString(), printed.NewValueJson);
    }
}
