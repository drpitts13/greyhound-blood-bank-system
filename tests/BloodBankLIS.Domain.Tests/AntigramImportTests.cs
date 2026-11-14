using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AntigramImportTests
{
    private static readonly IReadOnlyList<AntigramImport.Antigen> Catalog =
    [
        new(1, "C", "anti-C"), new(2, "c", "anti-c"), new(3, "E", "anti-E"), new(4, "K", "anti-K"),
        new(5, "FYA", "anti-Fya"), new(6, "JKA", "anti-Jka")
    ];

    private static IReadOnlyList<IReadOnlyList<string>> Csv(string text) => CsvFormat.ReadRows(text);

    // TEST-BB-949: a manufacturer sheet saved in the layout: title rows skipped, the Cell header found, the Role column
    // read, antigen columns matched by code (C and c kept apart) or by a looser spelling ("Fy(a)", "anti-Jka"), values
    // read including the apostrophe a spreadsheet or the export adds; an autocontrol row has no typings; a column that
    // names no antigen is reported and left out.
    [Fact]
    public void Parse_ReadsAManufacturerSheet()
    {
        var result = AntigramImport.Parse(Csv(
            "Panel A lot 1234,,,,,,,\n" +
            "Expires 2027-01-31,,,,,,,\n" +
            "Cell,Role,C,c,E,Fy(a),anti-Jka,Donor\n" +
            "1,Panel,+,0,w,'+,+het,A123\n" +
            "2,,0,+,0,0,+hom,B456\n" +
            "3,Autocontrol,,,,,,\n"), Catalog);

        Assert.True(result.CanUse, string.Join("; ", result.Problems));
        Assert.Equal(["Donor"], result.IgnoredColumns);
        Assert.Equal([1L, 2, 3, 5, 6], result.AntigenIds);
        Assert.Equal(3, result.Cells.Count);
        var first = result.Cells[0];
        Assert.Equal(("1", PanelCellRole.Panel), (first.CellNumber, first.Role));
        Assert.Equal(
            [AntigenExpression.Present, AntigenExpression.Absent, AntigenExpression.Weak, AntigenExpression.Present, AntigenExpression.Heterozygous],
            first.Antigens.Select(a => a.Expression));
        Assert.Equal(AntigenExpression.Homozygous, result.Cells[1].Antigens.Single(a => a.AntigenId == 6).Expression);
        Assert.Equal(PanelCellRole.Autocontrol, result.Cells[2].Role);
        Assert.Empty(result.Cells[2].Antigens);
    }

    // TEST-BB-950: an unreadable value, a repeated cell, an unknown role, no header, no antigen column, or too many cells
    // stops the import (nothing is used); "k" never matches K.
    [Fact]
    public void Parse_Problems_StopTheImport()
    {
        var bad = AntigramImport.Parse(Csv("Cell,C,K\n1,+,maybe\n1,0,0\n2,x,0\n"), Catalog);
        Assert.False(bad.CanUse);
        Assert.Contains(bad.Problems, p => p.Contains("Line 2, cell 1, K: \"maybe\" is not"));
        Assert.Contains(bad.Problems, p => p.Contains("Line 3: cell 1 appears more than once"));
        Assert.Contains(bad.Problems, p => p.Contains("cell 2, C: \"x\""));

        Assert.False(AntigramImport.Parse(Csv("Cell,Role,C\n1,Spare,+\n"), Catalog).CanUse);
        Assert.Contains("No header row", Assert.Single(AntigramImport.Parse(Csv("Number,C\n1,+\n"), Catalog).Problems));
        Assert.Contains(AntigramImport.Parse(Csv("Cell,Donor\n1,A1\n"), Catalog).Problems, p => p.Contains("No column names an antigen"));

        var lowerK = AntigramImport.Parse(Csv("Cell,k,C\n1,+,0\n"), Catalog);
        Assert.Equal(["k"], lowerK.IgnoredColumns);

        var many = "Cell,C\n" + string.Join("\n", Enumerable.Range(1, AntigramImport.MaxCells + 1).Select(i => $"{i},+"));
        Assert.Contains(AntigramImport.Parse(Csv(many), Catalog).Problems, p => p.Contains($"at most {AntigramImport.MaxCells}"));
    }

    // TEST-BB-951: an exported antigram reads back as the same cells (the export is the import layout).
    [Fact]
    public void Export_ReadsBack()
    {
        var antigens = Catalog.Take(3).ToList();
        var bytes = CsvFormat.Write(AntigramImport.Header(antigens),
        [
            AntigramImport.Row("1", PanelCellRole.Panel, [AntigenExpression.Present, AntigenExpression.Absent, AntigenExpression.Heterozygous]),
            AntigramImport.Row("2", PanelCellRole.Selected, [AntigenExpression.NotTested, AntigenExpression.Weak, AntigenExpression.Homozygous])
        ]);
        var text = System.Text.Encoding.UTF8.GetString(bytes);

        var result = AntigramImport.Parse(CsvFormat.ReadRows(text), Catalog);
        Assert.True(result.CanUse, string.Join("; ", result.Problems));
        Assert.Equal(
            [AntigenExpression.Present, AntigenExpression.Absent, AntigenExpression.Heterozygous],
            result.Cells[0].Antigens.Select(a => a.Expression));
        Assert.Equal(PanelCellRole.Selected, result.Cells[1].Role);
        Assert.Equal(
            [AntigenExpression.NotTested, AntigenExpression.Weak, AntigenExpression.Homozygous],
            result.Cells[1].Antigens.Select(a => a.Expression));
    }
}
