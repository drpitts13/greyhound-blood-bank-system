using BloodBankLIS.Printing.Rendering;

namespace BloodBankLIS.Printing.Tests;

public class ZplLabelRendererTests
{
    private readonly ZplLabelRenderer _renderer = new();

    [Fact]
    public void Render_WrapsWithStartAndEndAndDimensions()
    {
        var doc = new LabelDocument(406, 203, new LabelElement[]
        {
            new LabelText(10, 10, "Hello")
        });

        var zpl = _renderer.Render(doc);

        Assert.StartsWith("^XA", zpl);
        Assert.EndsWith("^XZ", zpl);
        Assert.Contains("^PW406", zpl);
        Assert.Contains("^LL203", zpl);
        Assert.Contains("^FDHello^FS", zpl);
    }

    [Fact]
    public void Render_EmitsBarcodeCommandForBarcodeElement()
    {
        var doc = new LabelDocument(406, 203, new LabelElement[]
        {
            new LabelBarcode(10, 50, "ACC-123", height: 60)
        });

        var zpl = _renderer.Render(doc);

        Assert.Contains("^BCN,60,Y,N,N", zpl);
        Assert.Contains("^FDACC-123^FS", zpl);
    }

    [Theory]
    [InlineData("a^b", "a_5Eb")]
    [InlineData("a~b", "a_7Eb")]
    [InlineData("a\\b", "a_5Cb")]
    [InlineData("a_b", "a_5Fb")]
    public void Escape_NeutralizesControlCharacters(string input, string expected)
    {
        Assert.Equal(expected, ZplLabelRenderer.Escape(input));
    }

    [Fact]
    public void Render_EscapesControlCharsInFieldData()
    {
        var doc = new LabelDocument(100, 100, new LabelElement[]
        {
            new LabelText(0, 0, "Smith^Evil~Inject")
        });

        var zpl = _renderer.Render(doc);

        // The raw caret/tilde must be escaped, never appearing as commands in field data.
        Assert.Contains("^FH^FDSmith_5EEvil_7EInject^FS", zpl);
    }
}
