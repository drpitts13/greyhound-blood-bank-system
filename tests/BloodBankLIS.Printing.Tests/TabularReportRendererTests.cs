using BloodBankLIS.Printing.Rendering;

namespace BloodBankLIS.Printing.Tests;

public class TabularReportRendererTests
{
    [Fact]
    public void PdfAndExcel_AreNonEmptyDocuments()
    {
        var renderer = new TabularReportRenderer();
        var columns = new[] { "Unit", "Status" };
        var rows = new List<IReadOnlyList<string?>> { new[] { "W123", "Available" } };

        var pdf = renderer.ToPdf("Inventory on hand", columns, rows, truncated: false);
        var excel = renderer.ToExcel("Inventory on hand", columns, rows, truncated: false);

        Assert.True(pdf.Length > 4);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);

        Assert.True(excel.Length > 2);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }
}
