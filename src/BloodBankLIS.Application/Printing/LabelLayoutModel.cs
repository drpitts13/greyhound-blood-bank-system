using System.Text.Json;

namespace BloodBankLIS.Application.Printing;

/// <summary>Placed field on a label canvas. Coordinates are dots at 203 dpi.</summary>
public sealed class PlacedLabelField
{
    public string Key { get; set; } = string.Empty;

    public int X { get; set; }

    public int Y { get; set; }

    public int FontHeight { get; set; } = 28;

    public bool Bold { get; set; }

    public bool Barcode { get; set; }

    public string? StaticText { get; set; }
}

/// <summary>
/// One label design. Specimen uses <see cref="Page1"/> only.
/// P-tag page 1 is the 4×3 tag; page 2 is the bottom half of the portrait sheet.
/// </summary>
public sealed class LabelLayoutDocument
{
    public int WidthDots { get; set; }

    public int HeightDots { get; set; }

    public int Page2WidthDots { get; set; }

    public int Page2HeightDots { get; set; }

    public List<PlacedLabelField> Page1 { get; set; } = [];

    public List<PlacedLabelField> Page2 { get; set; } = [];
}

public sealed record LabelFieldOption(string Key, string Label, bool AllowBarcode);

public static class LabelFields
{
    public const string Accession = "Accession";
    public const string PatientName = "PatientName";
    public const string Mrn = "Mrn";
    public const string DateOfBirth = "DateOfBirth";
    public const string SpecimenType = "SpecimenType";
    public const string Collected = "Collected";
    public const string DrawLocation = "DrawLocation";
    public const string StaticText = "StaticText";
    public const string PatientAbo = "PatientAbo";
    public const string UnitNumber = "UnitNumber";
    public const string UnitAbo = "UnitAbo";
    public const string Product = "Product";
    public const string Crossmatch = "Crossmatch";
    public const string Expires = "Expires";
    public const string Issued = "Issued";
    public const string EmergencyBanner = "EmergencyBanner";
    public const string IncompleteBanner = "IncompleteBanner";
    public const string PtagId = "PtagId";
    public const string Attestation = "Attestation";
    public const string SignatureLine = "SignatureLine";
    public const string DateLine = "DateLine";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static IReadOnlyList<LabelFieldOption> SpecimenPalette() =>
    [
        new(Accession, "Specimen ID", true),
        new(PatientName, "Patient name", false),
        new(Mrn, "Patient ID", false),
        new(DateOfBirth, "Date of birth", false),
        new(SpecimenType, "Specimen type", false),
        new(Collected, "Collection time", false),
        new(DrawLocation, "Draw location", false),
        new(StaticText, "Static text", false)
    ];

    public static IReadOnlyList<LabelFieldOption> PtagPalette(int page)
    {
        var barcode = page == 1;
        var fields = new List<LabelFieldOption>
        {
            new(PatientName, "Patient name", false),
            new(Mrn, "Patient ID", false),
            new(DateOfBirth, "Date of birth", false),
            new(PatientAbo, "Patient ABO/Rh", false),
            new(UnitNumber, "Unit number", barcode),
            new(UnitAbo, "Unit ABO/Rh", false),
            new(Product, "Product", false),
            new(Crossmatch, "Crossmatch", false),
            new(Expires, "Unit expires", false),
            new(Issued, "Issued by", false),
            new(PtagId, "P-tag ID", barcode),
            new(EmergencyBanner, "Emergency banner", false),
            new(IncompleteBanner, "Incomplete testing banner", false),
            new(StaticText, "Static text", false)
        };
        if (page == 2)
        {
            fields.Add(new(Attestation, "Attestation", false));
            fields.Add(new(SignatureLine, "Signature line", false));
            fields.Add(new(DateLine, "Date line", false));
        }
        else
        {
            fields.Insert(0, new(StaticText, "Static text", false));
        }

        return fields
            .GroupBy(f => f.Key)
            .Select(g => g.First())
            .ToList();
    }

    public static bool IsKnown(string key) =>
        SpecimenPalette().Any(f => f.Key == key)
        || key is PatientAbo or UnitNumber or UnitAbo or Product or Crossmatch or Expires or Issued
            or EmergencyBanner or IncompleteBanner or PtagId or Attestation or SignatureLine or DateLine;

    public static string Caption(string key) => key switch
    {
        Accession => "Specimen ID",
        PatientName => "Patient name",
        Mrn => "Patient ID",
        DateOfBirth => "Date of birth",
        SpecimenType => "Specimen type",
        Collected => "Collection time",
        DrawLocation => "Draw location",
        PatientAbo => "Patient ABO/Rh",
        UnitNumber => "Unit number",
        UnitAbo => "Unit ABO/Rh",
        Product => "Product",
        Crossmatch => "Crossmatch",
        Expires => "Unit expires",
        Issued => "Issued by",
        EmergencyBanner => "Emergency banner",
        IncompleteBanner => "Incomplete testing",
        PtagId => "P-tag ID",
        Attestation => "Attestation",
        SignatureLine => "Signature",
        DateLine => "Date",
        StaticText => "Text",
        _ => key
    };

    public static bool RequiresPtagId(LabelLayoutDocument? document) =>
        document is not null && document.Page1.Concat(document.Page2).Any(f => f.Key == PtagId);

    public const string CheckAttestation =
        "I attest that this unit was physically checked with the tag.";
}

public static class LabelLayoutDefaults
{
    public const int PtagPage2Width = 1726;
    public const int PtagPage2Height = 1117;

    public static LabelLayoutDocument Specimen() => new()
    {
        WidthDots = 406,
        HeightDots = 203,
        Page1 =
        [
            Field(LabelFields.PatientName, 10, 10, 30, true),
            Field(LabelFields.Mrn, 10, 48, 28, false),
            Field(LabelFields.DateOfBirth, 220, 48, 28, false),
            Field(LabelFields.SpecimenType, 10, 80, 28, false),
            Field(LabelFields.DrawLocation, 240, 80, 28, false),
            Field(LabelFields.Accession, 10, 112, 28, false, barcode: true),
            Field(LabelFields.Collected, 10, 178, 28, false)
        ]
    };

    public static LabelLayoutDocument Ptag() => new()
    {
        WidthDots = 812,
        HeightDots = 609,
        Page2WidthDots = PtagPage2Width,
        Page2HeightDots = PtagPage2Height,
        Page1 =
        [
            Field(LabelFields.StaticText, 20, 20, 40, true, text: "COMPATIBILITY TAG"),
            Field(LabelFields.PatientName, 20, 80, 32, true),
            Field(LabelFields.Mrn, 20, 118, 28, false),
            Field(LabelFields.DateOfBirth, 280, 118, 28, false),
            Field(LabelFields.PatientAbo, 20, 152, 32, true),
            Field(LabelFields.UnitNumber, 20, 204, 32, true),
            Field(LabelFields.UnitNumber, 20, 242, 28, false, barcode: true),
            Field(LabelFields.UnitAbo, 20, 330, 28, false),
            Field(LabelFields.Product, 360, 330, 28, false),
            Field(LabelFields.Crossmatch, 20, 364, 28, false),
            Field(LabelFields.Expires, 20, 398, 28, false),
            Field(LabelFields.Issued, 20, 432, 28, false)
        ],
        Page2 =
        [
            Field(LabelFields.Attestation, 48, 36, 28, false),
            Field(LabelFields.PatientName, 48, 110, 32, true),
            Field(LabelFields.Mrn, 48, 160, 28, false),
            Field(LabelFields.DateOfBirth, 48, 210, 28, false),
            Field(LabelFields.PatientAbo, 48, 260, 32, true),
            Field(LabelFields.UnitNumber, 48, 320, 32, true),
            Field(LabelFields.UnitAbo, 48, 370, 28, false),
            Field(LabelFields.Product, 48, 420, 28, false),
            Field(LabelFields.Crossmatch, 48, 470, 28, false),
            Field(LabelFields.Expires, 48, 520, 28, false),
            Field(LabelFields.Issued, 48, 570, 28, false),
            Field(LabelFields.SignatureLine, 48, 780, 28, false),
            Field(LabelFields.DateLine, 900, 780, 28, false)
        ]
    };

    private static PlacedLabelField Field(
        string key, int x, int y, int font, bool bold, bool barcode = false, string? text = null) =>
        new()
        {
            Key = key,
            X = x,
            Y = y,
            FontHeight = font,
            Bold = bold,
            Barcode = barcode,
            StaticText = text
        };
}
