using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-298: utilization by ordering location and provider.
public class UtilizationServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private UtilizationService Service(BloodBankDbContext c) =>
        new(new EfRepository<Crossmatch>(c),
            new EfRepository<Issue>(c),
            new EfRepository<TransfusionEvent>(c),
            new EfRepository<Order>(c),
            new EfRepository<OrderingLocation>(c),
            new EfRepository<OrderingProvider>(c),
            _factory.Clock);

    [Fact]
    public async Task GroupsByLocationAndProvider_AndCountsIssuedAndTransfused()
    {
        await using var c = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var f = await SeedAsync(c);

        // ICU / Dr Ward: four units crossmatched; one repeat crossmatch; three issued; two transfused (completed, stopped); one wasted.
        var icu = await OrderAsync(c, f, f.Icu, f.DrWard, null);
        await XmAsync(c, f, f.Units[0], icu, now.AddDays(-5));
        await XmAsync(c, f, f.Units[0], icu, now.AddDays(-4));
        await XmAsync(c, f, f.Units[1], icu, now.AddDays(-5));
        await XmAsync(c, f, f.Units[2], icu, now.AddDays(-5));
        await XmAsync(c, f, f.Units[3], icu, now.AddDays(-5));
        await TransfuseAsync(c, f, f.Units[0], TransfusionDisposition.Completed);
        await TransfuseAsync(c, f, f.Units[1], TransfusionDisposition.Stopped);
        await TransfuseAsync(c, f, f.Units[2], TransfusionDisposition.Wasted);

        // ER, free-text provider: one unit, not issued.
        var er = await OrderAsync(c, f, f.Er, null, "Dr Free Text");
        await XmAsync(c, f, f.Units[4], er, now.AddDays(-2));

        // No order: one unit, issued and transfused.
        await XmAsync(c, f, f.Units[5], null, now.AddDays(-1));
        await TransfuseAsync(c, f, f.Units[5], TransfusionDisposition.Completed);

        // Not counted: incompatible, and compatible but outside the period.
        await XmAsync(c, f, f.Units[6], icu, now.AddDays(-3), CrossmatchResult.Incompatible);
        await XmAsync(c, f, f.Units[7], icu, now.AddDays(-60));

        var report = await Service(c).GetAsync(now.AddDays(-30), now);

        Assert.Equal(6, report.Total.Crossmatched);
        Assert.Equal(4, report.Total.Issued);
        Assert.Equal(3, report.Total.Transfused);
        Assert.Equal(2.00m, report.Total.CrossmatchToTransfusionRatio);
        Assert.Equal(75.0m, report.Total.TransfusedPercentOfIssued);

        Assert.Equal(
            ["(no order)|(no provider)", "Emergency|Dr Free Text", "ICU|Dr Ward"],
            report.Rows.Select(r => $"{r.OrderingLocation}|{r.OrderingProvider}"));

        var icuRow = report.Rows.Single(r => r.OrderingLocation == "ICU");
        Assert.Equal((4, 3, 2), (icuRow.Crossmatched, icuRow.Issued, icuRow.Transfused));
        Assert.Equal(2.00m, icuRow.CrossmatchToTransfusionRatio);
        Assert.Equal(50.0m, icuRow.TransfusedPercentOfCrossmatched);

        var erRow = report.Rows.Single(r => r.OrderingLocation == "Emergency");
        Assert.Equal((1, 0, 0), (erRow.Crossmatched, erRow.Issued, erRow.Transfused));
        Assert.Null(erRow.CrossmatchToTransfusionRatio);
        Assert.Null(erRow.TransfusedPercentOfIssued);
    }

    [Fact]
    public async Task EmptyPeriod_ReturnsZeroTotals_AndSwapsReversedDates()
    {
        await using var c = _factory.Create();
        var now = _factory.Clock.UtcNow;

        var report = await Service(c).GetAsync(now, now.AddDays(-7));

        Assert.Empty(report.Rows);
        Assert.Equal(0, report.Total.Crossmatched);
        Assert.Null(report.Total.CrossmatchToTransfusionRatio);
        Assert.True(report.FromUtc < report.ToUtc);
    }

    private sealed record Fixture(
        Patient Patient,
        Encounter Encounter,
        Specimen Specimen,
        OrderingLocation Icu,
        OrderingLocation Er,
        OrderingProvider DrWard,
        List<BloodUnit> Units);

    private async Task<Fixture> SeedAsync(BloodBankDbContext c)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = "MRN-UTIL",
            LastName = "Usage",
            FirstName = "Una",
            DateOfBirth = new DateOnly(1970, 1, 1),
            Sex = Sex.Female
        };
        var icu = new OrderingLocation { Code = "ICU", Name = "ICU", IsActive = true };
        var er = new OrderingLocation { Code = "ER", Name = "Emergency", IsActive = true };
        var drWard = new OrderingProvider { ProviderId = "P-100", Name = "Dr Ward" };
        var product = new ProductType { ProductCode = "RBC-UTIL", Name = "Red cells" };
        c.AddRange(patient, icu, er, drWard, product);
        await c.SaveChangesAsync();

        var encounter = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = "V-UTIL",
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = _factory.Clock.UtcNow.AddDays(-90)
        };
        c.Encounters.Add(encounter);
        await c.SaveChangesAsync();

        var specimen = new Specimen
        {
            AccessionNumber = "ACC-UTIL",
            PatientId = patient.Id,
            EncounterId = encounter.Id,
            SpecimenType = "EDTA",
            CollectedUtc = _factory.Clock.UtcNow.AddDays(-61),
            Status = SpecimenStatus.Accepted
        };
        c.Specimens.Add(specimen);
        var units = Enumerable.Range(1, 8).Select(i => new BloodUnit
        {
            UnitNumber = $"U-UTIL-{i}",
            ProductTypeId = product.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        }).ToList();
        c.BloodUnits.AddRange(units);
        await c.SaveChangesAsync();
        return new Fixture(patient, encounter, specimen, icu, er, drWard, units);
    }

    private async Task<Order> OrderAsync(BloodBankDbContext c, Fixture f, OrderingLocation location, OrderingProvider? provider, string? providerText)
    {
        var order = new Order
        {
            OrderNumber = $"XM-{location.Code}",
            PatientId = f.Patient.Id,
            EncounterId = f.Encounter.Id,
            OrderingLocationId = location.Id,
            OrderingProviderId = provider?.Id,
            OrderingProvider = providerText,
            OrderName = "Crossmatch",
            OrderType = OrderType.Crossmatch,
            OrderCategory = OrderCategory.Test,
            Status = OrderStatus.InProcess,
            OrderedUtc = _factory.Clock.UtcNow.AddDays(-10)
        };
        c.Orders.Add(order);
        await c.SaveChangesAsync();
        return order;
    }

    private static async Task XmAsync(
        BloodBankDbContext c, Fixture f, BloodUnit unit, Order? order, DateTime performedUtc,
        CrossmatchResult result = CrossmatchResult.Compatible)
    {
        c.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = unit.Id,
            PatientId = f.Patient.Id,
            SpecimenId = f.Specimen.Id,
            Method = CrossmatchMethod.Electronic,
            Result = result,
            PerformedUtc = performedUtc,
            OrderId = order?.Id
        });
        await c.SaveChangesAsync();
    }

    private async Task TransfuseAsync(BloodBankDbContext c, Fixture f, BloodUnit unit, TransfusionDisposition disposition)
    {
        var issue = new Issue
        {
            BloodProductId = unit.Id,
            PatientId = f.Patient.Id,
            IssuedUtc = _factory.Clock.UtcNow.AddDays(-1),
            Status = IssueStatus.Issued
        };
        c.Issues.Add(issue);
        await c.SaveChangesAsync();
        c.TransfusionEvents.Add(new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unit.Id,
            PatientId = f.Patient.Id,
            StartUtc = issue.IssuedUtc.AddMinutes(30),
            FinalDisposition = disposition
        });
        await c.SaveChangesAsync();
    }
}
