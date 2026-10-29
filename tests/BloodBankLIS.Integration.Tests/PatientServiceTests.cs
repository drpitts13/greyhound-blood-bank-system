using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class PatientServiceTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public PatientServiceTests(SqliteContextFactory factory) => _factory = factory;

    private static SpecimenService Specimens(BloodBankDbContext c, SqliteContextFactory factory) =>
        new(new EfRepository<Specimen>(c), new EfRepository<Patient>(c), new EfRepository<SpecimenTypeDefinition>(c), c, factory.Clock);

    private PatientService Patients(BloodBankDbContext c, IPermissionEvaluator? permissions = null, bool withAudit = false) =>
        new(new EfRepository<Patient>(c), c, _factory.Clock, Specimens(c, _factory),
            audit: withAudit ? new AuditWriter(c, _factory.Clock, _factory.CurrentUser) : null,
            currentUser: _factory.CurrentUser, permissions: permissions,
            identifiers: new EfRepository<PatientIdentifier>(c));

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
            "Revised", "Alex", "M", new DateOnly(1975, 6, 15), Sex.Female, PatientStatus.Inactive,
            NameChangeReason: "Legal name correction"));

        Assert.True(result.Succeeded);
        Assert.Equal("Revised", result.Value!.LastName);
        Assert.Equal("Alex", result.Value.FirstName);
        Assert.Equal("M", result.Value.MiddleName);
        Assert.Equal(new DateOnly(1975, 6, 15), result.Value.DateOfBirth);
        Assert.Equal(Sex.Female, result.Value.Sex);
        Assert.Equal(PatientStatus.Inactive, result.Value.Status);
        Assert.Equal("MRN-DEMO-EDIT", result.Value.MedicalRecordNumber);
        var alias = Assert.Single(await context.PatientIdentifiers.Where(i =>
            i.PatientId == id && i.IdentifierType == IdentityTokenType.PriorFullName).ToListAsync());
        Assert.Equal("Original, Pat Q", alias.Value);
    }

    [Fact]
    public async Task ListAliases_RunsAsADatabaseQuery_AndReturnsPriorNames()
    {
        var id = await EnsurePatientAsync("MRN-DEMO-ALIAS");
        await using (var renaming = _factory.Create())
        {
            var renamed = await Patients(renaming).UpdateAsync(id, new UpdatePatientRequest(
                "Renamed", "Alex", null, new DateOnly(1975, 6, 15), Sex.Female, PatientStatus.Active,
                NameChangeReason: "Legal name correction"));
            Assert.True(renamed.Succeeded, renamed.Error);
        }

        // Opening a patient chart lists aliases; this query must translate to SQL.
        await using var context = _factory.Create();
        var aliases = await Patients(context).ListAliasesAsync(id);

        Assert.Contains(aliases, a => a.IdentifierType == IdentityTokenType.PriorFullName);
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
                "Hijacked", "Alex", null, new DateOnly(1975, 6, 15), Sex.Female, PatientStatus.Active,
                NameChangeReason: "Permission path name change"));
        Assert.True(allowed.Succeeded, allowed.Error);
        Assert.Equal("Hijacked", allowed.Value!.LastName);
    }

    [Fact]
    public async Task Update_NameChangeWithoutReason_IsHardStopped()
    {
        var id = await EnsurePatientAsync("MRN-DEMO-ALIAS-REASON");
        await using var context = _factory.Create();
        var denied = await Patients(context).UpdateAsync(id, new UpdatePatientRequest(
            "Changed", "Pat", null, new DateOnly(1980, 1, 1), Sex.Unknown, PatientStatus.Active));
        Assert.False(denied.Succeeded);
        Assert.Equal(
            PatientHistoricalAliasRule.EvaluateNameChangeReason(true, null).Message,
            denied.Error);
        Assert.Equal("Original", (await context.Patients.FindAsync(id))!.LastName);
        Assert.False(await context.PatientIdentifiers.AnyAsync(i => i.PatientId == id));
    }

    [Fact]
    public async Task ListDtos_SearchMatchesPriorNameAlias()
    {
        var id = await EnsurePatientAsync("MRN-DEMO-ALIAS-SEARCH");
        await using var context = _factory.Create();
        var updated = await Patients(context).UpdateAsync(id, new UpdatePatientRequest(
            "Newman", "Pat", null, new DateOnly(1980, 1, 1), Sex.Unknown, PatientStatus.Active,
            NameChangeReason: "Marriage"));
        Assert.True(updated.Succeeded, updated.Error);

        var dto = Assert.Single((await Patients(context).ListDtosAsync()).Where(p => p.Id == id));
        Assert.Contains(dto.Aliases!, a => a.Value == "Original, Pat Q");
        Assert.True(dto.MatchesSearch("Original"));
        Assert.True(dto.MatchesSearch("Newman"));
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
            "Original", "Pat", "Q", DateOnly.FromDateTime(_factory.Clock.UtcNow.AddDays(1)), Sex.Unknown, PatientStatus.Active));

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

    [Fact]
    public async Task Update_WritesPatientAccess()
    {
        var mrn = $"MRN-ACC-{Guid.NewGuid():N}"[..20];
        var id = await EnsurePatientAsync(mrn);

        await using var context = _factory.Create();
        var result = await Patients(context, withAudit: true).UpdateAsync(id, new UpdatePatientRequest(
            "Access", "Pat", null, new DateOnly(1981, 2, 2), Sex.Male, PatientStatus.Active,
            NameChangeReason: "Audit path name change"));
        Assert.True(result.Succeeded, result.Error);

        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(Patient)
            && a.EntityId == id
            && a.EventType == AuditEventType.PatientAccess
            && a.Reason == "Patient name changed: Audit path name change"));
    }

    [Fact]
    public async Task Create_WritesPatientAccess()
    {
        var mrn = $"MRN-NEW-{Guid.NewGuid():N}"[..20];
        await using var context = _factory.Create();
        var result = await Patients(context, withAudit: true).CreateAsync(
            new CreatePatientRequest(mrn, "Created", "Pat", null, new DateOnly(1982, 3, 3), Sex.Unknown));
        Assert.True(result.Succeeded, result.Error);

        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(Patient)
            && a.EntityId == result.Value!.Id
            && a.EventType == AuditEventType.PatientAccess
            && a.Reason == "Patient created."));
    }
}
