using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;

namespace BloodBankLIS.Integration.Tests;

public class SpecimenWindowTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public SpecimenWindowTests(SqliteContextFactory factory) => _factory = factory;

    // TEST-BB-1337 (end-user testing pass 310): the patient's specimen window names a transfusion within the look-back (not
    // only a pregnancy); one older than the look-back, or none, gives no risk.
    [Fact]
    public async Task Window_NamesARecentTransfusion()
    {
        await using var c = _factory.Create();
        var now = _factory.Clock.UtcNow;
        var transfused = new Patient { MedicalRecordNumber = $"MRN-WIN-T-{Guid.NewGuid():N}"[..18], LastName = "Window", FirstName = "Transfused", DateOfBirth = new DateOnly(1970, 1, 1) };
        var old = new Patient { MedicalRecordNumber = $"MRN-WIN-O-{Guid.NewGuid():N}"[..18], LastName = "Window", FirstName = "Old", DateOfBirth = new DateOnly(1970, 1, 1) };
        c.Patients.AddRange(transfused, old);
        var product = new ProductType { ProductCode = $"RBC-WIN-{Guid.NewGuid():N}"[..14], Name = "Red cells", ComponentClass = ComponentClass.RedBloodCells };
        c.ProductTypes.Add(product);
        await c.SaveChangesAsync();
        foreach (var (patient, daysAgo) in new[] { (transfused, 10), (old, 200) })
        {
            var unit = new BloodUnit { UnitNumber = $"W-WIN-{Guid.NewGuid():N}"[..14], ProductTypeId = product.Id, Abo = AboGroup.O, RhD = RhType.Positive, ExpiresUtc = now.AddDays(5), Status = UnitStatus.Transfused };
            c.BloodUnits.Add(unit);
            await c.SaveChangesAsync();
            var issue = new Issue { BloodProductId = unit.Id, PatientId = patient.Id, IssuedUtc = now.AddDays(-daysAgo), IssuedBy = "tech", Status = IssueStatus.Transfused };
            c.Issues.Add(issue);
            await c.SaveChangesAsync();
            c.TransfusionEvents.Add(new TransfusionEvent { IssueId = issue.Id, BloodProductId = unit.Id, PatientId = patient.Id, StartUtc = now.AddDays(-daysAgo), DocumentedBy = "rn" });
            await c.SaveChangesAsync();
        }

        var service = new SpecimenService(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c), c,
            _factory.Clock, transfusions: new EfRepository<TransfusionEvent>(c));
        var window = await service.GetSpecimenWindowAsync(transfused.Id);
        Assert.NotNull(window);
        Assert.True(window!.AlloimmunizationRisk);
        Assert.Equal(now.AddDays(-10), window.LastTransfusionUtc);
        Assert.Null(window.RecentPregnancyUtc);
        Assert.Equal((SpecimenValidityPolicy.DefaultAlloimmunizationRiskHours, SpecimenValidityPolicy.DefaultLookbackDays), (window.AlloimmunizationRiskHours, window.LookbackDays));

        Assert.False((await service.GetSpecimenWindowAsync(old.Id))!.AlloimmunizationRisk);
        Assert.Null(await service.GetSpecimenWindowAsync(-1));
    }
}
