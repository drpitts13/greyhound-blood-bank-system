using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-524: translations for one interface override the global rows there; order and result test codes fall back to
// the shared procedure code; inbound, a grouper wins over a test for the same external code; a value with no
// translation is recorded only for a field that has translations in that direction.
public class InterfaceTranslatorScopeTests
{
    private static InterfaceValueTranslation Row(string key, string local, string external,
        InterfaceTranslationDirection direction = InterfaceTranslationDirection.Both, long? endpointId = null) =>
        new() { DataItemKey = key, InternalValue = local, ExternalValue = external, Direction = direction, EndpointId = endpointId };

    [Fact]
    public void InterfaceRows_OverrideGlobalRows_AndOtherInterfacesAreIgnored()
    {
        var rows = new[]
        {
            Row(InterfaceDataItemKeys.PatientSex, "F", "FEMALE"),
            Row(InterfaceDataItemKeys.PatientSex, "F", "2", endpointId: 7),
            Row(InterfaceDataItemKeys.PatientSex, "M", "1", endpointId: 9)
        };

        var forSeven = InterfaceValueTranslator.From(rows, endpointId: 7);
        Assert.Equal("2", forSeven.ToExternal(InterfaceDataItemKeys.PatientSex, "F"));
        Assert.Equal("F", forSeven.ToInternal(InterfaceDataItemKeys.PatientSex, "2"));
        Assert.True(forSeven.Explain(InterfaceDataItemKeys.PatientSex, "F", inbound: false).FromInterfaceOverride);
        Assert.Equal("M", forSeven.ToExternal(InterfaceDataItemKeys.PatientSex, "M"));

        var global = InterfaceValueTranslator.From(rows);
        Assert.Equal("FEMALE", global.ToExternal(InterfaceDataItemKeys.PatientSex, "F"));
        Assert.False(global.Explain(InterfaceDataItemKeys.PatientSex, "F", inbound: false).FromInterfaceOverride);
    }

    [Fact]
    public void TestCodes_FallBackToTheProcedureCode_AndASpecificRowWins()
    {
        var rows = new[]
        {
            Row(InterfaceDataItemKeys.ProcedureCode, "TNS", "LAB10117"),
            Row(InterfaceDataItemKeys.ResultObrTestCode, "ABORH", "LAB200", InterfaceTranslationDirection.Outbound),
            Row(InterfaceDataItemKeys.ProcedureCode, "ABORH", "LAB100")
        };
        var translator = InterfaceValueTranslator.From(rows);

        Assert.Equal("TNS", translator.ToInternal(InterfaceDataItemKeys.OrderTestCode, "lab10117"));
        Assert.Equal("LAB10117", translator.ToExternal(InterfaceDataItemKeys.ResultObrTestCode, "TNS"));
        Assert.Equal("LAB200", translator.ToExternal(InterfaceDataItemKeys.ResultObrTestCode, "ABORH"));
        var outcome = translator.Explain(InterfaceDataItemKeys.OrderTestCode, "LAB100", inbound: true);
        Assert.Equal(("ABORH", InterfaceDataItemKeys.ProcedureCode), (outcome.Value, outcome.FromKey));
        Assert.True(translator.TryToExternal(InterfaceDataItemKeys.ResultObrTestCode, "TNS", out var external));
        Assert.Equal("LAB10117", external);
        Assert.False(translator.TryToExternal(InterfaceDataItemKeys.ResultObrTestCode, "ABSC", out _));
    }

    [Fact]
    public void Inbound_AGrouperWinsOverATestForTheSameExternalCode()
    {
        var rows = new[]
        {
            Row(InterfaceDataItemKeys.ProcedureCode, "ABORH", "LAB10117", InterfaceTranslationDirection.Inbound),
            Row(InterfaceDataItemKeys.ProcedureCode, "TNS", "LAB10117", InterfaceTranslationDirection.Inbound),
            Row(InterfaceDataItemKeys.ProcedureCode, "ABSC", "LAB10117", InterfaceTranslationDirection.Inbound)
        };
        var translator = InterfaceValueTranslator.From(rows, isGrouper: code => code == "TNS");
        Assert.Equal("TNS", translator.ToInternal(InterfaceDataItemKeys.OrderTestCode, "LAB10117"));
    }

    [Fact]
    public void Misses_AreRecordedOnlyForFieldsWithTranslations_Once()
    {
        var translator = InterfaceValueTranslator.From([Row(InterfaceDataItemKeys.PatientRace, "WHITE", "2106-3")]);

        Assert.Equal("9999-9", translator.ToInternal(InterfaceDataItemKeys.PatientRace, "9999-9"));
        translator.ToInternal(InterfaceDataItemKeys.PatientRace, "9999-9");
        translator.ToInternal(InterfaceDataItemKeys.PatientLastName, "Smith");
        translator.ToExternal(InterfaceDataItemKeys.PatientRace, "ASIAN");
        translator.RecordMiss(InterfaceDataItemKeys.OrderTestCode, "LAB99999", InterfaceTranslationDirection.Inbound);

        Assert.Equal(
            [
                new TranslationMiss(InterfaceDataItemKeys.PatientRace, "9999-9", InterfaceTranslationDirection.Inbound),
                new TranslationMiss(InterfaceDataItemKeys.PatientRace, "ASIAN", InterfaceTranslationDirection.Outbound),
                new TranslationMiss(InterfaceDataItemKeys.OrderTestCode, "LAB99999", InterfaceTranslationDirection.Inbound)
            ],
            translator.Misses);
        Assert.Empty(InterfaceValueTranslator.Empty.Misses);
    }
}
