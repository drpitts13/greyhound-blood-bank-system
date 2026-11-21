using System.Text;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Interfaces;
using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-529: translation rows are read from a spreadsheet paste (tabs) or a CSV file (commas, quotes, header), the
// direction defaulting to Both; bad lines are named and skipped, repeats are counted once; an export reads back the same.
public class InterfaceTranslationImportTests
{
    [Fact]
    public void SpreadsheetPaste_WithAHeader_ReadsEachRow_AndBlankDirectionIsBoth()
    {
        var result = InterfaceTranslationImport.Parse("Local value\tExternal value\tDirection\nTNS\tLAB10117\t\nABORH\tLAB200\tin\r\nWHITE\t2106-3\tOutbound\n\n");

        Assert.Empty(result.Problems);
        Assert.Equal(
            [
                new InterfaceTranslationImport.Row("TNS", "LAB10117", InterfaceTranslationDirection.Both),
                new InterfaceTranslationImport.Row("ABORH", "LAB200", InterfaceTranslationDirection.Inbound),
                new InterfaceTranslationImport.Row("WHITE", "2106-3", InterfaceTranslationDirection.Outbound)
            ],
            result.Rows);
    }

    [Fact]
    public void Csv_HandlesQuotes_AndTwoColumnLines()
    {
        var result = InterfaceTranslationImport.Parse("﻿internal,external\n\"Type, screen\",\"LAB \"\"10117\"\"\",both\nDAT,LAB300");

        Assert.Empty(result.Problems);
        Assert.Equal(("Type, screen", "LAB \"10117\""), (result.Rows[0].LocalValue, result.Rows[0].ExternalValue));
        Assert.Equal(InterfaceTranslationDirection.Both, result.Rows[1].Direction);
    }

    [Fact]
    public void BadLines_AreNamedAndSkipped_AndRepeatsCountedOnce()
    {
        var result = InterfaceTranslationImport.Parse("TNS,LAB10117\n,LAB1\nABSC,\nDAT,LAB300,sideways\nX,Y,Both,extra\ntns,lab10117,BOTH");

        Assert.Single(result.Rows);
        Assert.Equal(1, result.Duplicates);
        Assert.Equal([2, 3, 4, 5], result.Problems.Select(p => p.Line));
        Assert.Contains("local value is blank", result.Problems[0].Message);
        Assert.Contains("'sideways' is not a direction", result.Problems[2].Message);
    }

    [Fact]
    public void Export_ReadsBackUnchanged_IncludingAFormulaGuardedCode()
    {
        InterfaceTranslationImport.Row[] rows =
        [
            new("TNS", "LAB10117", InterfaceTranslationDirection.Both),
            new("HH", "-AA", InterfaceTranslationDirection.Outbound),
            new("Comma, value", "E", InterfaceTranslationDirection.Inbound)
        ];
        var csv = Encoding.UTF8.GetString(CsvFormat.Write(InterfaceTranslationImport.Columns, InterfaceTranslationImport.Cells(rows)));

        var back = InterfaceTranslationImport.Parse(csv);

        Assert.Empty(back.Problems);
        Assert.Equal(rows, back.Rows);
    }

    [Fact]
    public void Empty_HasNothing() =>
        Assert.Equal((0, 0), (InterfaceTranslationImport.Parse("  \n ").Rows.Count, InterfaceTranslationImport.Parse(null).Problems.Count));
}
