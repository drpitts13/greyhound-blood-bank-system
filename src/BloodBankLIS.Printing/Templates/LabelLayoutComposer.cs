using System.Globalization;
using BloodBankLIS.Application.Printing;
using BloodBankLIS.Printing.Rendering;

namespace BloodBankLIS.Printing.Templates;

/// <summary>Turns a saved label design and a data model into a <see cref="LabelDocument"/>.</summary>
public static class LabelLayoutComposer
{
    public static LabelDocument ComposeSpecimen(LabelLayoutDocument layout, SpecimenLabelModel model)
    {
        var elements = new List<LabelElement>();
        foreach (var field in layout.Page1)
        {
            Add(elements, field, barcodeAllowed: true, specimen: model, tag: null);
        }

        return new LabelDocument(
            layout.WidthDots > 0 ? layout.WidthDots : 406,
            layout.HeightDots > 0 ? layout.HeightDots : 203,
            elements);
    }

    public static LabelDocument ComposePtag(LabelLayoutDocument layout, CompatibilityTagModel model)
    {
        var page1 = new List<LabelElement>();
        foreach (var field in layout.Page1)
        {
            Add(page1, field, barcodeAllowed: true, specimen: null, tag: model);
        }

        var page2 = new List<LabelElement>();
        foreach (var field in layout.Page2)
        {
            Add(page2, field, barcodeAllowed: false, specimen: null, tag: model);
        }

        return new LabelDocument(
            layout.WidthDots > 0 ? layout.WidthDots : 812,
            layout.HeightDots > 0 ? layout.HeightDots : 609,
            page1,
            page2: page2,
            page2WidthDots: layout.Page2WidthDots > 0 ? layout.Page2WidthDots : LabelLayoutDefaults.PtagPage2Width,
            page2HeightDots: layout.Page2HeightDots > 0 ? layout.Page2HeightDots : LabelLayoutDefaults.PtagPage2Height);
    }

    private static void Add(
        List<LabelElement> elements,
        PlacedLabelField field,
        bool barcodeAllowed,
        SpecimenLabelModel? specimen,
        CompatibilityTagModel? tag)
    {
        if (field.Barcode && barcodeAllowed)
        {
            var data = BarcodeData(field.Key, specimen, tag);
            if (!string.IsNullOrWhiteSpace(data))
            {
                elements.Add(new LabelBarcode(field.X, field.Y, data, height: Math.Max(field.FontHeight, 40)));
            }

            return;
        }

        var text = Text(field, specimen, tag);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (field.Key is LabelFields.SignatureLine or LabelFields.DateLine)
        {
            elements.Add(new LabelText(field.X, field.Y, "______________________________", field.FontHeight, field.FontHeight));
            elements.Add(new LabelText(field.X, field.Y + field.FontHeight + 4, text, field.FontHeight, field.FontHeight));
            return;
        }

        elements.Add(new LabelText(field.X, field.Y, text, field.FontHeight, field.FontHeight, field.Bold));
    }

    private static string? Text(PlacedLabelField field, SpecimenLabelModel? specimen, CompatibilityTagModel? tag)
    {
        if (specimen is not null)
        {
            return field.Key switch
            {
                LabelFields.Accession => specimen.AccessionNumber,
                LabelFields.PatientName => specimen.PatientName,
                LabelFields.Mrn => "MRN: " + specimen.MedicalRecordNumber,
                LabelFields.DateOfBirth => "DOB: " + specimen.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                LabelFields.SpecimenType => specimen.SpecimenType,
                LabelFields.Collected => "Coll: " + specimen.CollectedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC",
                LabelFields.DrawLocation => specimen.DrawLocation,
                LabelFields.StaticText => field.StaticText,
                _ => null
            };
        }

        if (tag is null)
        {
            return null;
        }

        return field.Key switch
        {
            LabelFields.PatientName => "Patient: " + tag.PatientName,
            LabelFields.Mrn => "MRN: " + tag.MedicalRecordNumber,
            LabelFields.DateOfBirth => "DOB: " + tag.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            LabelFields.PatientAbo => "Patient ABO/Rh: " + tag.PatientBloodType,
            LabelFields.UnitNumber => "Unit: " + tag.UnitNumber,
            LabelFields.UnitAbo => "Unit ABO/Rh: " + tag.UnitBloodType,
            LabelFields.Product => "Product: " + tag.ProductName,
            LabelFields.Crossmatch => "Crossmatch: " + tag.CrossmatchMethod + " / " + tag.CrossmatchResult,
            LabelFields.Expires => "Unit expires: " + tag.UnitExpiresUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC",
            LabelFields.Issued => "Issued: " + tag.IssuedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC by " + tag.IssuedBy,
            LabelFields.EmergencyBanner => tag.IsEmergency ? "*** EMERGENCY / UNCROSSMATCHED RELEASE ***" : null,
            LabelFields.IncompleteBanner => tag.TestsIncomplete ? "*** TESTING INCOMPLETE — 21 CFR 606.151 ***" : null,
            LabelFields.PtagId => string.IsNullOrWhiteSpace(tag.PtagId) ? null : "P-tag ID: " + tag.PtagId,
            LabelFields.Attestation => LabelFields.CheckAttestation,
            LabelFields.SignatureLine => "Signature",
            LabelFields.DateLine => "Date",
            LabelFields.StaticText => field.StaticText,
            _ => null
        };
    }

    private static string? BarcodeData(string key, SpecimenLabelModel? specimen, CompatibilityTagModel? tag) => key switch
    {
        LabelFields.Accession => specimen?.AccessionNumber,
        LabelFields.UnitNumber => tag?.UnitNumber,
        LabelFields.PtagId => tag?.PtagId,
        _ => null
    };
}
