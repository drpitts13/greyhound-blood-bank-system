using System.Globalization;
using BloodBankLIS.Printing.Rendering;

namespace BloodBankLIS.Printing.Templates;

/// <summary>
/// ISBT 128-style blood component label: DIN barcode, product code, ABO/Rh,
/// and expiration. Layout only; the print service supplies audited unit fields.
/// A 4x2 inch label at 203 dpi (~812 x 406 dots).
/// </summary>
public static class ComponentLabelTemplate
{
    public const string TemplateCode = "ISBT-COMP";

    public static LabelDocument Build(ComponentLabelModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var barcode = string.IsNullOrWhiteSpace(model.Din) ? model.UnitNumber : model.Din;
        var elements = new List<LabelElement>
        {
            new LabelText(16, 12, "BLOOD COMPONENT", fontHeight: 32, fontWidth: 32, bold: true),
            new LabelText(16, 50, model.UnitBloodType, fontHeight: 48, fontWidth: 48, bold: true),
            new LabelText(220, 58, model.ProductName, fontHeight: 28, fontWidth: 28),
            new LabelBarcode(16, 110, barcode, height: 70),
            new LabelText(16, 200, $"DIN: {model.Din ?? "—"}    Unit: {model.UnitNumber}"),
            new LabelText(16, 236, $"Product: {model.ProductCodeData ?? model.ProductName}"),
            new LabelText(16, 272, $"Expires: {model.ExpiresUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC"),
        };

        if (!string.IsNullOrWhiteSpace(model.AboRhdCode))
        {
            elements.Add(new LabelText(16, 308, $"ABO/RhD code: {model.AboRhdCode}"));
        }

        if (!string.IsNullOrWhiteSpace(model.CollectionFacility))
        {
            elements.Add(new LabelText(400, 308, model.CollectionFacility!));
        }

        return new LabelDocument(812, 406, elements);
    }
}
