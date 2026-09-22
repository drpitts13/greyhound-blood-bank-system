using System.Text;
using BloodBankLIS.Application.Printing;
using BloodBankLIS.Printing.Rendering;
using BloodBankLIS.Printing.Templates;

namespace BloodBankLIS.Printing.Tests;

public class LabelLayoutTests
{
    [Fact]
    public void DefaultSpecimenLayout_MatchesCurrentTextAndBarcodePositions()
    {
        var model = new SpecimenLabelModel(
            "ACC-555", "Doe, John", "MRN-9", new DateOnly(1980, 1, 1),
            "EDTA Whole Blood", new DateTime(2026, 5, 30, 8, 0, 0, DateTimeKind.Utc), "ED");
        var builtin = SpecimenLabelTemplate.Build(model);
        var designed = LabelLayoutComposer.ComposeSpecimen(LabelLayoutDefaults.Specimen(), model);

        Assert.Equal(builtin.WidthDots, designed.WidthDots);
        Assert.Equal(builtin.HeightDots, designed.HeightDots);
        AssertSame(builtin, designed, "Doe, John", 10, 10);
        AssertSame(builtin, designed, "EDTA Whole Blood", 10, 80);
        AssertSame(builtin, designed, "ED", 240, 80);
        var builtinBarcode = Assert.Single(builtin.Elements.OfType<LabelBarcode>());
        var designedBarcode = Assert.Single(designed.Elements.OfType<LabelBarcode>());
        Assert.Equal(builtinBarcode.Data, designedBarcode.Data);
        Assert.Equal(builtinBarcode.X, designedBarcode.X);
        Assert.Equal(builtinBarcode.Y, designedBarcode.Y);
        Assert.Contains(designed.Elements.OfType<LabelText>(), t => t.Value.Contains("MRN-9") && t.X == 10 && t.Y == 48);
        Assert.Contains(designed.Elements.OfType<LabelText>(), t => t.Value.Contains("1980-01-01") && t.X == 220 && t.Y == 48);
    }

    [Fact]
    public void DefaultPtagLayout_MatchesCurrentTagPositions_AndPage2HasNoBarcode()
    {
        var model = StandardTag();
        var builtin = CompatibilityTagTemplate.Build(model);
        var designed = LabelLayoutComposer.ComposePtag(LabelLayoutDefaults.Ptag(), model);

        Assert.Equal(812, designed.WidthDots);
        Assert.Equal(609, designed.HeightDots);
        Assert.Empty(designed.Page2Elements.OfType<LabelBarcode>());
        Assert.Contains(designed.Page2Elements.OfType<LabelText>(), t => t.Value == LabelFields.CheckAttestation);
        Assert.Contains(designed.Page2Elements.OfType<LabelText>(), t => t.Value == "Signature");
        Assert.Contains(designed.Page2Elements.OfType<LabelText>(), t => t.Value == "Date");
        AssertSame(builtin, designed, "COMPATIBILITY TAG", 20, 20);
        AssertSame(builtin, designed, "Patient: Doe, John", 20, 80);
        AssertSame(builtin, designed, "Patient ABO/Rh: O+", 20, 152);
        AssertSame(builtin, designed, "Unit: W12345", 20, 204);
        var barcode = Assert.Single(designed.Elements.OfType<LabelBarcode>());
        Assert.Equal(20, barcode.X);
        Assert.Equal(242, barcode.Y);
        Assert.Equal("W12345", barcode.Data);
    }

    [Fact]
    public void SavedLayout_IsWhatPdfAndZplRender_AndPage2CannotCarryABarcode()
    {
        var layout = new LabelLayoutDocument
        {
            WidthDots = 812,
            HeightDots = 609,
            Page2WidthDots = LabelLayoutDefaults.PtagPage2Width,
            Page2HeightDots = LabelLayoutDefaults.PtagPage2Height,
            Page1 =
            [
                new PlacedLabelField { Key = LabelFields.StaticText, X = 50, Y = 60, FontHeight = 28, StaticText = "CUSTOM-FIELD" },
                new PlacedLabelField { Key = LabelFields.UnitNumber, X = 80, Y = 100, FontHeight = 28, Barcode = true }
            ],
            Page2 =
            [
                new PlacedLabelField { Key = LabelFields.Attestation, X = 40, Y = 30, FontHeight = 24 },
                new PlacedLabelField { Key = LabelFields.UnitNumber, X = 40, Y = 80, FontHeight = 24, Barcode = true }
            ]
        };

        var document = LabelLayoutComposer.ComposePtag(layout, StandardTag());
        Assert.Contains(document.Elements.OfType<LabelText>(), t => t.Value == "CUSTOM-FIELD" && t.X == 50 && t.Y == 60);
        Assert.Contains(document.Elements.OfType<LabelBarcode>(), b => b.Data == "W12345" && b.X == 80 && b.Y == 100);
        Assert.Empty(document.Page2Elements.OfType<LabelBarcode>());
        Assert.Contains(document.Page2Elements.OfType<LabelText>(), t => t.Value.Contains("W12345"));

        var zpl = new ZplLabelRenderer().Render(document).Text!;
        Assert.Contains("^FO50,60", zpl);
        Assert.Contains("^FO80,100", zpl);
        Assert.DoesNotContain(LabelFields.CheckAttestation, zpl);

        var pdf = new PdfLabelRenderer().Render(document).Pdf!;
        var latin = Encoding.Latin1.GetString(pdf);
        Assert.Contains("CUSTOM-FIELD", latin);
        Assert.Contains(LabelFields.CheckAttestation, latin);
        Assert.Contains("0 0 612 792", latin);
    }

    private static void AssertSame(LabelDocument builtin, LabelDocument designed, string value, int x, int y)
    {
        var left = Assert.Single(builtin.Elements.OfType<LabelText>(), t => t.Value == value);
        var right = Assert.Single(designed.Elements.OfType<LabelText>(), t => t.Value == value);
        Assert.Equal(x, left.X);
        Assert.Equal(y, left.Y);
        Assert.Equal(left.X, right.X);
        Assert.Equal(left.Y, right.Y);
        Assert.Equal(left.Bold, right.Bold);
    }

    private static CompatibilityTagModel StandardTag() => new(
        PatientName: "Doe, John",
        MedicalRecordNumber: "MRN-9",
        DateOfBirth: new DateOnly(1980, 1, 1),
        PatientBloodType: "O+",
        UnitNumber: "W12345",
        UnitBloodType: "O-",
        ProductName: "Red Blood Cells",
        CrossmatchMethod: "Serologic",
        CrossmatchResult: "Compatible",
        UnitExpiresUtc: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
        IssuedUtc: new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc),
        IssuedBy: "tech",
        IsEmergency: false);
}
