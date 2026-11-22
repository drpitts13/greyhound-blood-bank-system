using System.Text.Json.Nodes;
using BloodBankLIS.Application.Interop.Fhir;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-320: read-only FHIR R4 API (OCD-043). Current data only, no invented codes, every patient read audited.
public class FhirReadServiceTests : IDisposable
{
    private const string Din = "A999926654321";
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private FhirReadService Fhir(BloodBankDbContext c) =>
        new(new EfRepository<Patient>(c), new EfRepository<PatientBloodTypeHistory>(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<SpecialTransfusionRequirement>(c), new EfRepository<SpecialRequirementDefinition>(c),
            new EfRepository<Order>(c), new EfRepository<BloodUnit>(c), new EfRepository<ProductType>(c), c,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser));

    private FhirReadService FhirWithResults(BloodBankDbContext c) =>
        new(new EfRepository<Patient>(c), new EfRepository<PatientBloodTypeHistory>(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<SpecialTransfusionRequirement>(c), new EfRepository<SpecialRequirementDefinition>(c),
            new EfRepository<Order>(c), new EfRepository<BloodUnit>(c), new EfRepository<ProductType>(c), c,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser),
            new EfRepository<TestResult>(c), new EfRepository<TestDefinition>(c), new EfRepository<OrderLine>(c),
            new EfRepository<Specimen>(c), new EfRepository<SpecimenTypeDefinition>(c));

    // TEST-BB-552: the patient's tubes are Specimens (accession, type with its description, collection, receipt,
    // expiry note); a rejected tube is unsatisfactory with its reason, an expired one unavailable, a cancelled one
    // entered-in-error; results and reports point at their tube.
    [Fact]
    public async Task Specimens_CarryTheirStateAndExpiry_AndResultsPointAtThem()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var now = _factory.Clock.UtcNow;
        var good = new Specimen { AccessionNumber = "ACC-FHIR-S1", PatientId = f.Patient.Id, SpecimenType = "EDTA", CollectedUtc = now.AddHours(-5), ReceivedUtc = now.AddHours(-4), ExpiresUtc = now.AddDays(3), Collector = "RN Patel", Status = SpecimenStatus.Accepted };
        var rejected = new Specimen { AccessionNumber = "ACC-FHIR-S2", PatientId = f.Patient.Id, SpecimenType = "EDTA", CollectedUtc = now.AddHours(-6), Status = SpecimenStatus.Rejected, RejectionReason = "Unlabeled tube" };
        var expired = new Specimen { AccessionNumber = "ACC-FHIR-S3", PatientId = f.Patient.Id, SpecimenType = "EDTA", CollectedUtc = now.AddDays(-5), Status = SpecimenStatus.Expired };
        var cancelled = new Specimen { AccessionNumber = "ACC-FHIR-S4", PatientId = f.Patient.Id, SpecimenType = "EDTA", CollectedUtc = now.AddDays(-6), Status = SpecimenStatus.Cancelled };
        c.AddRange(good, rejected, expired, cancelled);
        await c.SaveChangesAsync();
        var result = new TestResult { SpecimenId = good.Id, PatientId = f.Patient.Id, TestCode = "ABSC", Value = "Negative", Status = ResultStatus.Verified, Source = ResultSource.Manual, EnteredUtc = now, VerifiedUtc = now };
        c.Add(result);
        await c.SaveChangesAsync();

        var resources = Resources(await FhirWithResults(c).SearchSpecimensAsync(f.Patient.Id, "/fhir/Specimen"));

        Assert.Equal(["ACC-FHIR-S1", "ACC-FHIR-S2", "ACC-FHIR-S3", "ACC-FHIR-S4"], resources.Select(r => (string?)r["accessionIdentifier"]!["value"]));
        Assert.Equal(["available", "unsatisfactory", "unavailable", "entered-in-error"], resources.Select(r => (string?)r["status"]));
        var s1 = resources[0];
        Assert.Equal(("EDTA", "RN Patel"), ((string?)s1["type"]!["coding"]![0]!["code"], (string?)s1["collection"]!["collector"]!["display"]));
        Assert.Contains("Valid for testing until", (string?)s1["note"]![0]!["text"]);
        Assert.Equal("Rejected: Unlabeled tube", (string?)resources[1]["note"]![0]!["text"]);
        Assert.Null(resources[2]["receivedTime"]);

        var observation = await FhirWithResults(c).ReadObservationAsync($"result-{result.Id}");
        Assert.Equal($"Specimen/{good.Id}", (string?)observation!["specimen"]!["reference"]);
        Assert.Equal("ACC-FHIR-S2", (string?)(await FhirWithResults(c).ReadSpecimenAsync(rejected.Id))!["accessionIdentifier"]!["value"]);
        Assert.Null(await FhirWithResults(c).ReadSpecimenAsync(999_999));
    }

    // TEST-BB-551: an order's verified results are a blood bank DiagnosticReport: partial while a test on the order
    // has no verified result, final when all have, corrected after a verified correction; an order with none is absent.
    [Fact]
    public async Task OrderReport_IsPartialThenFinalThenCorrected()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var now = _factory.Clock.UtcNow;
        var order = await c.Orders.SingleAsync(o => o.OrderNumber == "ORD-FHIR1");
        order.TestCode = "TNS";
        c.OrderLines.AddRange(
            new OrderLine { OrderId = order.Id, LineNumber = 1, LineName = "ABO/Rh", TestCode = "ABORH", IsActive = true },
            new OrderLine { OrderId = order.Id, LineNumber = 2, LineName = "Screen", TestCode = "ABSC", IsActive = true });
        var specimen = new Specimen { AccessionNumber = "ACC-FHIR-DR", PatientId = f.Patient.Id, SpecimenType = "EDTA", CollectedUtc = now.AddHours(-3), Status = SpecimenStatus.Accepted };
        c.Add(specimen);
        await c.SaveChangesAsync();
        var abo = new TestResult { SpecimenId = specimen.Id, PatientId = f.Patient.Id, OrderId = order.Id, TestCode = "ABORH", Value = "O|Negative", Status = ResultStatus.Verified, Source = ResultSource.Manual, EnteredUtc = now.AddHours(-2), VerifiedUtc = now.AddHours(-1) };
        var screen = new TestResult { SpecimenId = specimen.Id, PatientId = f.Patient.Id, OrderId = order.Id, TestCode = "ABSC", Value = "Negative", Status = ResultStatus.Entered, Source = ResultSource.Manual, EnteredUtc = now.AddHours(-2) };
        c.AddRange(abo, screen);
        await c.SaveChangesAsync();

        var partial = Assert.Single(Resources(await FhirWithResults(c).SearchDiagnosticReportsAsync(f.Patient.Id, "/fhir/DiagnosticReport")));
        Assert.Equal(("partial", "BLB", "TNS"), ((string?)partial["status"], (string?)partial["category"]![0]!["coding"]![0]!["code"], (string?)partial["code"]!["coding"]![0]!["code"]));
        Assert.Equal([$"Observation/result-{abo.Id}"], partial["result"]!.AsArray().Select(r => (string?)r!["reference"]));
        Assert.Equal($"ServiceRequest/{order.Id}", (string?)partial["basedOn"]![0]!["reference"]);
        Assert.Equal([$"Specimen/{specimen.Id}"], partial["specimen"]!.AsArray().Select(s => (string?)s!["reference"]));

        screen.Status = ResultStatus.Verified;
        screen.VerifiedUtc = now;
        await c.SaveChangesAsync();
        var final = await FhirWithResults(c).ReadDiagnosticReportAsync(order.Id);
        Assert.Equal("final", (string?)final!["status"]);
        Assert.Equal(2, final["result"]!.AsArray().Count);
        Assert.Equal(now.ToString("yyyy-MM-ddTHH:mm:ssZ"), (string?)final["issued"]);

        screen.Status = ResultStatus.Corrected;
        await c.SaveChangesAsync();
        Assert.Equal("corrected", (string?)(await FhirWithResults(c).ReadDiagnosticReportAsync(order.Id))!["status"]);
        Assert.Null(await FhirWithResults(c).ReadDiagnosticReportAsync(999_999));
        Assert.Empty(Resources(await FhirWithResults(c).SearchDiagnosticReportsAsync(999_999, "/x")));
    }

    // TEST-BB-548: verified results reach the EHR as laboratory Observations: local code plus the test's LOINC code
    // when it has one, numeric value with units, reference range, and flag, linked to the order; corrected ones as
    // corrected; unverified ones never; an invalidated one, read by id, as entered-in-error.
    [Fact]
    public async Task VerifiedResults_AreObservations_WithLoincAndQuantity()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var now = _factory.Clock.UtcNow;
        c.TestDefinitions.AddRange(
            new TestDefinition { Code = "FHGB", Name = "Hemoglobin", ResultValueType = ResultValueType.Numeric, ResultUnits = "g/dL", ReferenceLow = 12m, ReferenceHigh = 16m, LoincCode = "718-7", IsActive = true, IsDraft = false, EffectiveUtc = now, Version = 1 },
            new TestDefinition { Code = "FDAT", Name = "DAT", ResultValueType = ResultValueType.Coded, AllowedResultValues = "Negative\nPositive", IsActive = true, EffectiveUtc = now, Version = 1 });
        var specimen = new Specimen { AccessionNumber = "ACC-FHIR-R", PatientId = f.Patient.Id, SpecimenType = "EDTA", CollectedUtc = now.AddHours(-3), Status = SpecimenStatus.Accepted };
        c.Add(specimen);
        await c.SaveChangesAsync();
        var order = await c.Orders.SingleAsync(o => o.OrderNumber == "ORD-FHIR1");
        TestResult R(string code, string value, ResultStatus status, bool verified, string? flag = null) => new()
        {
            SpecimenId = specimen.Id, PatientId = f.Patient.Id, OrderId = order.Id, TestCode = code, Value = value, Status = status, AbnormalFlag = flag,
            Source = ResultSource.Manual, EnteredUtc = now.AddHours(-2), EnteredBy = "tech", VerifiedUtc = verified ? now.AddHours(-1) : null, VerifiedBy = verified ? "sup" : null
        };
        var hgb = R("FHGB", "11.2", ResultStatus.Verified, true, "L");
        var dat = R("FDAT", "Positive", ResultStatus.Corrected, true);
        var pending = R("FHGB", "13.0", ResultStatus.Entered, false);
        var withdrawn = R("FDAT", "Negative", ResultStatus.Invalidated, true);
        var abo = R("ABORH", "O|Positive", ResultStatus.Verified, true);
        c.AddRange(hgb, dat, pending, withdrawn, abo);
        await c.SaveChangesAsync();

        var resources = Resources(await FhirWithResults(c).SearchObservationsAsync(f.Patient.Id, "/fhir/Observation"));

        var results = resources.Where(r => ((string?)r["id"])!.StartsWith("result-")).ToList();
        Assert.Equal(new[] { $"result-{abo.Id}", $"result-{dat.Id}", $"result-{hgb.Id}" }.Order(), results.Select(r => (string?)r["id"]!).Order());
        // An ABO/Rh result reads as the chart shows it, not its stored form.
        Assert.Equal(BloodBankLIS.Domain.ValueObjects.AboRhResultValue.FormatDisplay("O|Positive"),
            (string?)results.Single(r => (string?)r["id"] == $"result-{abo.Id}")["valueString"]);
        Assert.NotEqual("O|Positive", (string?)results.Single(r => (string?)r["id"] == $"result-{abo.Id}")["valueString"]);
        var h = results.Single(r => (string?)r["id"] == $"result-{hgb.Id}");
        Assert.Equal("final", (string?)h["status"]);
        Assert.Equal((FhirMapper.ObservationCodeSystem, "FHGB"), ((string?)h["code"]!["coding"]![0]!["system"], (string?)h["code"]!["coding"]![0]!["code"]));
        Assert.Equal((FhirMapper.LoincSystem, "718-7"), ((string?)h["code"]!["coding"]![1]!["system"], (string?)h["code"]!["coding"]![1]!["code"]));
        Assert.Equal((11.2m, "g/dL"), ((decimal)h["valueQuantity"]!["value"]!, (string?)h["valueQuantity"]!["unit"]));
        Assert.Equal((12m, 16m), ((decimal)h["referenceRange"]![0]!["low"]!["value"]!, (decimal)h["referenceRange"]![0]!["high"]!["value"]!));
        Assert.Equal("L", (string?)h["interpretation"]![0]!["coding"]![0]!["code"]);
        Assert.Equal($"ServiceRequest/{order.Id}", (string?)h["basedOn"]![0]!["reference"]);
        var d = results.Single(r => (string?)r["id"] == $"result-{dat.Id}");
        Assert.Equal(("corrected", "Positive"), ((string?)d["status"], (string?)d["valueString"]));
        Assert.Single(d["code"]!["coding"]!.AsArray());
        Assert.DoesNotContain(":null", resources.Select(r => r.ToJsonString()).Aggregate((a, b) => a + b));

        var read = await FhirWithResults(c).ReadObservationAsync($"result-{withdrawn.Id}");
        Assert.Equal("entered-in-error", (string?)read!["status"]);
        Assert.Null(await FhirWithResults(c).ReadObservationAsync($"result-{pending.Id}"));
    }

    private static Task<int> AccessCountAsync(BloodBankDbContext c, long patientId) =>
        c.AuditEvents.CountAsync(a => a.EventType == AuditEventType.PatientAccess && a.EntityId == patientId);

    [Fact]
    public async Task ReadPatient_MapsDemographics_AndAuditsTheRead()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var p = await Fhir(c).ReadPatientAsync(f.Patient.Id);

        Assert.NotNull(p);
        Assert.Equal("Patient", (string?)p["resourceType"]);
        Assert.Equal(FhirMapper.MrnSystem, (string?)p["identifier"]![0]!["system"]);
        Assert.Equal("MRN-FHIR1", (string?)p["identifier"]![0]!["value"]);
        Assert.Equal("Fhirson", (string?)p["name"]![0]!["family"]);
        Assert.Equal("female", (string?)p["gender"]);
        Assert.Equal("1970-05-01", (string?)p["birthDate"]);
        Assert.Null(p["link"]);
        Assert.Equal(1, await AccessCountAsync(c, f.Patient.Id));
    }

    [Fact]
    public async Task ReadPatient_Unknown_ReturnsNull_AndAuditsNothing()
    {
        await using var c = _factory.Create();
        await SeedAsync(c);

        Assert.Null(await Fhir(c).ReadPatientAsync(999_999));
        Assert.False(await c.AuditEvents.AnyAsync(a => a.EventType == AuditEventType.PatientAccess));
    }

    [Fact]
    public async Task MergedPatient_LinksToTheSurvivor()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var duplicate = new Patient
        {
            MedicalRecordNumber = "MRN-FHIR-DUP", LastName = "Fhirson", FirstName = "Ann", DateOfBirth = new DateOnly(1970, 5, 1),
            Sex = Sex.Female, Status = PatientStatus.Merged, MergedIntoPatientId = f.Patient.Id
        };
        c.Add(duplicate);
        await c.SaveChangesAsync();

        var p = await Fhir(c).ReadPatientAsync(duplicate.Id);

        Assert.False((bool)p!["active"]!);
        Assert.Equal("replaced-by", (string?)p["link"]![0]!["type"]);
        Assert.Equal($"Patient/{f.Patient.Id}", (string?)p["link"]![0]!["other"]!["reference"]);
    }

    [Theory]
    [InlineData("MRN-FHIR1", 1)]
    [InlineData("urn:hemocore:mrn|MRN-FHIR1", 1)]
    [InlineData("|MRN-FHIR1", 1)]
    [InlineData("http://other-hospital/mrn|MRN-FHIR1", 0)]
    [InlineData("MRN-NOBODY", 0)]
    public async Task SearchPatient_ByIdentifier_HonorsTheSystem(string identifier, int expected)
    {
        await using var c = _factory.Create();
        await SeedAsync(c);

        var bundle = await Fhir(c).SearchPatientsAsync(identifier, "/fhir/Patient");

        Assert.Equal("searchset", (string?)bundle["type"]);
        Assert.Equal(expected, (int)bundle["total"]!);
    }

    [Fact]
    public async Task Observations_AreCurrentTypeAndActiveAntibodiesOnly()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var bundle = await Fhir(c).SearchObservationsAsync(f.Patient.Id, "/fhir/Observation?patient=1");

        var resources = Resources(bundle);
        Assert.Equal(2, resources.Count);
        var abo = Assert.Single(resources, r => (string?)r["code"]!["coding"]![0]!["code"] == "ABORH");
        Assert.Equal("O Neg", (string?)abo["valueCodeableConcept"]!["text"]);
        var antibody = Assert.Single(resources, r => (string?)r["code"]!["coding"]![0]!["code"] == "ANTIBODY");
        Assert.Equal("anti-K", (string?)antibody["valueString"]);
        Assert.Equal($"Patient/{f.Patient.Id}", (string?)antibody["subject"]!["reference"]);
        Assert.Null(antibody["note"]);
        Assert.Equal(1, await AccessCountAsync(c, f.Patient.Id));
    }

    [Fact]
    public async Task NoCode_ClaimsAnExternalTerminology()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var svc = Fhir(c);

        var json = string.Join("\n",
            (await svc.SearchObservationsAsync(f.Patient.Id, "/x")).ToJsonString(),
            (await svc.SearchFlagsAsync(f.Patient.Id, "/x")).ToJsonString(),
            (await svc.SearchServiceRequestsAsync(f.Patient.Id, "/x")).ToJsonString());

        Assert.DoesNotContain("loinc.org", json);
        Assert.DoesNotContain("snomed.info", json);
        Assert.DoesNotContain(":null", json);
    }

    [Fact]
    public async Task Flags_IncludeEndedRequirements_AsInactive()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var resources = Resources(await Fhir(c).SearchFlagsAsync(f.Patient.Id, "/fhir/Flag"));

        Assert.Equal(2, resources.Count);
        var irradiated = Assert.Single(resources, r => (string?)r["status"] == "active");
        Assert.Equal("Irradiated blood", (string?)irradiated["code"]!["text"]);
        var ended = Assert.Single(resources, r => (string?)r["status"] == "inactive");
        Assert.Equal("K-negative blood", (string?)ended["code"]!["text"]);
        Assert.NotNull(ended["period"]!["end"]);
    }

    [Fact]
    public async Task ServiceRequests_MapStatusAndPriority()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var sr = Assert.Single(Resources(await Fhir(c).SearchServiceRequestsAsync(f.Patient.Id, "/fhir/ServiceRequest")));

        Assert.Equal("ORD-FHIR1", (string?)sr["identifier"]![0]!["value"]);
        Assert.Equal("stat", (string?)sr["priority"]);
        Assert.Equal("active", (string?)sr["status"]);
        Assert.Equal("order", (string?)sr["intent"]);
    }

    [Fact]
    public async Task SearchForAPatientWithNoData_IsEmpty_AndNotAudited()
    {
        await using var c = _factory.Create();
        await SeedAsync(c);

        var bundle = await Fhir(c).SearchObservationsAsync(999_999, "/fhir/Observation");

        Assert.Equal(0, (int)bundle["total"]!);
        Assert.False(await c.AuditEvents.AnyAsync(a => a.EventType == AuditEventType.PatientAccess));
    }

    [Theory]
    [InlineData("abo-x")]
    [InlineData("12")]
    [InlineData("antibody-999999")]
    public async Task ReadObservation_BadOrUnknownId_ReturnsNull(string id)
    {
        await using var c = _factory.Create();
        await SeedAsync(c);

        Assert.Null(await Fhir(c).ReadObservationAsync(id));
    }

    [Fact]
    public async Task ReadObservation_SupersededTypeOrInactiveAntibody_IsNotServed()
    {
        await using var c = _factory.Create();
        await SeedAsync(c);
        var oldType = await c.Set<PatientBloodTypeHistory>().SingleAsync(b => !b.IsCurrent);
        var currentType = await c.Set<PatientBloodTypeHistory>().SingleAsync(b => b.IsCurrent);
        var retired = await c.Set<AntibodyHistory>().SingleAsync(a => !a.IsActive);
        var svc = Fhir(c);

        Assert.Null(await svc.ReadObservationAsync("abo-" + oldType.Id));
        Assert.Null(await svc.ReadObservationAsync("antibody-" + retired.Id));
        Assert.NotNull(await svc.ReadObservationAsync("abo-" + currentType.Id));
    }

    [Fact]
    public async Task Product_ByDin_MapsCategoryStatusAndExpiry()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var bundle = await Fhir(c).SearchProductsAsync($"{FhirMapper.DinSystem}|{Din}", "/fhir/BiologicallyDerivedProduct");

        var product = Assert.Single(Resources(bundle));
        Assert.Equal(f.Unit.Id.ToString(), (string?)product["id"]);
        Assert.Equal("cells", (string?)product["productCategory"]);
        Assert.Equal("available", (string?)product["status"]);
        Assert.Equal("Red cells", (string?)product["productCode"]!["text"]);
        Assert.NotNull(product["storage"]![0]!["duration"]!["end"]);
        Assert.Equal(0, (int)(await Fhir(c).SearchProductsAsync("urn:wrong|" + Din, "/x"))["total"]!);
    }

    [Theory]
    [InlineData("Patient/12", true, 12)]
    [InlineData("12", true, 12)]
    [InlineData("Practitioner/12", false, 0)]
    [InlineData("", false, 0)]
    [InlineData("Patient/-1", false, 0)]
    public void PatientReference_Parsing(string reference, bool ok, long id)
    {
        Assert.Equal(ok, FhirReadService.TryParsePatientReference(reference, out var parsed));
        if (ok)
        {
            Assert.Equal(id, parsed);
        }
    }

    [Fact]
    public void Capability_IsReadOnlyR4()
    {
        var cap = FhirMapper.Capability(_factory.Clock.UtcNow);

        Assert.Equal("4.0.1", (string?)cap["fhirVersion"]);
        var interactions = cap["rest"]![0]!["resource"]!.AsArray()
            .SelectMany(r => r!["interaction"]!.AsArray().Select(i => (string?)i!["code"]))
            .Distinct().ToList();
        Assert.Equal(["read", "search-type"], interactions.Order().ToList());
    }

    private static List<JsonObject> Resources(JsonObject bundle) =>
        bundle["entry"]!.AsArray().Select(e => e!["resource"]!.AsObject()).ToList();

    private sealed record Fixture(Patient Patient, BloodUnit Unit);

    private async Task<Fixture> SeedAsync(BloodBankDbContext c)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-FHIR1", LastName = "Fhirson", FirstName = "Ann", DateOfBirth = new DateOnly(1970, 5, 1), Sex = Sex.Female
        };
        var location = new OrderingLocation { Code = "FHIR-WARD", Name = "Ward 9", IsActive = true };
        var product = new ProductType { ProductCode = "RBC-FHIR", Name = "Red cells", ComponentClass = ComponentClass.RedBloodCells };
        c.AddRange(patient, location, product);
        await c.SaveChangesAsync();

        var encounter = new Encounter
        {
            PatientId = patient.Id, VisitNumber = "V-FHIR", EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active, AdmitUtc = _factory.Clock.UtcNow.AddDays(-1)
        };
        c.Add(encounter);
        var unit = new BloodUnit
        {
            UnitNumber = $"{Din}-A", Din = Din, ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Negative,
            Status = UnitStatus.Available, ExpiresUtc = _factory.Clock.UtcNow.AddDays(10)
        };
        c.AddRange(
            unit,
            new PatientBloodTypeHistory { PatientId = patient.Id, Abo = AboGroup.A, RhD = RhType.Positive, IsCurrent = false },
            new PatientBloodTypeHistory { PatientId = patient.Id, Abo = AboGroup.O, RhD = RhType.Negative, IsCurrent = true },
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-K", IsActive = true },
            new AntibodyHistory { PatientId = patient.Id, AntibodySpecificity = "anti-E", IsActive = false, DeactivationReason = "Entered on wrong patient" },
            new SpecialTransfusionRequirement
            {
                PatientId = patient.Id, RequirementType = SpecialTransfusionRequirementType.Irradiated, Reason = "Stem cell transplant",
                EffectiveUtc = _factory.Clock.UtcNow.AddDays(-5), IsActive = true
            },
            new SpecialTransfusionRequirement
            {
                PatientId = patient.Id, RequirementType = SpecialTransfusionRequirementType.AntigenNegative, AntigenCode = "K",
                Reason = "anti-K", EffectiveUtc = _factory.Clock.UtcNow.AddDays(-30), ExpiresUtc = _factory.Clock.UtcNow.AddDays(-2), IsActive = false
            });
        await c.SaveChangesAsync();

        c.Add(new Order
        {
            OrderNumber = "ORD-FHIR1", PatientId = patient.Id, EncounterId = encounter.Id, OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Test, OrderName = "Type and screen", OrderType = OrderType.TypeAndScreen, Priority = OrderPriority.Stat
        });
        await c.SaveChangesAsync();
        return new Fixture(patient, unit);
    }
}
