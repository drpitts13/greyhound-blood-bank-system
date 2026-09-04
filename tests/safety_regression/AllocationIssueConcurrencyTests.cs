using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests.SafetyRegression;

/// <summary>
/// Database uniqueness must prevent two reserved allocations or two open issues
/// for the same unit, even when application-layer checks race.
/// </summary>
public class AllocationIssueConcurrencyTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AllocationIssueConcurrencyTests(SqliteContextFactory factory) => _factory = factory;

    [Fact]
    public async Task TwoReservedAllocations_ForSameUnit_ViolateUniqueIndex()
    {
        await using var c = _factory.Create();
        var (patientA, patientB, unit) = await SeedTwoPatientsAndUnitAsync(c, "CONC-ALLOC");

        c.Allocations.Add(new Allocation
        {
            BloodProductId = unit.Id,
            PatientId = patientA.Id,
            Status = AllocationStatus.Reserved,
            AllocatedUtc = _factory.Clock.UtcNow,
            AllocatedBy = "tech-a"
        });
        c.Allocations.Add(new Allocation
        {
            BloodProductId = unit.Id,
            PatientId = patientB.Id,
            Status = AllocationStatus.Reserved,
            AllocatedUtc = _factory.Clock.UtcNow,
            AllocatedBy = "tech-b"
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => c.SaveChangesAsync());
        Assert.Contains("UNIQUE", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecondAllocate_AfterReservation_FailsClosed()
    {
        await using var c = _factory.Create();
        var (patientA, patientB, unit) = await SeedTwoPatientsAndUnitAsync(c, "CONC-ALLOC2");
        var compatibility = Compatibility(c);

        var first = await compatibility.AllocateUnitAsync(new AllocateUnitRequest(unit.Id, patientA.Id));
        Assert.True(first.Succeeded);

        var second = await compatibility.AllocateUnitAsync(new AllocateUnitRequest(unit.Id, patientB.Id));
        Assert.False(second.Succeeded);
        Assert.Contains("already has an active allocation", second.Error, StringComparison.OrdinalIgnoreCase);

        var reserved = await c.Allocations.CountAsync(a =>
            a.BloodProductId == unit.Id && a.Status == AllocationStatus.Reserved);
        Assert.Equal(1, reserved);
    }

    [Fact]
    public async Task TwoOpenIssues_ForSameUnit_ViolateUniqueIndex()
    {
        await using var c = _factory.Create();
        var (patientA, patientB, unit) = await SeedTwoPatientsAndUnitAsync(c, "CONC-ISSUE");

        c.Issues.Add(new Issue
        {
            BloodProductId = unit.Id,
            PatientId = patientA.Id,
            IssuedUtc = _factory.Clock.UtcNow,
            IssuedBy = "tech-a",
            Status = IssueStatus.Issued
        });
        c.Issues.Add(new Issue
        {
            BloodProductId = unit.Id,
            PatientId = patientB.Id,
            IssuedUtc = _factory.Clock.UtcNow,
            IssuedBy = "tech-b",
            Status = IssueStatus.Issued
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => c.SaveChangesAsync());
        Assert.Contains("UNIQUE", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReturnedIssue_AllowsSubsequentIssueRow()
    {
        await using var c = _factory.Create();
        var (patient, _, unit) = await SeedTwoPatientsAndUnitAsync(c, "CONC-RETURN");

        c.Issues.Add(new Issue
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            IssuedUtc = _factory.Clock.UtcNow,
            IssuedBy = "tech-a",
            Status = IssueStatus.Returned
        });
        await c.SaveChangesAsync();

        c.Issues.Add(new Issue
        {
            BloodProductId = unit.Id,
            PatientId = patient.Id,
            IssuedUtc = _factory.Clock.UtcNow,
            IssuedBy = "tech-b",
            Status = IssueStatus.Issued
        });
        await c.SaveChangesAsync();

        Assert.Equal(2, await c.Issues.CountAsync(i => i.BloodProductId == unit.Id));
    }

    private CompatibilityService Compatibility(BloodBankDbContext c) =>
        new(new InventoryRepository(c), new EfRepository<Crossmatch>(c), new EfRepository<Allocation>(c),
            new EfRepository<Patient>(c), new EfRepository<Specimen>(c), new EfRepository<ProductType>(c),
            new EfRepository<PatientBloodTypeHistory>(c),
            new BloodAttributeCompatLoader(
                new EfRepository<AntibodyHistory>(c),
                new EfRepository<AntigenProfile>(c),
                new EfRepository<UnitBloodAttribute>(c),
                new EfRepository<BloodAttributeDefinition>(c)),
            new AntibodyScreenCompatLoader(
                new EfRepository<TestResult>(c),
                new EfRepository<TestDefinition>(c),
                new EfRepository<AntibodyHistory>(c)),
            c, _factory.Clock, _factory.CurrentUser);

    private async Task<(Patient PatientA, Patient PatientB, BloodUnit Unit)> SeedTwoPatientsAndUnitAsync(
        BloodBankDbContext c, string key)
    {
        var patientA = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}-A",
            LastName = "Race",
            FirstName = "Alpha",
            DateOfBirth = new DateOnly(1970, 1, 1)
        };
        var patientB = new Patient
        {
            MedicalRecordNumber = $"MRN-{key}-B",
            LastName = "Race",
            FirstName = "Beta",
            DateOfBirth = new DateOnly(1971, 1, 1)
        };
        c.Patients.AddRange(patientA, patientB);

        var productType = new ProductType
        {
            ProductCode = $"RBC-{key}",
            Name = "Test RBC",
            ComponentClass = ComponentClass.RedBloodCells,
            RequiresCrossmatch = true
        };
        c.ProductTypes.Add(productType);
        await c.SaveChangesAsync();

        var unit = new BloodUnit
        {
            UnitNumber = $"U-{key}",
            ProductTypeId = productType.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(20),
            Status = UnitStatus.Available
        };
        c.BloodUnits.Add(unit);
        await c.SaveChangesAsync();

        return (patientA, patientB, unit);
    }
}
