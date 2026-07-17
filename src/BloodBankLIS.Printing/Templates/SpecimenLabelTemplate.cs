using System.Globalization;
using BloodBankLIS.Printing.Rendering;

namespace BloodBankLIS.Printing.Templates;

/// <summary>
/// Standard specimen label layout: accession barcode + patient identifiers and
/// collection details. Layout only; no business rules (docs A.1-A.2). A 2x1 inch
/// label at 203 dpi (~406 x 203 dots).
/// </summary>
public static class SpecimenLabelTemplate
{
    public const string TemplateCode = "SPEC-STD";

    public static LabelDocument Build(SpecimenLabelModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var elements = new List<LabelElement>
        {
            new LabelText(10, 10, model.PatientName, fontHeight: 30, fontWidth: 30, bold: true),
            new LabelText(10, 48, $"MRN: {model.MedicalRecordNumber}   DOB: {model.DateOfBirth:yyyy-MM-dd}"),
            new LabelText(10, 80, model.SpecimenType),
            new LabelBarcode(10, 112, model.AccessionNumber, height: 50),
            new LabelText(10, 178, $"Coll: {model.CollectedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC"),
        };

        if (!string.IsNullOrWhiteSpace(model.DrawLocation))
        {
            elements.Add(new LabelText(240, 80, model.DrawLocation!));
        }

        return new LabelDocument(406, 203, elements);
    }
}
