using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

public class AdminInterfaceSetupTests : IClassFixture<SqliteContextFactory>
{
    private readonly SqliteContextFactory _factory;

    public AdminInterfaceSetupTests(SqliteContextFactory factory) => _factory = factory;

    private Hl7ConfigAdminService Service(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new Hl7ConfigAdminService(
            new EfRepository<InterfaceEndpoint>(context),
            new InterfaceFieldMappingRepository(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env));
    }

    [Fact]
    public async Task Create_PersistsTypeVendorAndMappings()
    {
        var preset = Hl7ConfigAdminService.VendorPreset(InterfaceVendorCodes.Epic, InterfaceType.Adt, Hl7Direction.Inbound);
        Assert.NotNull(preset);

        long id;
        await using (var context = _factory.Create())
        {
            var result = await Service(context).CreateAsync(new SaveHl7EndpointRequest(
                "Epic ADT",
                InterfaceType.Adt,
                Hl7Direction.Inbound,
                InterfaceTransport.File,
                null,
                null,
                @"C:\hl7\epic-adt",
                null,
                null,
                InterfaceVendorCodes.Epic,
                InterfaceMappingMode.Vendor,
                "Test",
                preset!.Connection.SendingApplication,
                preset.Connection.SendingFacility,
                preset.Connection.ReceivingApplication,
                preset.Connection.ReceivingFacility,
                null,
                null,
                null,
                null,
                true,
                preset.Mappings,
                "Initial Epic ADT setup"));

            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(InterfaceType.Adt, result.Value!.InterfaceType);
            Assert.Equal("ADT", result.Value.MessageTypes);
            Assert.Equal("EPIC", result.Value.SendingApplication);
            Assert.Contains(result.Value.FieldMappings, m => m.DataItemKey == InterfaceDataItemKeys.PatientMrn);
            id = result.Value.Id;
        }

        await using var verify = _factory.Create();
        var loaded = await Service(verify).GetAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal(InterfaceVendorCodes.Epic, loaded!.VendorCode);
        Assert.NotEmpty(loaded.FieldMappings);
        Assert.True(await verify.AuditEvents.AnyAsync(a =>
            a.EntityType == nameof(InterfaceEndpoint)
            && a.EntityId == id
            && a.EventType == AuditEventType.Interface));
    }

    [Fact]
    public async Task ResultsEndpoint_CreateAndUpdate_WriteInterface()
    {
        var preset = Hl7ConfigAdminService.VendorPreset(InterfaceVendorCodes.Epic, InterfaceType.Results, Hl7Direction.Inbound);
        Assert.NotNull(preset);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var name = "ORU-RA14-" + suffix;
        await using var context = _factory.Create();
        var svc = Service(context);
        var created = await svc.CreateAsync(new SaveHl7EndpointRequest(
            name,
            InterfaceType.Results,
            Hl7Direction.Inbound,
            InterfaceTransport.File,
            null,
            null,
            $@"C:\hl7\oru-ra14-{suffix}",
            null,
            null,
            InterfaceVendorCodes.Epic,
            InterfaceMappingMode.Vendor,
            "Test",
            preset!.Connection.SendingApplication,
            preset.Connection.SendingFacility,
            preset.Connection.ReceivingApplication,
            preset.Connection.ReceivingFacility,
            null,
            null,
            null,
            null,
            true,
            preset.Mappings,
            "draft ORU endpoint"));
        Assert.True(created.Succeeded, created.Error ?? created.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var updated = await svc.UpdateAsync(created.Value!.Id, new SaveHl7EndpointRequest(
            name,
            InterfaceType.Results,
            Hl7Direction.Inbound,
            InterfaceTransport.File,
            null,
            null,
            $@"C:\hl7\oru-ra14-{suffix}-b",
            null,
            null,
            InterfaceVendorCodes.Epic,
            InterfaceMappingMode.Vendor,
            "Test",
            preset.Connection.SendingApplication,
            preset.Connection.SendingFacility,
            preset.Connection.ReceivingApplication,
            preset.Connection.ReceivingFacility,
            null,
            null,
            null,
            null,
            true,
            preset.Mappings,
            "moved ORU drop folder"));
        Assert.True(updated.Succeeded, updated.Error ?? updated.Evaluation?.HardStops.FirstOrDefault()?.Message);

        var events = await context.AuditEvents
            .Where(a => a.EntityType == nameof(InterfaceEndpoint) && a.EntityId == created.Value.Id)
            .ToListAsync();
        Assert.Equal(2, events.Count(a => a.EventType == AuditEventType.Interface));
    }

    [Fact]
    public void Vocabulary_ReturnsDataItemsAndVendors()
    {
        var items = Hl7ConfigAdminService.DataItems(InterfaceType.Billing, Hl7Direction.Outbound);
        Assert.Contains(items, i => i.Key == InterfaceDataItemKeys.BillingCode);

        var vendors = Hl7ConfigAdminService.Vendors(InterfaceType.Billing);
        Assert.Contains(vendors, v => v.Code == InterfaceVendorCodes.EpicResolute);
    }

    private InterfaceTranslationAdminService TranslationService(BloodBankDbContext context)
    {
        var env = new StaticEnvironmentInfo("Development", isDevMode: false);
        return new InterfaceTranslationAdminService(
            new InterfaceValueTranslationRepository(context),
            context,
            _factory.Clock,
            _factory.CurrentUser,
            new AuditWriter(context, _factory.Clock, _factory.CurrentUser, env),
            new ConfigurationHistoryWriter(context, _factory.Clock, _factory.CurrentUser, env));
    }

    [Fact]
    public async Task Translations_SaveAndLoad_PerDataItem_PreservesOtherItems()
    {
        await using (var context = _factory.Create())
        {
            var svc = TranslationService(context);
            var sex = await svc.ReplaceAsync(InterfaceDataItemKeys.PatientSex, new SaveInterfaceTranslationsRequest(
            [
                new InterfaceValueTranslationDto("F", "FEMALE", InterfaceTranslationDirection.Both),
                new InterfaceValueTranslationDto("M", "MALE", InterfaceTranslationDirection.Inbound)
            ], "sex maps"));
            Assert.True(sex.Succeeded, sex.Error);
            Assert.Equal(2, sex.Value!.Rows.Count);

            var billing = await svc.ReplaceAsync(InterfaceDataItemKeys.BillingCode, new SaveInterfaceTranslationsRequest(
            [
                new InterfaceValueTranslationDto("BB-XM", "71020", InterfaceTranslationDirection.Outbound)
            ], null));
            Assert.True(billing.Succeeded, billing.Error);
        }

        await using var verify = _factory.Create();
        var svc2 = TranslationService(verify);
        var loadedSex = await svc2.GetAsync(InterfaceDataItemKeys.PatientSex);
        Assert.True(loadedSex.Succeeded);
        Assert.Equal(2, loadedSex.Value!.Rows.Count);
        Assert.Contains(loadedSex.Value.Rows, r => r.InternalValue == "F" && r.ExternalValue == "FEMALE");

        var loadedBilling = await svc2.GetAsync(InterfaceDataItemKeys.BillingCode);
        Assert.True(loadedBilling.Succeeded);
        Assert.Single(loadedBilling.Value!.Rows);
        Assert.Equal("71020", loadedBilling.Value.Rows[0].ExternalValue);

        var replaced = await svc2.ReplaceAsync(InterfaceDataItemKeys.PatientSex, new SaveInterfaceTranslationsRequest(
        [
            new InterfaceValueTranslationDto("O", "OTHER", InterfaceTranslationDirection.Both)
        ], "replace sex"));
        Assert.True(replaced.Succeeded, replaced.Error);
        Assert.Single(replaced.Value!.Rows);

        var billingStill = await svc2.GetAsync(InterfaceDataItemKeys.BillingCode);
        Assert.Single(billingStill.Value!.Rows);
    }

    [Fact]
    public async Task Translations_RejectUnknownKey_AndDuplicateCodes()
    {
        await using var context = _factory.Create();
        var svc = TranslationService(context);

        var unknown = await svc.GetAsync("Not.A.Key");
        Assert.False(unknown.Succeeded);
        Assert.Contains("Unknown data item", unknown.Error);

        var dupInternal = await svc.ReplaceAsync(InterfaceDataItemKeys.OrderTestCode, new SaveInterfaceTranslationsRequest(
        [
            new InterfaceValueTranslationDto("XM", "A", InterfaceTranslationDirection.Outbound),
            new InterfaceValueTranslationDto("XM", "B", InterfaceTranslationDirection.Both)
        ], null));
        Assert.False(dupInternal.Succeeded);
        Assert.Contains("internal value", dupInternal.Error, StringComparison.OrdinalIgnoreCase);

        var dupExternal = await svc.ReplaceAsync(InterfaceDataItemKeys.OrderTestCode, new SaveInterfaceTranslationsRequest(
        [
            new InterfaceValueTranslationDto("XM", "HIS_XM", InterfaceTranslationDirection.Inbound),
            new InterfaceValueTranslationDto("TS", "HIS_XM", InterfaceTranslationDirection.Both)
        ], null));
        Assert.False(dupExternal.Succeeded);
        Assert.Contains("external value", dupExternal.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Vocabulary_AllDataItems_AreDistinct()
    {
        var items = InterfaceTranslationAdminService.AllDataItems();
        Assert.Contains(items, i => i.Key == InterfaceDataItemKeys.PatientSex);
        Assert.Equal(items.Count, items.Select(i => i.Key).Distinct().Count());
    }
}
