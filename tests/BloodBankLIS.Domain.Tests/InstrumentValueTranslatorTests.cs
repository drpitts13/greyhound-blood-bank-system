using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Instruments;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-535: an instrument's translations: a row limited to the result's test wins, order test codes go out, misses
// are collected once, error codes map to Hold/Warn/Ignore (an unknown code holds), rows are validated, and instrument
// rows import with an optional test limit.
public class InstrumentValueTranslatorTests
{
    private static InstrumentValueTranslation Row(string key, string instrument, string local, string? context = null,
        InterfaceTranslationDirection direction = InterfaceTranslationDirection.Inbound, int sort = 0) =>
        new() { DataItemKey = key, InstrumentValue = instrument, LocalValue = local, Context = context, Direction = direction, SortOrder = sort };

    [Fact]
    public void ARowForTheTest_WinsOverARowForEveryTest()
    {
        var translator = InstrumentValueTranslator.From(
        [
            Row(InstrumentTranslationKeys.Interpretation, "Pos", "Positive"),
            Row(InstrumentTranslationKeys.Interpretation, "Pos", "B|Positive", context: "ABORH")
        ]);

        Assert.Equal("B|Positive", translator.ToLocal(InstrumentTranslationKeys.Interpretation, "pos", "ABORH"));
        Assert.Equal("Positive", translator.ToLocal(InstrumentTranslationKeys.Interpretation, "Pos", "DAT"));
        Assert.Null(translator.ToLocal(InstrumentTranslationKeys.Interpretation, "Neg", "DAT"));
    }

    [Fact]
    public void OrderTestCodes_GoOut_AndInboundRowsDoNot()
    {
        var translator = InstrumentValueTranslator.From(
        [
            Row(InstrumentTranslationKeys.OrderTestCode, "Group", "ABORH", direction: InterfaceTranslationDirection.Outbound),
            Row(InstrumentTranslationKeys.ResultTestCode, "Group2", "ABORH")
        ]);

        Assert.Equal("Group", translator.ToInstrument(InstrumentTranslationKeys.OrderTestCode, "aborh"));
        Assert.Null(translator.ToLocal(InstrumentTranslationKeys.OrderTestCode, "Group"));
    }

    [Fact]
    public void ErrorCodes_MapToActions_AndAnUnknownCodeHolds_AndIsListedOnce()
    {
        var translator = InstrumentValueTranslator.From(
        [
            Row(InstrumentTranslationKeys.ErrorCode, "N", "Ignore"),
            Row(InstrumentTranslationKeys.ErrorCode, "W", "warn")
        ]);

        Assert.Equal((InstrumentErrorAction.Ignore, true), translator.ErrorAction("N"));
        Assert.Equal((InstrumentErrorAction.Warn, true), translator.ErrorAction("W"));
        Assert.Equal((InstrumentErrorAction.Hold, false), translator.ErrorAction("Q"));
        translator.ErrorAction("q");
        var miss = Assert.Single(translator.Misses);
        Assert.Equal((InstrumentTranslationKeys.ErrorCode, "Q"), (miss.DataItemKey, miss.Value));
    }

    [Fact]
    public void Validation_NamesBadRows()
    {
        var results = InstrumentInterfaceValidator.ValidateTranslations(
        [
            Row("Instrument.Nope", "A", "B"),
            Row(InstrumentTranslationKeys.ResultTestCode, "Group", ""),
            Row(InstrumentTranslationKeys.ResultTestCode, "Group", "ABORH", context: "ABORH"),
            Row(InstrumentTranslationKeys.ErrorCode, "Q", "Maybe"),
            Row(InstrumentTranslationKeys.OrderTestCode, "Group", "ABORH", direction: InterfaceTranslationDirection.Outbound),
            Row(InstrumentTranslationKeys.OrderTestCode, "Group2", "ABORH", direction: InterfaceTranslationDirection.Outbound),
            Row(InstrumentTranslationKeys.ErrorCode, "N", "Ignore", context: "ABORH")
        ]);

        Assert.Equal(["INST-XLAT-KEY", "INST-XLAT", "INST-XLAT-CONTEXT", "INST-XLAT-ACTION", "INST-XLAT-OUT"], results.Select(r => r.Code));
    }

    [Fact]
    public void InstrumentRows_ImportWithAnOptionalTestLimit()
    {
        var result = InterfaceTranslationImport.ParseInstrument("Instrument value\tLocal value\tTest limit\nB Pos\tB|Positive\tABORH\nPos\tPositive\n\tX\nPos,Positive\n");

        Assert.Equal(
            [new InterfaceTranslationImport.InstrumentRow("B Pos", "B|Positive", "ABORH"), new InterfaceTranslationImport.InstrumentRow("Pos", "Positive", null)],
            result.Rows);
        Assert.Equal(4, Assert.Single(result.Problems).Line);
        Assert.Equal(1, result.Duplicates);
    }
}
