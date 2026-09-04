using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class EncounterServiceAuthorizationTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public EncounterServiceAuthorizationTests(SqliteContextFactory factory) => _factory = factory;

    private EncounterService Encounters(BloodBankDbContext c, IPermissionEvaluator? permissions = null)
    {
        var providers = new OrderingProviderService(new EfRepository<OrderingProvider>(c), c);
        return new EncounterService(
            new EfRepository<Encounter>(c),
            new EfRepository<Patient>(c),
            new EfRepository<OrderingProvider>(c),
            providers,
            c,
            new FixedClock(DateTime.UtcNow),
            permissions,
            _factory.CurrentUser);
    }

    private async Task<Patient> SeedPatientAsync(BloodBankDbContext c)
    {
        var patient = new Patient
        {
            MedicalRecordNumber = $"MRN-{Guid.NewGuid():N}",
            LastName = "Visit",
            FirstName = "Auth",
            DateOfBirth = new DateOnly(1988, 4, 2),
            Sex = Sex.Male
        };
        c.Patients.Add(patient);
        await c.SaveChangesAsync();
        return patient;
    }

    [Fact]
    public async Task Create_WithoutPatientWrite_IsRejected()
    {
        await using var c = _factory.Create();
        var patient = await SeedPatientAsync(c);
        var request = new CreateEncounterRequest(
            $"VIS-{Guid.NewGuid():N}"[..16],
            EncounterType.Inpatient,
            EncounterStatus.Active,
            DateTime.UtcNow, null, null, null, "4W", "4W", null, null, null, null);

        var denied = await Encounters(c, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .CreateAsync(patient.Id, request);
        Assert.False(denied.Succeeded);
        Assert.Contains("patient.write", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await c.Encounters.AnyAsync(e => e.PatientId == patient.Id));

        var allowed = await Encounters(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .CreateAsync(patient.Id, request);
        Assert.True(allowed.Succeeded);
        Assert.Equal(request.VisitNumber, allowed.Value!.VisitNumber);
    }

    [Fact]
    public async Task Update_WithoutPatientWrite_IsRejected()
    {
        await using var c = _factory.Create();
        var patient = await SeedPatientAsync(c);
        var created = await Encounters(c).CreateAsync(patient.Id, new CreateEncounterRequest(
            $"VIS-{Guid.NewGuid():N}"[..16],
            EncounterType.Inpatient,
            EncounterStatus.Active,
            DateTime.UtcNow, null, null, null, "4W", "4W", null, null, null, null));
        Assert.True(created.Succeeded);

        var denied = await Encounters(c, new FixedPermissionEvaluator(1, PermissionCodes.LookbackManage))
            .UpdateAsync(patient.Id, created.Value!.Id, new UpdateEncounterRequest(
                EncounterType.Inpatient,
                EncounterStatus.Active,
                DateTime.UtcNow, null, null, null, "ICU", "ICU", null, null));
        Assert.False(denied.Succeeded);
        Assert.Contains("patient.write", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("4W", (await c.Encounters.AsNoTracking().SingleAsync(e => e.Id == created.Value.Id)).CurrentLocation);

        var allowed = await Encounters(c, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .UpdateAsync(patient.Id, created.Value.Id, new UpdateEncounterRequest(
                EncounterType.Inpatient,
                EncounterStatus.Active,
                DateTime.UtcNow, null, null, null, "ICU", "ICU", null, null));
        Assert.True(allowed.Succeeded);
        Assert.Equal("ICU", allowed.Value!.CurrentLocation);
    }
}
