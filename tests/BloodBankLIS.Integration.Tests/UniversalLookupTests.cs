using BloodBankLIS.Application.Search;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Infrastructure.Audit;
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
            encounters: new EfRepository<Encounter>(c), issues: new EfRepository<Issue>(c));

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

    // TEST-BB-505: a patient row says where the patient is now, unless the row is for that same visit.
    [Fact]
    public async Task PatientHit_SaysWhereThePatientIsNow()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var visit = c.Encounters.Single(e => e.VisitNumber == "V-LOOK");
        visit.CurrentLocation = "ICU 4";
        c.Encounters.Add(new Encounter
        {
            PatientId = f.Patient.Id, VisitNumber = "V-LOOK-OLD", EncounterType = EncounterType.Outpatient,
            Status = EncounterStatus.Discharged, AdmitUtc = _factory.Clock.UtcNow.AddDays(-30), DischargeUtc = _factory.Clock.UtcNow.AddDays(-29)
        });
        await c.SaveChangesAsync();

        Assert.Contains("Now Inpatient · ICU 4", Assert.Single((await Lookup(c).LookupAsync("MRN-LOOK1")).Hits).Detail);

        var old = Assert.Single((await Lookup(c).LookupAsync("V-LOOK-OLD")).Hits);
        Assert.Contains("Visit V-LOOK-OLD", old.Detail);
        Assert.Contains("Now Inpatient · ICU 4", old.Detail);

        var same = Assert.Single((await Lookup(c).LookupAsync("V-LOOK")).Hits);
        Assert.DoesNotContain("Now ", same.Detail);
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
        Assert.Equal("No patient, unit, specimen, order, visit, RhIG lot, shipment, or cooler in transit matches \"ZZZ-NOPE\".", result.Message);
    }

    // TEST-BB-612: a shipment reference (case and spaces ignored) finds the shipment, opening Ship units with it; a
    // shipped unit's own hit says where it went.
    [Fact]
    public async Task ShipmentReference_FindsTheShipment()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var unit = (await c.BloodUnits.FindAsync(f.Unit.Id))!;
        unit.Status = UnitStatus.ShippedOut;
        unit.ShippedToFacility = "St. Mary's Hospital";
        unit.ShippedUtc = _factory.Clock.UtcNow;
        unit.OutboundShipmentReference = "PL-2026 77";
        await c.SaveChangesAsync();

        var result = await Lookup(c).LookupAsync(" pl-202677 ");
        var hit = Assert.Single(result.Hits);
        Assert.Equal((LookupHitKind.Shipment, f.Unit.Id, "Shipment to St. Mary's Hospital: 1 unit"), (hit.Kind, hit.Id, hit.Title));
        Assert.Equal("/inventory/ship-out?shipment=PL-2026%2077", hit.Href);

        var unitHit = Assert.Single((await Lookup(c).LookupAsync(f.Unit.UnitNumber)).Hits);
        Assert.EndsWith("shipped to St. Mary's Hospital", unitHit.Detail);
    }

    // TEST-BB-659: a scanned cooler label (case and spaces ignored) finds the cooler while units issued in it are in
    // transit, counting only those not yet received on the ward and naming where they went, and opens Check a cooler with
    // it; another cooler's units do not count; once everything in it is received the cooler is not found.
    [Fact]
    public async Task CoolerLabel_FindsTheCoolerInTransit()
    {
        await using var c = _factory.Create();
        var f = await SeedAsync(c);
        var now = _factory.Clock.UtcNow;
        // One issue per unit (a unit is on one issue at a time).
        var units = Enumerable.Range(1, 4).Select(i => new BloodUnit
        {
            UnitNumber = $"W-CLR-{i}", ProductTypeId = f.Product.Id, Abo = AboGroup.O, RhD = RhType.Positive,
            ExpiresUtc = now.AddDays(20), Status = UnitStatus.Issued
        }).ToList();
        c.BloodUnits.AddRange(units);
        await c.SaveChangesAsync();
        var next = 0;
        Issue InCooler(string cooler, int minutesAgo, string to) => new()
        {
            BloodProductId = units[next++].Id, PatientId = f.Patient.Id, IssuedUtc = now.AddMinutes(-minutesAgo), IssuedBy = "tech",
            IssuedToLocation = to, CoolerId = cooler, Status = IssueStatus.Issued
        };
        var received = InCooler("CLR 12", 50, "OR 3");
        received.WardReceivedUtc = now.AddMinutes(-40);
        c.Issues.AddRange(InCooler("CLR 12", 30, "OR 3"), InCooler("CLR 12", 25, "OR 3"), received, InCooler("CLR 99", 20, "ICU 4"));
        await c.SaveChangesAsync();

        var hit = Assert.Single((await Lookup(c).LookupAsync(" clr12 ")).Hits);
        Assert.Equal((LookupHitKind.Cooler, "CLR 12", "Cooler CLR 12: 2 units in transit"), (hit.Kind, hit.Identifier, hit.Title));
        Assert.StartsWith("To OR 3 · issued ", hit.Detail);
        Assert.Equal("/issuing/cooler-check?cooler=CLR%2012", hit.Href);
        Assert.Equal("Issue", LookupRecordType.For(hit.Kind));

        foreach (var issue in c.Issues.Where(i => i.CoolerId == "CLR 12"))
        {
            issue.WardReceivedUtc ??= now;
        }

        await c.SaveChangesAsync();
        Assert.Empty((await Lookup(c).LookupAsync("CLR12")).Hits);
    }

    // TEST-BB-614: recent patients are the signed-in user's own chart openings, newest first, once each, at most eight; a
    // merged-away chart shows as the chart it was merged into; another user's openings do not count.
    [Fact]
    public async Task RecentPatients_AreTheUsersOwnLatestChartOpenings()
    {
        await using var c = _factory.Create();
        var patients = Enumerable.Range(1, 10)
            .Select(i => new Patient { MedicalRecordNumber = $"MRN-REC-{i:D2}", LastName = $"Recent{i:D2}", FirstName = "Pat", DateOfBirth = new DateOnly(1980, 1, i) })
            .ToList();
        c.Patients.AddRange(patients);
        await c.SaveChangesAsync();
        var survivor = patients[9];
        patients[8].Status = PatientStatus.Merged;
        patients[8].MergedIntoPatientId = survivor.Id;
        var t0 = _factory.Clock.UtcNow;
        void Opened(Patient p, int minutes, string user = "tech-test") => c.AuditEvents.Add(new AuditEvent
        {
            EventType = AuditEventType.PatientAccess, EntityType = nameof(Patient), EntityId = p.Id, UserName = user,
            OccurredUtc = t0.AddMinutes(minutes), Reason = "Patient record opened."
        });
        for (var i = 0; i < 8; i++)
        {
            Opened(patients[i], i);
        }

        Opened(patients[0], 20);              // opened again: first, once
        Opened(patients[8], 21);              // merged away: shows as the survivor
        Opened(survivor, 5);                  // and the survivor appears once
        Opened(patients[7], 30, "someone-else");
        await c.SaveChangesAsync();

        var lookup = new UniversalLookupService(new EfRepository<Patient>(c), new EfRepository<PatientIdentifier>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), new EfRepository<Specimen>(c), new EfRepository<Order>(c),
            auditQuery: new AuditQuery(c), currentUser: _factory.CurrentUser);
        var recent = await lookup.RecentPatientsAsync();

        Assert.Equal("Your recent patients", recent.Message);
        Assert.Equal(UniversalLookupService.RecentPatientCount, recent.Hits.Count);
        Assert.Equal([survivor.Id, patients[0].Id, patients[7].Id, patients[6].Id], recent.Hits.Take(4).Select(h => h.Id));
        Assert.Equal(recent.Hits.Count, recent.Hits.Select(h => h.Id).Distinct().Count());
        Assert.All(recent.Hits, h => Assert.Equal(LookupHitKind.Patient, h.Kind));
        Assert.Contains("Opened", recent.Hits[1].Detail);

        var anonymous = new UniversalLookupService(new EfRepository<Patient>(c), new EfRepository<PatientIdentifier>(c), new EfRepository<BloodUnit>(c),
            new EfRepository<ProductType>(c), new EfRepository<Specimen>(c), new EfRepository<Order>(c), auditQuery: new AuditQuery(c));
        Assert.Empty((await anonymous.RecentPatientsAsync()).Hits);
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
