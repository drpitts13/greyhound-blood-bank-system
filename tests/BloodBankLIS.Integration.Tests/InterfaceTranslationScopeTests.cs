using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

// TEST-BB-525: translations are saved for all interfaces or as one interface's overrides, with a reason; a local value
// must be one the LIS has (tests and groupers, visit types, race…); "check a value" shows the translation and its
// source; unmapped values are counted, resolved by a saved translation, or dismissed with a reason.
public class InterfaceTranslationScopeTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private InterfaceUnmappedValueService Unmapped(BloodBankDbContext c) =>
        new(new EfRepository<InterfaceUnmappedValue>(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser), new EfRepository<InterfaceEndpoint>(c));

    private InterfaceTranslationAdminService Service(BloodBankDbContext c)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new InterfaceTranslationAdminService(
            new InterfaceValueTranslationRepository(c), c, _factory.Clock, _factory.CurrentUser,
            new AuditWriter(c, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(c, _factory.Clock, _factory.CurrentUser, env),
            valueSets: new InterfaceValueSets(new EfRepository<TestDefinition>(c), new EfRepository<TestGrouper>(c)),
            unmapped: Unmapped(c),
            endpoints: new EfRepository<InterfaceEndpoint>(c));
    }

    private static async Task<InterfaceEndpoint> SeedAsync(BloodBankDbContext c)
    {
        c.TestDefinitions.AddRange(
            new TestDefinition { Code = "ABORH", Name = "ABO/Rh", IsActive = true, IsDraft = false },
            new TestDefinition { Code = "ABSC", Name = "Antibody screen", IsActive = true, IsDraft = false });
        c.TestGroupers.Add(new TestGrouper { Code = "TNS", Name = "Type and screen", IsActive = true, IsDraft = false, MemberTestsJson = "[]" });
        var endpoint = new InterfaceEndpoint { Name = "Second EHR", InterfaceType = InterfaceType.Orders, Direction = Hl7Direction.Inbound, IsEnabled = true };
        c.InterfaceEndpoints.Add(endpoint);
        await c.SaveChangesAsync();
        return endpoint;
    }

    private static SaveInterfaceTranslationsRequest Request(string? reason, long? endpointId, params (string Local, string External, InterfaceTranslationDirection Direction)[] rows) =>
        new(rows.Select(r => new InterfaceValueTranslationDto(r.Local, r.External, r.Direction)).ToList(), reason, endpointId);

    [Fact]
    public async Task GlobalAndInterfaceRows_AreSavedApart_WithAReason_AndCheckShowsTheSource()
    {
        await using var c = _factory.Create();
        var endpoint = await SeedAsync(c);
        var service = Service(c);

        Assert.False((await service.ReplaceAsync(InterfaceDataItemKeys.ProcedureCode,
            Request(null, null, ("TNS", "LAB10117", InterfaceTranslationDirection.Both)))).Succeeded);

        var global = await service.ReplaceAsync(InterfaceDataItemKeys.ProcedureCode,
            Request("Epic procedure codes", null, ("tns", "LAB10117", InterfaceTranslationDirection.Both), ("ABORH", "LAB10117", InterfaceTranslationDirection.Inbound)));
        Assert.True(global.Succeeded, global.Error);
        Assert.Contains(global.Value!.Rows, r => r.InternalValue == "TNS");
        Assert.Contains(global.Value.LocalValues!, v => v.Value == "TNS" && v.Kind == "Grouper");

        var overrideRows = await service.ReplaceAsync(InterfaceDataItemKeys.ProcedureCode,
            Request("Second EHR codes", endpoint.Id, ("TNS", "TS-2", InterfaceTranslationDirection.Both)));
        Assert.True(overrideRows.Succeeded, overrideRows.Error);
        Assert.Single(overrideRows.Value!.Rows);
        Assert.Equal(2, overrideRows.Value.GlobalRows!.Count);
        Assert.Equal(3, await c.InterfaceValueTranslations.CountAsync());

        var inbound = await service.CheckAsync(InterfaceDataItemKeys.OrderTestCode, "LAB10117", inbound: true, endpointId: null);
        Assert.Equal(("TNS", InterfaceDataItemKeys.ProcedureCode), (inbound.Output, inbound.FromKey));
        Assert.Contains("Grouper TNS", inbound.Note);
        var overridden = await service.CheckAsync(InterfaceDataItemKeys.ResultObrTestCode, "TNS", inbound: false, endpointId: endpoint.Id);
        Assert.Equal("TS-2", overridden.Output);
        Assert.True(overridden.FromInterfaceOverride);
        var unknown = await service.CheckAsync(InterfaceDataItemKeys.OrderTestCode, "LAB99999", inbound: true, endpointId: null);
        Assert.False(unknown.Translated);
        Assert.Contains("interface error queue", unknown.Note);

        var fields = await service.ListFieldsAsync();
        Assert.Contains(fields, f => f.Key == InterfaceDataItemKeys.ProcedureCode && f.RowCount == 3 && f.Category == "Tests and procedures");
        Assert.Contains(fields, f => f.Key == InterfaceDataItemKeys.EncounterPatientClass && f.HasValueList);
    }

    [Fact]
    public async Task LocalValues_MustBeOnesTheLisHas()
    {
        await using var c = _factory.Create();
        await SeedAsync(c);
        var service = Service(c);

        var badTest = await service.ReplaceAsync(InterfaceDataItemKeys.ProcedureCode,
            Request("codes", null, ("NOPE", "LAB1", InterfaceTranslationDirection.Both)));
        Assert.False(badTest.Succeeded);
        Assert.Contains("active test or test grouper", badTest.Error);

        var twoTests = await service.ReplaceAsync(InterfaceDataItemKeys.ProcedureCode,
            Request("codes", null, ("ABORH", "LAB1", InterfaceTranslationDirection.Inbound), ("ABSC", "LAB1", InterfaceTranslationDirection.Inbound)));
        Assert.False(twoTests.Succeeded);

        Assert.False((await service.ReplaceAsync(InterfaceDataItemKeys.EncounterPatientClass,
            Request("classes", null, ("Ward", "W", InterfaceTranslationDirection.Both)))).Succeeded);
        var patientClass = await service.ReplaceAsync(InterfaceDataItemKeys.EncounterPatientClass,
            Request("classes", null, ("emergency", "E", InterfaceTranslationDirection.Both)));
        Assert.True(patientClass.Succeeded, patientClass.Error);
        Assert.Equal("Emergency", Assert.Single(patientClass.Value!.Rows).InternalValue);

        Assert.False((await service.ReplaceAsync(InterfaceDataItemKeys.PatientRace,
            Request("race", null, ("CAUCASIAN", "2106-3", InterfaceTranslationDirection.Both)))).Succeeded);

        // Free-text fields take any local value.
        Assert.True((await service.ReplaceAsync(InterfaceDataItemKeys.BillingCode,
            Request("charges", null, ("BB-XM", "71020", InterfaceTranslationDirection.Outbound)))).Succeeded);
    }

    [Fact]
    public async Task UnmappedValues_AreCounted_ResolvedByATranslation_OrDismissedWithAReason()
    {
        await using var c = _factory.Create();
        await SeedAsync(c);
        var unmapped = Unmapped(c);
        TranslationMiss[] misses =
        [
            new(InterfaceDataItemKeys.OrderTestCode, "LAB10117", InterfaceTranslationDirection.Inbound),
            new(InterfaceDataItemKeys.PatientRace, "9999-9", InterfaceTranslationDirection.Inbound)
        ];
        await unmapped.RecordAsync(misses, null, "MSG-1");
        await unmapped.RecordAsync([misses[0]], null, "MSG-2");

        var open = await unmapped.ListAsync(openOnly: true);
        Assert.Equal(2, open.Count);
        var testCode = open.Single(v => v.DataItemKey == InterfaceDataItemKeys.OrderTestCode);
        Assert.Equal((2, "MSG-2"), (testCode.Count, testCode.LastMessageControlId));
        Assert.Equal(2, await unmapped.CountOpenAsync());

        var saved = await Service(c).ReplaceAsync(InterfaceDataItemKeys.ProcedureCode,
            Request("Epic procedure codes", null, ("TNS", "LAB10117", InterfaceTranslationDirection.Both)));
        Assert.True(saved.Succeeded, saved.Error);
        var resolved = (await unmapped.ListAsync(openOnly: false)).Single(v => v.Id == testCode.Id);
        Assert.Equal("Translation saved.", resolved.ResolvedNote);

        var race = open.Single(v => v.DataItemKey == InterfaceDataItemKeys.PatientRace);
        Assert.False((await unmapped.DismissAsync(race.Id, " ")).Succeeded);
        var dismissed = await unmapped.DismissAsync(race.Id, "Test message from the EHR vendor");
        Assert.True(dismissed.Succeeded, dismissed.Error);
        Assert.Equal(0, await unmapped.CountOpenAsync());
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(InterfaceUnmappedValue) && a.EntityId == race.Id));
    }
}
