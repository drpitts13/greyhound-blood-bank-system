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
