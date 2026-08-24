using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Common;
using BloodBankLIS.Infrastructure.Persistence;

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
                null,
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
    }

    [Fact]
    public void Vocabulary_ReturnsDataItemsAndVendors()
    {
        var items = Hl7ConfigAdminService.DataItems(InterfaceType.Billing, Hl7Direction.Outbound);
        Assert.Contains(items, i => i.Key == InterfaceDataItemKeys.BillingCode);

        var vendors = Hl7ConfigAdminService.Vendors(InterfaceType.Billing);
        Assert.Contains(vendors, v => v.Code == InterfaceVendorCodes.EpicResolute);
    }
}
