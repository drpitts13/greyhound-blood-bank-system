namespace BloodBankLIS.Printing.Rendering;

/// <summary>
/// A renderer-agnostic description of a label: dimensions in dots plus positioned
/// elements. Templates build this from a data model; renderers turn it into ZPL or a
/// preview. No business logic lives here (see docs/printing-billing.md A.1-A.2).
/// </summary>
public sealed class LabelDocument
{
    public LabelDocument(
        int widthDots,
        int heightDots,
        IEnumerable<LabelElement> elements,
        LabelCheckForm? checkForm = null,
        IEnumerable<LabelElement>? page2 = null,
        int page2WidthDots = 0,
        int page2HeightDots = 0)
    {
        WidthDots = widthDots;
        HeightDots = heightDots;
        Elements = elements.ToList();
        CheckForm = checkForm;
        Page2Elements = page2?.ToList() ?? [];
        Page2WidthDots = page2WidthDots;
        Page2HeightDots = page2HeightDots;
    }

    public int WidthDots { get; }

    public int HeightDots { get; }

    public IReadOnlyList<LabelElement> Elements { get; }

    /// <summary>Bottom half of a folded P-tag. Empty when the built-in form is used instead.</summary>
    public IReadOnlyList<LabelElement> Page2Elements { get; }

    public int Page2WidthDots { get; }

    public int Page2HeightDots { get; }

    /// <summary>
    /// When set, the PDF renderer prints the label on the top half of a portrait
    /// letter page and this form on the bottom half. ZPL ignores it.
    /// </summary>
    public LabelCheckForm? CheckForm { get; }
}

/// <summary>
/// A fold-under check form: product and patient facts, no barcode, plus a signature.
/// </summary>
public sealed class LabelCheckForm
{
    public LabelCheckForm(string attestation, IEnumerable<LabelCheckField> fields)
    {
        Attestation = attestation;
        Fields = fields.ToList();
    }

    public string Attestation { get; }

    public IReadOnlyList<LabelCheckField> Fields { get; }
}

public sealed record LabelCheckField(string Label, string Value);

/// <summary>Base type for a positioned element on a label (origin is top-left, in dots).</summary>
public abstract class LabelElement
{
    protected LabelElement(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }
}

/// <summary>A line of human-readable text.</summary>
public sealed class LabelText : LabelElement
{
    public LabelText(int x, int y, string value, int fontHeight = 28, int fontWidth = 28, bool bold = false)
        : base(x, y)
    {
        Value = value ?? string.Empty;
        FontHeight = fontHeight;
        FontWidth = fontWidth;
        Bold = bold;
    }

    public string Value { get; }

    public int FontHeight { get; }

    public int FontWidth { get; }

    public bool Bold { get; }
}

/// <summary>A 1D barcode (Code 128 by default) with optional human-readable interpretation.</summary>
public sealed class LabelBarcode : LabelElement
{
    public LabelBarcode(int x, int y, string data, int height = 80, bool printInterpretation = true)
        : base(x, y)
    {
        Data = data ?? string.Empty;
        Height = height;
        PrintInterpretation = printInterpretation;
    }

    public string Data { get; }

    public int Height { get; }

    public bool PrintInterpretation { get; }
}
