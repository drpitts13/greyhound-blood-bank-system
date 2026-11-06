using BloodBankLIS.Application.Search;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-307: top-bar "scan anything" lookup. One exact match opens; partial or several matches list.
public class UniversalLookupTests : IDisposable
{
    private const string Din = "A999926123456";
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static UniversalLookupService Lookup(BloodBankDbContext c) =>
        new(new EfRepository<Patient>(c), new EfRepository<PatientIdentifier>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), new EfRepository<Specimen>(c), new EfRepository<Order>(c),
            encounters: new EfRepository<Encounter>(c));

    [Fact]
    public async Task ScannedDin_OpensTheOneUnit()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var result = await Lookup(c).LookupAsync($"={Din}00");

        Assert.True(result.OpenDirectly);
        var hit = Assert.Single(result.Hits);
        Assert.Equal(LookupHitKind.Unit, hit.Kind);
        Assert.Equal(f.Unit.Id, hit.Id);
        Assert.Equal($"/inventory/units/{f.Unit.Id}", hit.Href);
        Assert.Contains("O Pos", hit.Detail);
    }

    [Fact]
    public async Task HumanReadableDin_WithSpaces_OpensTheUnit()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var result = await Lookup(c).LookupAsync("A9999 26 123456");

        Assert.True(result.OpenDirectly);
        Assert.Equal(f.Unit.Id, Assert.Single(result.Hits).Id);
    }

    [Fact]
    public async Task DinSharedByDividedComponents_ListsEach_AndDoesNotOpen()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        c.BloodUnits.Add(new BloodUnit
        {
            UnitNumber = $"{Din}-B", Din = Din, ProductTypeId = f.Product.Id, Abo = AboGroup.O, RhD = RhType.Positive,
            Status = UnitStatus.Available, ExpiresUtc = _factory.Clock.UtcNow.AddDays(10)
        });
        await c.SaveChangesAsync();

        var result = await Lookup(c).LookupAsync($"={Din}00");

        Assert.False(result.OpenDirectly);
        Assert.Equal(2, result.Hits.Count);
        Assert.All(result.Hits, h => Assert.True(h.Exact));
    }

    [Fact]
    public async Task MalformedScannerDin_SaysSo_AndDoesNotSearchNames()
    {
        await using var c = _factory.Create();
        await SeedAsync(c);

        var result = await Lookup(c).LookupAsync("=A9999");

        Assert.Empty(result.Hits);
        Assert.False(result.OpenDirectly);
        Assert.Contains("not a readable unit number", result.Message);
    }

    [Fact]
    public async Task Accession_AnyCase_OpensThePatientsSpecimens()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var result = await Lookup(c).LookupAsync("acc-look1");

        Assert.True(result.OpenDirectly);
        var hit = Assert.Single(result.Hits);
        Assert.Equal(LookupHitKind.Specimen, hit.Kind);
        Assert.Equal($"/patients/{f.Patient.Id}?tab=specimens&specimen={hit.Id}", hit.Href);
        Assert.Contains("MRN-LOOK1", hit.Title);
    }

    [Fact]
    public async Task OrderNumber_OpensThePatientsOrders()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var result = await Lookup(c).LookupAsync("ORD-LOOK1");

        Assert.True(result.OpenDirectly);
        var hit = Assert.Single(result.Hits);
        Assert.Equal(LookupHitKind.Order, hit.Kind);
        Assert.Equal($"/patients/{f.Patient.Id}?tab=orders&order=ORD-LOOK1", hit.Href);
    }

    [Fact]
    public async Task Mrn_OpensTheChart()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);

        var result = await Lookup(c).LookupAsync("  mrn-look1\r\n");

        Assert.True(result.OpenDirectly);
        Assert.Equal($"/patients/{f.Patient.Id}", Assert.Single(result.Hits).Href);
    }

    [Fact]
    public async Task PriorMrn_OpensTheSurvivor_AndSaysWhy()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        c.PatientIdentifiers.Add(new PatientIdentifier
        {
            PatientId = f.Patient.Id, IdentifierType = IdentityTokenType.PriorMedicalRecordNumber, Value = "OLD-0042", IsActive = true
        });
        await c.SaveChangesAsync();

        var result = await Lookup(c).LookupAsync("OLD-0042");

        Assert.True(result.OpenDirectly);
        var hit = Assert.Single(result.Hits);
        Assert.Equal(f.Patient.Id, hit.Id);
        Assert.Contains("Prior MRN OLD-0042", hit.Detail);
    }

    // TEST-BB-504: a visit or account number, or another recorded identifier, finds the patient.
    [Fact]
    public async Task VisitNumber_OpensThatVisitsOrders_AndSaysItIsOpen()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var visit = c.Encounters.Single(e => e.VisitNumber == "V-LOOK");
        visit.AccountNumber = "FIN-7781";
        visit.CurrentLocation = "Ward 7";
        await c.SaveChangesAsync();

        foreach (var scan in new[] { "v-look", "FIN-7781" })
        {
            var result = await Lookup(c).LookupAsync(scan);

            Assert.True(result.OpenDirectly);
            var hit = Assert.Single(result.Hits);
            Assert.Equal(LookupHitKind.Patient, hit.Kind);
            Assert.Equal(f.Patient.Id, hit.Id);
            Assert.Equal($"/patients/{f.Patient.Id}?tab=orders&visit={visit.Id}", hit.Href);
            Assert.Contains("Visit V-LOOK · Inpatient · Ward 7 · open", hit.Detail);
        }
    }

    [Fact]
    public async Task DischargedVisit_SaysSo_AndAMergedVisitOpensTheSurvivor()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var retired = new Patient
        {
            MedicalRecordNumber = "MRN-OLDV", LastName = "Lookup", FirstName = "Ann", DateOfBirth = new DateOnly(1970, 5, 1),
            Sex = Sex.Female, Status = PatientStatus.Merged
        };
        c.Patients.Add(retired);
        await c.SaveChangesAsync();
        retired.MergedIntoPatientId = f.Patient.Id;
        c.Encounters.Add(new Encounter
        {
            PatientId = retired.Id, VisitNumber = "V-OLD", EncounterType = EncounterType.Outpatient,
            Status = EncounterStatus.Discharged, DischargeUtc = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc)
        });
        await c.SaveChangesAsync();

        var hit = Assert.Single((await Lookup(c).LookupAsync("V-OLD")).Hits);

        Assert.Equal(f.Patient.Id, hit.Id);
        Assert.Equal($"/patients/{f.Patient.Id}", hit.Href);
        Assert.Contains("was merged into MRN MRN-LOOK1", hit.Detail);
        Assert.Contains("Visit V-OLD · Outpatient · discharged 2026-10-02", hit.Detail);
    }

    [Fact]
    public async Task AccountAndOtherIdentifiers_FindThePatient_ButNamesStayPartial()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        c.PatientIdentifiers.AddRange(
            new PatientIdentifier { PatientId = f.Patient.Id, IdentifierType = IdentityTokenType.AccountNumber, Value = "ACCT-55", IsActive = true },
            new PatientIdentifier { PatientId = f.Patient.Id, IdentifierType = IdentityTokenType.Other, Value = "EMPI-9", IsActive = true },
            new PatientIdentifier { PatientId = f.Patient.Id, IdentifierType = IdentityTokenType.Other, Value = "GONE-1", IsActive = false },
            new PatientIdentifier { PatientId = f.Patient.Id, IdentifierType = IdentityTokenType.PriorFullName, Value = "SMITHERS", IsActive = true });
        await c.SaveChangesAsync();

        var account = await Lookup(c).LookupAsync("acct-55");
        Assert.True(account.OpenDirectly);
        Assert.Contains("Account number ACCT-55", Assert.Single(account.Hits).Detail);

        var other = await Lookup(c).LookupAsync("EMPI-9");
        Assert.True(other.OpenDirectly);
        Assert.Contains("Identifier EMPI-9", Assert.Single(other.Hits).Detail);

        Assert.DoesNotContain((await Lookup(c).LookupAsync("GONE-1")).Hits, h => h.Exact);
        var name = await Lookup(c).LookupAsync("SMITHERS");
        Assert.False(name.OpenDirectly);
        Assert.Contains("Matched a prior name or MRN", Assert.Single(name.Hits).Detail);
    }

    [Fact]
    public async Task MergedDuplicateMrn_OpensTheSurvivingChart()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        c.Patients.Add(new Patient
        {
            MedicalRecordNumber = "MRN-DUP", LastName = "Lookup", FirstName = "Ann", DateOfBirth = new DateOnly(1970, 5, 1),
            Sex = Sex.Female, Status = PatientStatus.Merged, MergedIntoPatientId = f.Patient.Id
        });
        await c.SaveChangesAsync();

        var result = await Lookup(c).LookupAsync("MRN-DUP");

        Assert.True(result.OpenDirectly);
        var hit = Assert.Single(result.Hits);
        Assert.Equal(f.Patient.Id, hit.Id);
        Assert.Equal("MRN-LOOK1", hit.Identifier);
        Assert.Contains("was merged into MRN MRN-LOOK1", hit.Detail);
    }

    // TEST-BB-437: a patient hit carries the current type and alerts; a merged duplicate shows the survivor's.
    [Fact]
    public async Task PatientHit_CarriesTypeAndAlerts_FromTheSurvivingChart()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var now = _factory.Clock.UtcNow;
        c.Patients.Add(new Patient
        {
            MedicalRecordNumber = "MRN-DUP2", LastName = "Lookup", FirstName = "Ann", DateOfBirth = new DateOnly(1970, 5, 1),
            Sex = Sex.Female, Status = PatientStatus.Merged, MergedIntoPatientId = f.Patient.Id
        });
        c.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory { PatientId = f.Patient.Id, Abo = AboGroup.AB, RhD = RhType.Negative, IsCurrent = true });
        c.AntibodyHistory.AddRange(
            new AntibodyHistory { PatientId = f.Patient.Id, AntibodySpecificity = "anti-Jka", IsActive = true },
            new AntibodyHistory { PatientId = f.Patient.Id, AntibodySpecificity = "anti-S", IsActive = false });
        c.SpecialTransfusionRequirements.AddRange(
            new SpecialTransfusionRequirement { PatientId = f.Patient.Id, RequirementType = SpecialTransfusionRequirementType.Irradiated, Reason = "In effect", EffectiveUtc = now.AddDays(-1) },
            new SpecialTransfusionRequirement { PatientId = f.Patient.Id, RequirementType = SpecialTransfusionRequirementType.Irradiated, Reason = "Ended", EffectiveUtc = now.AddDays(-5), ExpiresUtc = now.AddDays(-1) });
        await c.SaveChangesAsync();

        var flagged = new UniversalLookupService(new EfRepository<Patient>(c), new EfRepository<PatientIdentifier>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), new EfRepository<Specimen>(c), new EfRepository<Order>(c),
            new EfRepository<PatientBloodTypeHistory>(c), new EfRepository<AntibodyHistory>(c),
            new EfRepository<SpecialTransfusionRequirement>(c), _factory.Clock,
            new EfRepository<BloodBankLIS.Domain.Entities.Configuration.SpecialRequirementDefinition>(c));

        foreach (var query in new[] { "MRN-LOOK1", "MRN-DUP2" })
        {
            var hit = Assert.Single((await flagged.LookupAsync(query)).Hits, h => h.Kind == LookupHitKind.Patient);
            Assert.Equal(f.Patient.Id, hit.Id);
            Assert.Equal("AB-", hit.BloodType);
            Assert.Equal(new[] { "anti-Jka" }, hit.Antibodies);
            Assert.Equal(1, hit.RequirementsInEffect);
            Assert.Equal(0, hit.InformationalNotes);
        }

        // Without the history repositories (older wiring) the hit simply carries no flags.
        var plain = Assert.Single((await Lookup(c).LookupAsync("MRN-LOOK1")).Hits, h => h.Kind == LookupHitKind.Patient);
        Assert.Null(plain.BloodType);
        Assert.Equal(0, plain.RequirementsInEffect);
    }

    [Fact]
    public async Task PartialName_ListsBothPatients_AndDoesNotOpen()
    {
        await using var c = _factory.Create();
        await SeedAsync(c);
        c.Patients.Add(new Patient
        {
            MedicalRecordNumber = "MRN-LOOK2", LastName = "Lookup", FirstName = "Bea", DateOfBirth = new DateOnly(1980, 2, 2), Sex = Sex.Female
        });
        await c.SaveChangesAsync();

        var result = await Lookup(c).LookupAsync("lookup");

        Assert.False(result.OpenDirectly);
        Assert.Equal(2, result.Hits.Count(h => h.Kind == LookupHitKind.Patient));
        Assert.All(result.Hits, h => Assert.False(h.Exact));
    }

    [Fact]
    public async Task OnePartialMatch_StillDoesNotOpen()
    {
        await using var c = _factory.Create();
        await SeedAsync(c);

        var result = await Lookup(c).LookupAsync("Lookup, Ann");

        var hit = Assert.Single(result.Hits);
        Assert.False(hit.Exact);
        Assert.False(result.OpenDirectly);
    }

    [Fact]
    public async Task ExactMatch_HidesPartialMatches()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        c.Patients.Add(new Patient
        {
            MedicalRecordNumber = "MRN-LOOK10", LastName = "Other", FirstName = "Cal", DateOfBirth = new DateOnly(1990, 3, 3), Sex = Sex.Male
        });
        await c.SaveChangesAsync();

        var result = await Lookup(c).LookupAsync("MRN-LOOK1");

        Assert.True(result.OpenDirectly);
        Assert.Equal(f.Patient.Id, Assert.Single(result.Hits).Id);
    }

    [Fact]
    public async Task ManyPartialMatches_AreCapped_AndSaySo()
    {
        await using var c = _factory.Create();
        await SeedAsync(c);
        for (var i = 0; i < UniversalLookupService.PartialLimit + 3; i++)
        {
            c.Patients.Add(new Patient
            {
                MedicalRecordNumber = $"BULK-{i:000}", LastName = "Bulkname", FirstName = $"P{i}", DateOfBirth = new DateOnly(1990, 1, 1), Sex = Sex.Male
            });
        }

        await c.SaveChangesAsync();

        var result = await Lookup(c).LookupAsync("bulkname");

        Assert.True(result.Truncated);
        Assert.Equal(UniversalLookupService.PartialLimit, result.Hits.Count);
        Assert.Contains("narrow", result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task BlankInput_ReturnsNothing_WithoutError(string? query)
    {
        await using var c = _factory.Create();
        await SeedAsync(c);

        var result = await Lookup(c).LookupAsync(query);

        Assert.Empty(result.Hits);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task UnknownInput_SaysNothingMatches()
    {
        await using var c = _factory.Create();
        await SeedAsync(c);

        var result = await Lookup(c).LookupAsync("ZZZ-NOPE");

        Assert.Empty(result.Hits);
        Assert.Equal("No patient, unit, specimen, order, or visit matches \"ZZZ-NOPE\".", result.Message);
    }

    private sealed record Fixture(Patient Patient, BloodUnit Unit, ProductType Product);

    private async Task<Fixture> SeedAsync(BloodBankDbContext c)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-LOOK1", LastName = "Lookup", FirstName = "Ann", DateOfBirth = new DateOnly(1970, 5, 1), Sex = Sex.Female
        };
        var location = new OrderingLocation { Code = "LOOK-WARD", Name = "Ward 7", IsActive = true };
        var product = new ProductType { ProductCode = "RBC-LOOK", Name = "Red cells", ComponentClass = ComponentClass.RedBloodCells };
        c.AddRange(patient, location, product);
        await c.SaveChangesAsync();

        var encounter = new Encounter
        {
            PatientId = patient.Id, VisitNumber = "V-LOOK", EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active, AdmitUtc = _factory.Clock.UtcNow.AddDays(-1)
        };
        c.Encounters.Add(encounter);
        var unit = new BloodUnit
        {
            UnitNumber = $"{Din}-A", Din = Din, ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive,
            Status = UnitStatus.Available, ExpiresUtc = _factory.Clock.UtcNow.AddDays(10)
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        c.Specimens.Add(new Specimen
        {
            AccessionNumber = "ACC-LOOK1", PatientId = patient.Id, EncounterId = encounter.Id, SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddHours(-2), Status = SpecimenStatus.Accepted
        });
        c.Orders.Add(new Order
        {
            OrderNumber = "ORD-LOOK1", PatientId = patient.Id, EncounterId = encounter.Id, OrderingLocationId = location.Id,
            OrderCategory = OrderCategory.Test, OrderName = "Type and screen", OrderType = OrderType.TypeAndScreen
        });
        await c.SaveChangesAsync();
        return new Fixture(patient, unit, product);
    }
}
