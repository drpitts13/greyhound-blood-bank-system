using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Printing;

/// <summary>A label stock size offered when a design is created, in inches and in dots at 203 dpi.</summary>
public sealed record LabelStockSize(string Label, decimal WidthInches, decimal HeightInches)
{
    public int WidthDots => SpecimenLabelDesignRules.ToDots(WidthInches);

    public int HeightDots => SpecimenLabelDesignRules.ToDots(HeightInches);
}

/// <summary>What a design check found: errors stop the save, warnings are shown and saved with.</summary>
public sealed record SpecimenLabelDesignCheck(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool Ok => Errors.Count == 0;
}

/// <summary>
/// Rules for specimen label designs (Cycle 657). Layout only: what a design must hold to print and scan, and what to
/// warn about. Whether a label must carry two patient identifiers is the laboratory's decision (OCD-080), so it warns.
/// </summary>
public static class SpecimenLabelDesignRules
{
    public const int Dpi = 203;
    public const decimal MinWidthInches = 0.5m;
    public const decimal MaxWidthInches = 4m;
    public const decimal MinHeightInches = 0.25m;
    public const decimal MaxHeightInches = 3m;

    /// <summary>Accession numbers this long are assumed when estimating how wide a barcode prints.</summary>
    public const int SampleBarcodeLength = 12;

    /// <summary>Barcodes print at least this tall (dots), whatever the size set on the element.</summary>
    public const int MinBarcodeHeight = 40;

    /// <summary>Room the digits printed under a barcode take.</summary>
    public const int BarcodeTextHeight = 22;

    public static IReadOnlyList<LabelStockSize> StockSizes { get; } =
    [
        new("2 × 1 in (tube)", 2m, 1m),
        new("2.25 × 1.25 in", 2.25m, 1.25m),
        new("3 × 1 in", 3m, 1m),
        new("2 × 0.75 in", 2m, 0.75m),
        new("1.5 × 0.5 in (aliquot / cryovial)", 1.5m, 0.5m),
        new("1 × 0.5 in", 1m, 0.5m)
    ];

    public static int ToDots(decimal inches) => (int)Math.Round(inches * Dpi, MidpointRounding.AwayFromZero);

    public static decimal ToInches(int dots) => Math.Round((decimal)dots / Dpi, 2);

    /// <summary>"2", "1.5", "0.82": inches as people write them.</summary>
    public static string InchesText(int dots) => ToInches(dots).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public static string InchesText(decimal inches) => inches.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// How wide a Code 128 barcode prints: eleven modules per character plus start, check, and stop (and the two-module
    /// stop bar), times the narrow bar width. Conservative: numeric runs print narrower.
    /// </summary>
    public static int EstimateBarcodeWidth(int dataLength, int moduleWidth) =>
        Math.Clamp(moduleWidth, 1, 4) * ((11 * (Math.Max(1, dataLength) + 3)) + 2);

    public static string? SizeError(decimal widthInches, decimal heightInches) =>
        widthInches < MinWidthInches || widthInches > MaxWidthInches
            ? $"Label width must be between {MinWidthInches} and {MaxWidthInches} inches."
            : heightInches < MinHeightInches || heightInches > MaxHeightInches
                ? $"Label height must be between {MinHeightInches} and {MaxHeightInches} inches."
                : null;

    public static SpecimenLabelDesignCheck Check(SpecimenLabelPurpose purpose, int widthDots, int heightDots, IReadOnlyList<PlacedLabelField> fields)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        if (fields.Count == 0)
        {
            errors.Add("Place at least one element on the label.");
            return new SpecimenLabelDesignCheck(errors, warnings);
        }

        var palette = LabelFields.SpecimenDesignPalette(purpose == SpecimenLabelPurpose.Aliquot);
        foreach (var field in fields)
        {
            if (palette.All(f => f.Key != field.Key))
            {
                errors.Add(field.Key == LabelFields.AliquotNumber
                    ? "The aliquot number belongs on an aliquot label design."
                    : $"'{LabelFields.Caption(field.Key)}' is not a specimen label element.");
            }

            if (field.Key == LabelFields.StaticText && string.IsNullOrWhiteSpace(field.StaticText))
            {
                errors.Add("Static text needs the words to print.");
            }
        }

        // The tube is scanned at receipt, testing, and storage: the label must carry the specimen ID as a barcode.
        if (!fields.Any(f => f.Key == LabelFields.Accession && f.Barcode))
        {
            errors.Add("Place the specimen ID as a barcode so the tube can be scanned.");
        }

        var identifiers = fields.Select(f => f.Key)
            .Where(k => k is LabelFields.PatientName or LabelFields.Mrn or LabelFields.DateOfBirth)
            .Distinct()
            .Count();
        if (identifiers < 2)
        {
            warnings.Add("Fewer than two patient identifiers (name, patient ID, date of birth) are on the label. Check this against your laboratory's labeling policy (OCD-080).");
        }

        if (purpose == SpecimenLabelPurpose.Aliquot && fields.All(f => f.Key != LabelFields.AliquotNumber))
        {
            warnings.Add("The aliquot number is not on the label, so aliquots of one specimen will look alike.");
        }

        foreach (var barcode in fields.Where(f => f.Barcode))
        {
            var width = EstimateBarcodeWidth(SampleBarcodeLength, barcode.ModuleWidth);
            if (barcode.X + width > widthDots)
            {
                warnings.Add($"The barcode may run off the label: a {SampleBarcodeLength}-character specimen ID prints about {InchesText(width)} in wide at bar width {barcode.ModuleWidth}. Move it left, or use a narrower bar width.");
            }

            if (barcode.Y + Math.Max(barcode.FontHeight, MinBarcodeHeight) + (barcode.BarcodeText ? BarcodeTextHeight : 0) > heightDots)
            {
                warnings.Add("The barcode runs off the bottom of the label. Move it up or make it shorter.");
            }
        }

        return new SpecimenLabelDesignCheck(errors.Distinct().ToList(), warnings.Distinct().ToList());
    }

    /// <summary>Keeps every element on the label and within the printable ranges.</summary>
    public static void Normalize(int widthDots, int heightDots, List<PlacedLabelField> fields)
    {
        foreach (var field in fields)
        {
            field.X = Math.Clamp(field.X, 0, Math.Max(0, widthDots - 8));
            field.Y = Math.Clamp(field.Y, 0, Math.Max(0, heightDots - 8));
            field.FontHeight = Math.Clamp(field.FontHeight, 8, 120);
            field.ModuleWidth = Math.Clamp(field.ModuleWidth, 1, 4);
            field.Barcode = field.Barcode && field.Key == LabelFields.Accession;
            field.StaticText = string.IsNullOrWhiteSpace(field.StaticText) ? null : field.StaticText.Trim();
        }
    }

    /// <summary>A starting layout for a new design, scaled to its size.</summary>
    public static LabelLayoutDocument Starter(SpecimenLabelPurpose purpose, int widthDots, int heightDots)
    {
        if (purpose == SpecimenLabelPurpose.Tube && widthDots >= 406 && heightDots >= 203)
        {
            var tube = LabelLayoutDefaults.Specimen();
            tube.WidthDots = widthDots;
            tube.HeightDots = heightDots;
            return tube;
        }

        // A small label, top to bottom: the name; the patient ID (and the aliquot number beside it); the barcode, its
        // digits off to save room; the specimen ID as text.
        var font = Math.Clamp(heightDots / 6, 14, 28);
        var barcodeY = 4 + (2 * (font + 2));
        var barcodeHeight = Math.Max(MinBarcodeHeight, heightDots - barcodeY - font - 6);
        var fields = new List<PlacedLabelField>
        {
            new() { Key = LabelFields.PatientName, X = 6, Y = 2, FontHeight = font, Bold = true },
            new() { Key = LabelFields.Mrn, X = 6, Y = 4 + font, FontHeight = font },
            new() { Key = LabelFields.Accession, X = 6, Y = barcodeY, FontHeight = barcodeHeight, Barcode = true, ModuleWidth = 1, BarcodeText = false },
            new() { Key = LabelFields.Accession, X = 6, Y = barcodeY + barcodeHeight + 2, FontHeight = font }
        };
        if (purpose == SpecimenLabelPurpose.Aliquot)
        {
            fields.Add(new() { Key = LabelFields.AliquotNumber, X = Math.Max(6, widthDots / 2), Y = 4 + font, FontHeight = font, Bold = true });
        }

        var document = new LabelLayoutDocument { WidthDots = widthDots, HeightDots = heightDots, Page1 = fields };
        Normalize(widthDots, heightDots, document.Page1);
        return document;
    }
}
