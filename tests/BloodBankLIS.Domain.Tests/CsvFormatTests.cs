using System.Text;
using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-300: CSV quoting and spreadsheet formula-injection guard.
public class CsvFormatTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData(" padded ", "\" padded \"")]
    public void Cell_QuotesPerRfc4180(string? value, string expected)
    {
        Assert.Equal(expected, CsvFormat.Cell(value));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("-cmd|' /C calc'!A0", "'-cmd|' /C calc'!A0")]
    [InlineData("\tTAB", "'\tTAB")]
    public void Cell_NeutralizesFormulaStarts(string value, string expected)
    {
        Assert.Equal(expected, CsvFormat.Cell(value));
    }

    [Theory]
    [InlineData("-18.5")]
    [InlineData("-3")]
    [InlineData("+4")]
    public void Cell_LeavesSignedNumbersAlone(string value)
    {
        Assert.Equal(value, CsvFormat.Cell(value));
    }

    [Fact]
    public void Write_HasBom_HeaderRow_AndCrLfLines()
    {
        var bytes = CsvFormat.Write(["unit", "note"], [["U-1", "ok"], ["U-2", null]]);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3).ToArray());
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.Equal("unit,note\r\nU-1,ok\r\nU-2,\r\n", text);
    }
}
