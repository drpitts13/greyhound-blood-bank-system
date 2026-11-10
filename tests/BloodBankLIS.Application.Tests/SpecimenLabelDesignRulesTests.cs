using BloodBankLIS.Application.Printing;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Tests;

/// <summary>Specimen label design rules (Cycle 657): what a design must hold, what warns, sizes, and the starters.</summary>
public class SpecimenLabelDesignRulesTests
{
    private static PlacedLabelField F(string key, int x = 10, int y = 10, bool barcode = false, int font = 24, int module = 2, string? text = null) =>
        new() { Key = key, X = x, Y = y, Barcode = barcode, FontHeight = font, ModuleWidth = module, StaticText = text };

    // TEST-BB-816: a design must carry the specimen ID as a barcode; an empty design, blank static text, and the aliquot
    // number on a tube design are refused.
    [Fact]
    public void Check_RefusesWhatCannotPrintOrScan()
    {
        Assert.Contains("Place at least one element on the label.", SpecimenLabelDesignRules.Check(SpecimenLabelPurpose.Tube, 406, 203, []).Errors);

        var noBarcode = SpecimenLabelDesignRules.Check(SpecimenLabelPurpose.Tube, 406, 203,
            [F(LabelFields.Accession), F(LabelFields.PatientName), F(LabelFields.Mrn)]);
        Assert.Contains("Place the specimen ID as a barcode so the tube can be scanned.", noBarcode.Errors);

        var bad = SpecimenLabelDesignRules.Check(SpecimenLabelPurpose.Tube, 406, 203,
            [F(LabelFields.Accession, barcode: true), F(LabelFields.StaticText, text: " "), F(LabelFields.AliquotNumber)]);
        Assert.Contains("Static text needs the words to print.", bad.Errors);
        Assert.Contains("The aliquot number belongs on an aliquot label design.", bad.Errors);
        Assert.False(bad.Ok);
    }

    // TEST-BB-817: fewer than two patient identifiers warns (OCD-080), as do an aliquot design without the aliquot number
    // and a barcode that will not fit across or down the label; a complete design has neither errors nor warnings.
    [Fact]
    public void Check_WarnsAboutIdentifiersAliquotNumberAndBarcodeFit()
    {
        var oneId = SpecimenLabelDesignRules.Check(SpecimenLabelPurpose.Aliquot, 305, 102,
            [F(LabelFields.Accession, x: 6, y: 40, barcode: true, font: 40, module: 1), F(LabelFields.PatientName)]);
        Assert.True(oneId.Ok);
        Assert.Contains(oneId.Warnings, w => w.Contains("Fewer than two patient identifiers") && w.Contains("OCD-080"));
        Assert.Contains("The aliquot number is not on the label, so aliquots of one specimen will look alike.", oneId.Warnings);

        var wide = SpecimenLabelDesignRules.Check(SpecimenLabelPurpose.Tube, 305, 102,
            [F(LabelFields.Accession, x: 6, y: 90, barcode: true, font: 40, module: 2), F(LabelFields.PatientName), F(LabelFields.Mrn)]);
        Assert.Contains(wide.Warnings, w => w.StartsWith("The barcode may run off the label"));
        Assert.Contains("The barcode runs off the bottom of the label. Move it up or make it shorter.", wide.Warnings);

        var good = SpecimenLabelDesignRules.Check(SpecimenLabelPurpose.Tube, 406, 203, LabelLayoutDefaults.Specimen().Page1);
        Assert.Empty(good.Errors);
        Assert.Empty(good.Warnings);
    }

    // TEST-BB-818: sizes convert at 203 dpi and are bounded; a Code 128 barcode's width grows with its bar width.
    [Fact]
    public void Sizes_AndBarcodeWidth()
    {
        Assert.Equal((406, 203), (SpecimenLabelDesignRules.ToDots(2m), SpecimenLabelDesignRules.ToDots(1m)));
        Assert.Equal((305, 102), (SpecimenLabelDesignRules.StockSizes.Single(s => s.Label.StartsWith("1.5")).WidthDots,
            SpecimenLabelDesignRules.StockSizes.Single(s => s.Label.StartsWith("1.5")).HeightDots));
        Assert.Null(SpecimenLabelDesignRules.SizeError(1.5m, 0.5m));
        Assert.NotNull(SpecimenLabelDesignRules.SizeError(0.25m, 0.5m));
        Assert.NotNull(SpecimenLabelDesignRules.SizeError(2m, 4m));
        Assert.Equal(167, SpecimenLabelDesignRules.EstimateBarcodeWidth(12, 1));
        Assert.Equal(334, SpecimenLabelDesignRules.EstimateBarcodeWidth(12, 2));
    }

    // TEST-BB-819: the starter layouts fit their labels: the 2 × 1 tube starter is the built-in layout; the 1.5 × 0.5
    // aliquot starter carries the barcode, two identifiers, the specimen ID as text and the aliquot number, with no
    // errors or warnings; a 1 × 0.5 starter keeps everything on the label.
    [Fact]
    public void Starters_FitTheirLabels()
    {
        Assert.Equal(LabelLayoutDefaults.Specimen().Page1.Count, SpecimenLabelDesignRules.Starter(SpecimenLabelPurpose.Tube, 406, 203).Page1.Count);

        var aliquot = SpecimenLabelDesignRules.Starter(SpecimenLabelPurpose.Aliquot, 305, 102);
        var check = SpecimenLabelDesignRules.Check(SpecimenLabelPurpose.Aliquot, 305, 102, aliquot.Page1);
        Assert.Empty(check.Errors);
        Assert.Empty(check.Warnings);
        Assert.Contains(aliquot.Page1, f => f.Key == LabelFields.AliquotNumber);
        Assert.Contains(aliquot.Page1, f => f.Key == LabelFields.Accession && !f.Barcode);

        var tiny = SpecimenLabelDesignRules.Starter(SpecimenLabelPurpose.Tube, 203, 102);
        Assert.All(tiny.Page1, f => Assert.True(f.X < 203 && f.Y < 102));
        Assert.DoesNotContain(tiny.Page1, f => f.Key == LabelFields.AliquotNumber);
    }

    // TEST-BB-820: normalizing keeps elements on the label and in range, and only the specimen ID may be a barcode.
    [Fact]
    public void Normalize_KeepsElementsOnTheLabel()
    {
        var fields = new List<PlacedLabelField>
        {
            F(LabelFields.PatientName, x: 900, y: -5, barcode: true, font: 500, module: 9),
            F(LabelFields.StaticText, text: "  Keep cold  ")
        };
        SpecimenLabelDesignRules.Normalize(305, 102, fields);
        Assert.Equal((297, 0, false, 120, 4), (fields[0].X, fields[0].Y, fields[0].Barcode, fields[0].FontHeight, fields[0].ModuleWidth));
        Assert.Equal("Keep cold", fields[1].StaticText);
    }

    // TEST-BB-835: elements whose estimated printed boxes overlap warn, naming both (the barcode by name, static text by
    // its words); text that may run off the right edge with typical data warns; elements side by side, or touching
    // within the tolerance, do not.
    [Fact]
    public void Check_WarnsAboutOverlapsAndTextOffTheEdge()
    {
        var clash = SpecimenLabelDesignRules.Check(SpecimenLabelPurpose.Tube, 406, 203,
        [
            F(LabelFields.Accession, x: 10, y: 112, barcode: true, font: 40),
            F(LabelFields.PatientName, x: 10, y: 10, font: 28),
            F(LabelFields.Mrn, x: 20, y: 20, font: 28),
            F(LabelFields.StaticText, x: 30, y: 130, font: 20, text: "KEEP COLD")
        ]);
        Assert.Contains("Patient name and Patient ID overlap. Move one of them.", clash.Warnings);
        Assert.Contains("The Specimen ID barcode and \"KEEP COLD\" overlap. Move one of them.", clash.Warnings);
        Assert.Equal([(0, 3), (1, 2)], SpecimenLabelDesignRules.Overlaps(
        [
            F(LabelFields.Accession, x: 10, y: 112, barcode: true, font: 40),
            F(LabelFields.PatientName, x: 10, y: 10, font: 28),
            F(LabelFields.Mrn, x: 20, y: 20, font: 28),
            F(LabelFields.StaticText, x: 30, y: 130, font: 20, text: "KEEP COLD")
        ]).OrderBy(p => p.A).ToArray());

        var offEdge = SpecimenLabelDesignRules.Check(SpecimenLabelPurpose.Tube, 406, 203,
            [F(LabelFields.Accession, barcode: true, y: 100, font: 40), F(LabelFields.Collected, x: 300, y: 10), F(LabelFields.PatientName, y: 40), F(LabelFields.Mrn, y: 70)]);
        Assert.Contains("Collection time may run off the right edge with typical data. Move it left or use a smaller font.", offEdge.Warnings);

        // Side by side on one line, and one under the other touching within the tolerance: no warning.
        Assert.Empty(SpecimenLabelDesignRules.Overlaps([F(LabelFields.Mrn, x: 10, y: 48, font: 28), F(LabelFields.DateOfBirth, x: 220, y: 48, font: 28)]));
        Assert.Empty(SpecimenLabelDesignRules.Overlaps([F(LabelFields.PatientName, x: 10, y: 10, font: 30), F(LabelFields.Mrn, x: 10, y: 34, font: 28)]));
    }

}
