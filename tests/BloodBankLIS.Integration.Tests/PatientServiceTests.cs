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
            identifiers: new EfRepository<PatientIdentifier>(c),
            dismissals: new EfRepository<PatientDuplicateDismissal>(c));

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

    // TEST-BB-1060: changing the date of birth on the chart needs a reason and alerts the blood bank, naming the current
    // tube; the audit keeps the change and reason (end-user testing pass 43: only a name change asked for a reason).
    [Fact]
    public async Task Update_DateOfBirthChange_NeedsAReason_AndIsAlerted()
    {
        var id = await EnsurePatientAsync("MRN-DEMO-DOB");
        await using var context = _factory.Create();
        context.Specimens.Add(new Specimen
        {
            AccessionNumber = "ACC-DEMO-DOB", PatientId = id, SpecimenType = "EDTA", CollectedUtc = _factory.Clock.UtcNow.AddHours(-1),
            ExpiresUtc = _factory.Clock.UtcNow.AddDays(2), Status = SpecimenStatus.Accepted
        });
        await context.SaveChangesAsync();
        var current = await context.Patients.SingleAsync(p => p.Id == id);
        var alerts = new BloodBankLIS.Application.Compliance.AlertService(new EfRepository<Alert>(context), context, _factory.Clock,
            _factory.CurrentUser, new AuditWriter(context, _factory.Clock, _factory.CurrentUser));
        var service = new PatientService(new EfRepository<Patient>(context), context, _factory.Clock, Specimens(context, _factory),
            audit: new AuditWriter(context, _factory.Clock, _factory.CurrentUser), currentUser: _factory.CurrentUser,
            identifiers: new EfRepository<PatientIdentifier>(context), alerts: alerts, specimenRows: new EfRepository<Specimen>(context));

        var newDob = current.DateOfBirth.AddDays(9);
        var refused = await service.UpdateAsync(id, new UpdatePatientRequest(
            current.LastName, current.FirstName, current.MiddleName, newDob, current.Sex, current.Status));
        Assert.Contains("date of birth or sex", refused.Error);

        var changed = await service.UpdateAsync(id, new UpdatePatientRequest(
            current.LastName, current.FirstName, current.MiddleName, newDob, current.Sex, current.Status,
            NameChangeReason: "Typo at registration"));
        Assert.True(changed.Succeeded, changed.Error);
        var alert = Assert.Single(await context.Alerts.Where(a => a.Source == AlertRule.PatientIdentityChanged && a.Link == $"/patients/{id}").ToListAsync());
        Assert.Contains("changed on the chart", alert.Title);
        Assert.Contains("(reason: Typo at registration)", alert.Message);
        Assert.Contains("ACC-DEMO-DOB", alert.Message);
    }

    // TEST-BB-1063: each of two charts with the same name and date of birth names the other as a possible duplicate,
    // with its blood bank flags; a third with another birthday names none (end-user testing pass 47).
    [Fact]
    public async Task PossibleDuplicatesOf_NamesTheOtherChart()
    {
        await using var context = _factory.Create();
        var key = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Patient New(string mrn, DateOnly dob) => new()
        {
            MedicalRecordNumber = mrn, LastName = $"Twin{key}", FirstName = "Casey", DateOfBirth = dob, Sex = Sex.Unknown, Status = PatientStatus.Active
        };
        var first = New($"MRN-DUPA-{key}", new DateOnly(1960, 2, 3));
        var second = New($"MRN-DUPB-{key}", new DateOnly(1960, 2, 3));
        var other = New($"MRN-DUPC-{key}", new DateOnly(1961, 2, 3));
        context.Patients.AddRange(first, second, other);
        await context.SaveChangesAsync();

        var service = Patients(context);
        var ofSecond = Assert.Single(await service.PossibleDuplicatesOfAsync(second.Id));
        Assert.Equal(first.Id, ofSecond.Other.Id);
        Assert.False(string.IsNullOrWhiteSpace(ofSecond.Reason));
        Assert.Equal(second.Id, Assert.Single(await service.PossibleDuplicatesOfAsync(first.Id)).Other.Id);
        Assert.Empty(await service.PossibleDuplicatesOfAsync(other.Id));
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

    // TEST-BB-466: registering someone who matches an active patient (same birthday, same last name and initial) is
    // refused until that match is confirmed as a different person; the confirmation is audited. A merged chart is not
    // a match, and listing matches needs the registration permission.
    [Fact]
    public async Task Create_PossibleDuplicate_IsRefusedUntilConfirmed_AndTheConfirmationIsAudited()
    {
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var dob = new DateOnly(1931, 2, 3).AddDays(Random.Shared.Next(0, 3000));
        var last = $"Dup{key}";
        await using var context = _factory.Create();
        var existing = new Patient { MedicalRecordNumber = $"MRN-DUPA-{key}", LastName = last, FirstName = "Robert", DateOfBirth = dob };
        var merged = new Patient { MedicalRecordNumber = $"MRN-DUPM-{key}", LastName = last, FirstName = "Rob", DateOfBirth = dob, Status = PatientStatus.Merged };
        context.Patients.AddRange(existing, merged);
        await context.SaveChangesAsync();
        var service = Patients(context, withAudit: true);

        var listed = await service.ListPossibleDuplicatesAsync(last.ToUpperInvariant(), "Rob", dob);
        Assert.True(listed.Succeeded, listed.Error);
        Assert.Equal(existing.Id, Assert.Single(listed.Value!).Id);

        var request = new CreatePatientRequest($"MRN-DUPB-{key}", last, "Rob", null, dob, Sex.Male);
        var refused = await service.CreateAsync(request);
        Assert.False(refused.Succeeded);
        Assert.Contains($"MRN {existing.MedicalRecordNumber}", refused.Error);
        Assert.DoesNotContain(merged.MedicalRecordNumber, refused.Error);
        Assert.False(await context.Patients.AnyAsync(p => p.MedicalRecordNumber == request.MedicalRecordNumber));

        // Confirming some other MRN is not confirming this one.
        Assert.False((await service.CreateAsync(request with { ConfirmedDifferentFromMrns = ["MRN-OTHER"] })).Succeeded);

        var created = await service.CreateAsync(request with { ConfirmedDifferentFromMrns = [existing.MedicalRecordNumber.ToLowerInvariant()] });
        Assert.True(created.Succeeded, created.Error);
        var audit = await context.AuditEvents.SingleAsync(a => a.EntityType == nameof(Patient) && a.EntityId == created.Value!.Id && a.Reason!.StartsWith("Patient created"));
        Assert.Contains(existing.MedicalRecordNumber, audit.Reason);
        Assert.Contains(existing.MedicalRecordNumber, audit.NewValueJson);

        // A different first initial on the same birthday is not a match.
        Assert.True((await service.CreateAsync(new CreatePatientRequest($"MRN-DUPC-{key}", last, "Susan", null, dob, Sex.Female))).Succeeded);

        var denied = await Patients(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientMerge)).ListPossibleDuplicatesAsync(last, "Rob", dob);
        Assert.False(denied.Succeeded);
    }

    // TEST-BB-467: Possible duplicates pairs active charts by the registration rule (an ADT-style upper-case chart
    // included), older registration first, and leaves out merged charts and different first initials; it needs
    // patient.merge.
    [Fact]
    public async Task PossibleDuplicatePairs_ListActiveMatches_OlderFirst()
    {
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var dob = new DateOnly(1921, 4, 5).AddDays(Random.Shared.Next(0, 3000));
        var last = $"Pair{key}";
        await using var context = _factory.Create();
        var older = new Patient { MedicalRecordNumber = $"MRN-PRA-{key}", LastName = last, FirstName = "Jean", DateOfBirth = dob };
        context.Patients.Add(older);
        await context.SaveChangesAsync();
        var adt = new Patient { MedicalRecordNumber = $"MRN-PRB-{key}", LastName = last.ToUpperInvariant(), FirstName = "JEANNE", DateOfBirth = dob };
        var merged = new Patient { MedicalRecordNumber = $"MRN-PRM-{key}", LastName = last, FirstName = "Jean", DateOfBirth = dob, Status = PatientStatus.Merged };
        var other = new Patient { MedicalRecordNumber = $"MRN-PRO-{key}", LastName = last, FirstName = "Mark", DateOfBirth = dob };
        context.Patients.AddRange(adt, merged, other);
        await context.SaveChangesAsync();

        var result = await Patients(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientMerge)).ListPossibleDuplicatePairsAsync();
        Assert.True(result.Succeeded, result.Error);
        var pair = Assert.Single(result.Value!, p => p.First.LastName.Equals(last, StringComparison.OrdinalIgnoreCase));
        Assert.Equal((older.Id, adt.Id), (pair.First.Id, pair.Second.Id));
        Assert.Equal(PossibleDuplicatePatientRule.SameNameAndBirthday, pair.Reason);

        var denied = await Patients(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite)).ListPossibleDuplicatePairsAsync();
        Assert.False(denied.Succeeded);
    }

    // TEST-BB-569: two active charts carrying the same external identifier are a possible-duplicate pair even with
    // different names, with the reason; the chart lists its external identifiers.
    [Fact]
    public async Task PossibleDuplicatePairs_IncludeChartsSharingAnExternalIdentifier()
    {
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var context = _factory.Create();
        var first = new Patient { MedicalRecordNumber = $"MRN-XA-{key}", LastName = $"Alpha{key}", FirstName = "Ann", DateOfBirth = new DateOnly(1950, 1, 2) };
        var second = new Patient { MedicalRecordNumber = $"MRN-XB-{key}", LastName = $"Beta{key}", FirstName = "Bea", DateOfBirth = new DateOnly(1961, 3, 4) };
        var third = new Patient { MedicalRecordNumber = $"MRN-XC-{key}", LastName = $"Gamma{key}", FirstName = "Cy", DateOfBirth = new DateOnly(1970, 5, 6) };
        context.Patients.AddRange(first, second, third);
        await context.SaveChangesAsync();
        context.PatientIdentifiers.AddRange(
            new PatientIdentifier { PatientId = first.Id, IdentifierType = IdentityTokenType.Other, Value = $"EPI-{key}", AssigningAuthority = "EPI PI" },
            new PatientIdentifier { PatientId = second.Id, IdentifierType = IdentityTokenType.Other, Value = $"EPI-{key}", AssigningAuthority = $"EPI PI, also on MRN-XA-{key}" },
            new PatientIdentifier { PatientId = third.Id, IdentifierType = IdentityTokenType.Other, Value = $"OWN-{key}", AssigningAuthority = "OTHER", IsActive = true },
            new PatientIdentifier { PatientId = third.Id, IdentifierType = IdentityTokenType.Other, Value = $"EPI-{key}", AssigningAuthority = "OLD", IsActive = false });
        await context.SaveChangesAsync();

        var result = await Patients(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientMerge)).ListPossibleDuplicatePairsAsync();
        Assert.True(result.Succeeded, result.Error);
        var pair = Assert.Single(result.Value!, p => p.First.Id == first.Id || p.Second.Id == first.Id);
        Assert.Equal((first.Id, second.Id), (pair.First.Id, pair.Second.Id));
        Assert.Equal($"Both charts carry external ID EPI-{key} (EPI PI)", pair.Reason);
        Assert.DoesNotContain(result.Value!, p => p.First.Id == third.Id || p.Second.Id == third.Id);

        var aliases = await Patients(context).ListAliasesAsync(third.Id);
        var external = Assert.Single(aliases);
        Assert.Equal("External ID (OTHER)", external.KindLabel);
    }

    // TEST-BB-572: a reviewer records that a listed pair are different people (reason required, merge permission,
    // audited); the pair leaves the report and returns when the charts come to share an identifier.
    [Fact]
    public async Task DismissPossibleDuplicate_HidesThePair_UntilNewEvidenceLinksThem()
    {
        var key = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var dob = new DateOnly(1931, 7, 8).AddDays(Random.Shared.Next(0, 3000));
        await using var context = _factory.Create();
        var first = new Patient { MedicalRecordNumber = $"MRN-DA-{key}", LastName = $"Dism{key}", FirstName = "Lee", DateOfBirth = dob };
        context.Patients.Add(first);
        await context.SaveChangesAsync();
        var second = new Patient { MedicalRecordNumber = $"MRN-DB-{key}", LastName = $"Dism{key}", FirstName = "Leo", DateOfBirth = dob };
        context.Patients.Add(second);
        await context.SaveChangesAsync();
        var mergers = Patients(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientMerge), withAudit: true);
        bool Listed(IEnumerable<PossibleDuplicatePairDto> pairs) => pairs.Any(p => p.First.Id == first.Id && p.Second.Id == second.Id);
        Assert.True(Listed((await mergers.ListPossibleDuplicatePairsAsync()).Value!));

        Assert.False((await mergers.DismissPossibleDuplicateAsync(new DismissPossibleDuplicateRequest(first.Id, second.Id, " "))).Succeeded);
        var denied = Patients(context, new FixedPermissionEvaluator(1, PermissionCodes.PatientWrite));
        Assert.False((await denied.DismissPossibleDuplicateAsync(new DismissPossibleDuplicateRequest(first.Id, second.Id, "Twins"))).Succeeded);

        var dismissed = await mergers.DismissPossibleDuplicateAsync(new DismissPossibleDuplicateRequest(second.Id, first.Id, "Siblings; different SSNs on the registration."));
        Assert.True(dismissed.Succeeded, dismissed.Error);
        Assert.False(Listed((await mergers.ListPossibleDuplicatePairsAsync()).Value!));
        Assert.True(await context.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(Patient) && a.EntityId == first.Id && a.Reason != null && a.Reason.StartsWith($"Not the same person as {second.MedicalRecordNumber}")));
        Assert.False((await mergers.DismissPossibleDuplicateAsync(new DismissPossibleDuplicateRequest(first.Id, second.Id, "Again"))).Succeeded);

        context.PatientIdentifiers.AddRange(
            new PatientIdentifier { PatientId = first.Id, IdentifierType = IdentityTokenType.Other, Value = $"EPI-D-{key}", AssigningAuthority = "EPI PI" },
            new PatientIdentifier { PatientId = second.Id, IdentifierType = IdentityTokenType.Other, Value = $"EPI-D-{key}", AssigningAuthority = $"EPI PI, also on {first.MedicalRecordNumber}" });
        await context.SaveChangesAsync();
        var back = (await mergers.ListPossibleDuplicatePairsAsync()).Value!.Single(p => p.First.Id == first.Id && p.Second.Id == second.Id);
        Assert.Contains("Both charts carry external ID", back.Reason);
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
