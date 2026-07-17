namespace BloodBankLIS.Printing.Rendering;

/// <summary>
/// A renderer-agnostic description of a label: dimensions in dots plus positioned
/// elements. Templates build this from a data model; renderers turn it into ZPL or a
/// preview. No business logic lives here (see docs/printing-billing.md A.1-A.2).
/// </summary>
public sealed class LabelDocument
{
    public LabelDocument(int widthDots, int heightDots, IEnumerable<LabelElement> elements)
    {
        WidthDots = widthDots;
        HeightDots = heightDots;
        Elements = elements.ToList();
    }

    public int WidthDots { get; }

    public int HeightDots { get; }

    public IReadOnlyList<LabelElement> Elements { get; }
}

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
