using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.Domain.Tests;

public class InterfaceValueTranslatorTests
{
    [Fact]
    public void Empty_PassesValuesThrough()
    {
        Assert.Equal("M", InterfaceValueTranslator.Empty.ToInternal(InterfaceDataItemKeys.PatientSex, "M"));
        Assert.Equal("M", InterfaceValueTranslator.Empty.ToExternal(InterfaceDataItemKeys.PatientSex, "M"));
        Assert.Equal(string.Empty, InterfaceValueTranslator.Empty.ToInternal(InterfaceDataItemKeys.PatientSex, string.Empty));
        Assert.Null(InterfaceValueTranslator.Empty.ToExternal(InterfaceDataItemKeys.PatientSex, null));
    }

    [Fact]
    public void ToInternal_MatchesExternalCaseInsensitively_AndReturnsStoredInternal()
    {
        var translator = InterfaceValueTranslator.From(
        [
            Row(InterfaceDataItemKeys.PatientSex, "F", "FEMALE", InterfaceTranslationDirection.Both)
        ]);

        Assert.Equal("F", translator.ToInternal(InterfaceDataItemKeys.PatientSex, "female"));
        Assert.Equal("F", translator.ToInternal(InterfaceDataItemKeys.PatientSex, "FEMALE"));
        Assert.Equal("X", translator.ToInternal(InterfaceDataItemKeys.PatientSex, "X"));
    }

    [Fact]
    public void ToExternal_MatchesInternalCaseInsensitively_AndReturnsStoredExternal()
    {
        var translator = InterfaceValueTranslator.From(
        [
            Row(InterfaceDataItemKeys.BillingCode, "BB-XM", "71020", InterfaceTranslationDirection.Both)
        ]);

        Assert.Equal("71020", translator.ToExternal(InterfaceDataItemKeys.BillingCode, "bb-xm"));
        Assert.Equal("OTHER", translator.ToExternal(InterfaceDataItemKeys.BillingCode, "OTHER"));
    }

    [Fact]
    public void Direction_InboundRow_DoesNotApplyOutbound()
    {
        var translator = InterfaceValueTranslator.From(
        [
            Row(InterfaceDataItemKeys.OrderTestCode, "XM", "HIS_XM", InterfaceTranslationDirection.Inbound)
        ]);

        Assert.Equal("XM", translator.ToInternal(InterfaceDataItemKeys.OrderTestCode, "HIS_XM"));
        Assert.Equal("XM", translator.ToExternal(InterfaceDataItemKeys.OrderTestCode, "XM"));
    }

    [Fact]
    public void Direction_OutboundRow_DoesNotApplyInbound()
    {
        var translator = InterfaceValueTranslator.From(
        [
            Row(InterfaceDataItemKeys.OrderTestCode, "XM", "HIS_XM", InterfaceTranslationDirection.Outbound)
        ]);

        Assert.Equal("HIS_XM", translator.ToInternal(InterfaceDataItemKeys.OrderTestCode, "HIS_XM"));
        Assert.Equal("HIS_XM", translator.ToExternal(InterfaceDataItemKeys.OrderTestCode, "XM"));
    }

    [Fact]
    public void AsymmetricRows_UseDifferentExternalValuesPerDirection()
    {
        var translator = InterfaceValueTranslator.From(
        [
            Row(InterfaceDataItemKeys.ResultValue, "A Pos", "A+", InterfaceTranslationDirection.Inbound),
            Row(InterfaceDataItemKeys.ResultValue, "A Pos", "A Positive", InterfaceTranslationDirection.Outbound)
        ]);

        Assert.Equal("A Pos", translator.ToInternal(InterfaceDataItemKeys.ResultValue, "A+"));
        Assert.Equal("A Positive", translator.ToExternal(InterfaceDataItemKeys.ResultValue, "A Pos"));
        Assert.Equal("A Positive", translator.ToInternal(InterfaceDataItemKeys.ResultValue, "A Positive"));
    }

    [Fact]
    public void UnrelatedDataItem_IsNotTranslated()
    {
        var translator = InterfaceValueTranslator.From(
        [
            Row(InterfaceDataItemKeys.PatientSex, "F", "FEMALE", InterfaceTranslationDirection.Both)
        ]);

        Assert.Equal("FEMALE", translator.ToInternal(InterfaceDataItemKeys.PatientMrn, "FEMALE"));
    }

    private static InterfaceValueTranslation Row(
        string key, string internalValue, string externalValue, InterfaceTranslationDirection direction) =>
        new()
        {
            DataItemKey = key,
            InternalValue = internalValue,
            ExternalValue = externalValue,
            Direction = direction
        };
}
