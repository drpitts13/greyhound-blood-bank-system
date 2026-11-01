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
            new EfRepository<ProductType>(c), new EfRepository<Specimen>(c), new EfRepository<Order>(c));

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
        Assert.Equal($"/patients/{f.Patient.Id}?tab=specimens", hit.Href);
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
        Assert.Equal($"/patients/{f.Patient.Id}?tab=orders", hit.Href);
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
        Assert.Equal("No patient, unit, specimen, or order matches \"ZZZ-NOPE\".", result.Message);
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
