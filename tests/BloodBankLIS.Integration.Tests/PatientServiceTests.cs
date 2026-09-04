using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class PatientServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public PatientServiceTests(SqliteContextFactory factory) => _factory = factory;

    private static SpecimenService Specimens(BloodBankDbContext c, SqliteContextFactory factory) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c), c, factory.Clock);

    private PatientService Patients(BloodBankDbContext c, IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<Patient>(c), c, _factory.Clock, Specimens(c, _factory),
            currentUser: _factory.CurrentUser, permissions: permissions);

    private async Task<long> EnsurePatientAsync(string mrn)
    {
        await using var context = _factory.Create();
        var existing = await context.Patients.FirstOrDefaultAsync(p => p.MedicalRecordNumber == mrn);
        if (existing is not null)
        {
            return existing.Id;
        }

        var patient = new Patient
        {
            MedicalRecordNumber = mrn,
            LastName = "Original",
            FirstName = "Pat",
            MiddleName = "Q",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Sex = Sex.Unknown,
            Status = PatientStatus.Active
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        return patient.Id;
    }

    [Fact]
    public async Task Update_WritesDemographics_AndLeavesMrnUnchanged()
    {
        var id = await EnsurePatientAsync("MRN-DEMO-EDIT");

        await using var context = _factory.Create();
        var result = await Patients(context).UpdateAsync(id, new UpdatePatientRequest(
            "Revised", "Alex", "M", new DateOnly(1975, 6, 15), Sex.Female, PatientStatus.Inactive));

        Assert.True(result.Succeeded);
        Assert.Equal("Revised", result.Value!.LastName);
        Assert.Equal("Alex", result.Value.FirstName);
        Assert.Equal("M", result.Value.MiddleName);
        Assert.Equal(new DateOnly(1975, 6, 15), result.Value.DateOfBirth);
        Assert.Equal(Sex.Female, result.Value.Sex);
        Assert.Equal(PatientStatus.Inactive, result.Value.Status);
        Assert.Equal("MRN-DEMO-EDIT", result.Value.MedicalRecordNumber);
    }

    [Fact]
    public async Task Update_WithoutPatientWrite_IsHardStopped()
    {
        var id = await EnsurePatientAsync("MRN-DEMO-PERM");

        await using var context = _factory.Create();
        var denied = await Patients(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientMerge))
            .UpdateAsync(id, new UpdatePatientRequest(
                "Hijacked", "Alex", null, new DateOnly(1975, 6, 15), Sex.Female, PatientStatus.Active));
        Assert.False(denied.Succeeded);
        Assert.Equal(PatientAuthorizationRule.EvaluateWrite(false).Message, denied.Error);
        Assert.Equal("Original", (await context.Patients.FindAsync(id))!.LastName);

        var allowed = await Patients(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .UpdateAsync(id, new UpdatePatientRequest(
                "Hijacked", "Alex", null, new DateOnly(1975, 6, 15), Sex.Female, PatientStatus.Active));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("Hijacked", allowed.Value!.LastName);
    }

    [Fact]
    public async Task Create_WithoutPatientWrite_IsHardStopped()
    {
        await using var context = _factory.Create();
        var denied = await Patients(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientMerge))
            .CreateAsync(new CreatePatientRequest("MRN-CREATE-PERM", "New", "Pat", null, new DateOnly(1980, 1, 1), Sex.Unknown));
        Assert.False(denied.Succeeded);
        Assert.Equal(PatientAuthorizationRule.EvaluateCreate(false).Message, denied.Error);
        Assert.False(await context.Patients.AnyAsync(p => p.MedicalRecordNumber == "MRN-CREATE-PERM"));

        var allowed = await Patients(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite))
            .CreateAsync(new CreatePatientRequest("MRN-CREATE-PERM", "New", "Pat", null, new DateOnly(1980, 1, 1), Sex.Unknown));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("MRN-CREATE-PERM", allowed.Value!.MedicalRecordNumber);
        Assert.Equal("New", allowed.Value.LastName);
    }

    [Fact]
    public async Task Update_BlankLastName_Fails()
    {
        var id = await EnsurePatientAsync("MRN-DEMO-BLANK");

        await using var context = _factory.Create();
        var result = await Patients(context).UpdateAsync(id, new UpdatePatientRequest(
            "  ", "Alex", null, new DateOnly(1980, 1, 1), Sex.Male, PatientStatus.Active));

        Assert.False(result.Succeeded);
        Assert.Contains("Last name", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_FutureDateOfBirth_Fails()
    {
        var id = await EnsurePatientAsync("MRN-DEMO-FUT");

        await using var context = _factory.Create();
        var result = await Patients(context).UpdateAsync(id, new UpdatePatientRequest(
            "Original", "Pat", null, DateOnly.FromDateTime(_factory.Clock.UtcNow.AddDays(1)), Sex.Unknown, PatientStatus.Active));

        Assert.False(result.Succeeded);
        Assert.Contains("future", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_MergedPatient_Fails()
    {
        var id = await EnsurePatientAsync("MRN-DEMO-MRG");
        await using (var setup = _factory.Create())
        {
            var patient = await setup.Patients.FindAsync(id);
            patient!.Status = PatientStatus.Merged;
            await setup.SaveChangesAsync();
        }

        await using var context = _factory.Create();
        var result = await Patients(context).UpdateAsync(id, new UpdatePatientRequest(
            "New", "Name", null, new DateOnly(1980, 1, 1), Sex.Male, PatientStatus.Active));

        Assert.False(result.Succeeded);
        Assert.Contains("merged", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_MissingPatient_Fails()
    {
        await using var context = _factory.Create();
        var result = await Patients(context).UpdateAsync(9_999_999, new UpdatePatientRequest(
            "X", "Y", null, new DateOnly(1980, 1, 1), Sex.Unknown, PatientStatus.Active));

        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
