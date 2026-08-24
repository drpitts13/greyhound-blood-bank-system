using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.Domain.Tests;

public class InterfaceSetupCatalogTests
{
    [Fact]
    public void DataItemCatalog_AllDistinct_IncludesSharedAndTypeSpecificKeys()
    {
        var items = InterfaceDataItemCatalog.AllDistinct();
        Assert.Contains(items, i => i.Key == InterfaceDataItemKeys.PatientSex);
        Assert.Contains(items, i => i.Key == InterfaceDataItemKeys.OrderTestCode);
        Assert.Contains(items, i => i.Key == InterfaceDataItemKeys.BillingCode);
        Assert.Equal(items.Count, items.Select(i => i.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.True(InterfaceDataItemCatalog.ContainsKey(InterfaceDataItemKeys.PatientMrn));
        Assert.False(InterfaceDataItemCatalog.ContainsKey("Not.A.Key"));
    }

    [Fact]
    public void DataItemCatalog_Adt_IncludesMrnAsRequired()
    {
        var items = InterfaceDataItemCatalog.For(InterfaceType.Adt, Hl7Direction.Inbound);
        var mrn = items.Single(i => i.Key == InterfaceDataItemKeys.PatientMrn);
        Assert.True(mrn.Required);
        Assert.Equal("PID-3-1", mrn.DefaultHl7Path);
    }

    [Fact]
    public void VendorPresets_EpicAdt_SetsSendingApplication()
    {
        var preset = InterfaceVendorPresets.Get(InterfaceVendorCodes.Epic, InterfaceType.Adt, Hl7Direction.Inbound);
        Assert.NotNull(preset);
        Assert.Equal("EPIC", preset!.Connection.SendingApplication);
        Assert.Contains(preset.Mappings, m => m.DataItemKey == InterfaceDataItemKeys.PatientMrn && m.Hl7Path == "PID-3-1");
    }

    [Fact]
    public void VendorPresets_MeditechAdt_UsesPid2ForMrn()
    {
        var preset = InterfaceVendorPresets.Get(InterfaceVendorCodes.Meditech, InterfaceType.Adt, Hl7Direction.Inbound);
        Assert.NotNull(preset);
        Assert.Contains(preset!.Mappings, m => m.DataItemKey == InterfaceDataItemKeys.PatientMrn && m.Hl7Path == "PID-2");
    }

    [Fact]
    public void VendorPresets_BillingVendors_OnlyForBilling()
    {
        var billing = InterfaceVendorPresets.For(InterfaceType.Billing);
        Assert.Contains(billing, v => v.Code == InterfaceVendorCodes.EpicResolute);
        Assert.DoesNotContain(InterfaceVendorPresets.For(InterfaceType.Adt), v => v.Code == InterfaceVendorCodes.EpicResolute);
    }

    [Fact]
    public void TypeDefaults_DeriveMessageTypesAndDirection()
    {
        Assert.Equal("ADT", InterfaceTypeDefaults.MessageTypes(InterfaceType.Adt));
        Assert.Equal("ORM,OML", InterfaceTypeDefaults.MessageTypes(InterfaceType.Orders));
        Assert.Equal(Hl7Direction.Outbound, InterfaceTypeDefaults.Direction(InterfaceType.Billing));
        Assert.True(InterfaceTypeDefaults.SupportsMessageType(InterfaceType.Bpam, "RAS"));
    }
}
