using System.Text;
using BloodBankLIS.Printing.Rendering;
using BloodBankLIS.Printing.Templates;

namespace BloodBankLIS.Printing.Tests;

public class PdfLabelRendererTests
{
    private readonly PdfLabelRenderer _renderer = new();

    [Fact]
    public void Render_ProducesPdfContainingTheTextField()
    {
        var doc = new LabelDocument(406, 203, new LabelElement[]
        {
            new LabelText(10, 10, "HelloLabel")
        });

        var pdf = _renderer.Render(doc).Pdf;

        Assert.NotNull(pdf);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf!, 0, 4));
        Assert.Contains("HelloLabel", Encoding.Latin1.GetString(pdf));
        Assert.DoesNotContain("0 0 612 792", Encoding.Latin1.GetString(pdf));
    }

    [Fact]
    public void CompatibilityTag_PrintsOnTheTopHalfOfAPortraitLetterPage()
    {
        var model = new CompatibilityTagModel(
            "Doe, John", "MRN-9", new DateOnly(1980, 1, 1), "O+",
            "W12345", "O-", "Red Blood Cells", "Serologic", "Compatible",
            new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc),
            "tech", false);
        var doc = CompatibilityTagTemplate.Build(model);

        var pdf = _renderer.Render(doc).Pdf;
        var latin = Encoding.Latin1.GetString(pdf!);

        Assert.NotNull(doc.CheckForm);
        Assert.Contains("0 0 612 792", latin);
        Assert.Contains(CompatibilityTagTemplate.CheckAttestation, latin);
        Assert.Contains("Signature", latin);
        Assert.Contains("Date", latin);
        Assert.Contains("Doe, John", latin);
        Assert.Contains("Red Blood Cells", latin);
    }
}
