using System.Globalization;
using BloodBankLIS.Printing.Rendering;

namespace BloodBankLIS.Printing.Templates;

/// <summary>
/// Standard compatibility tag (P-tag) layout: patient, unit, both blood types,
/// crossmatch status, expiration, and an emergency banner when the unit was released
/// uncrossmatched (docs A.4). Layout only; the data comes pre-decided from the issue
/// record. A 4x3 inch tag at 203 dpi (~812 x 609 dots).
/// </summary>
public static class CompatibilityTagTemplate
{
    public const string TemplateCode = "PTAG-STD";

    public static LabelDocument Build(CompatibilityTagModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var elements = new List<LabelElement>
        {
            new LabelText(20, 20, "COMPATIBILITY TAG", fontHeight: 40, fontWidth: 40, bold: true),
        };

        var y = 80;
        if (model.IsEmergency)
        {
            elements.Add(new LabelText(20, y, "*** EMERGENCY / UNCROSSMATCHED RELEASE ***", fontHeight: 34, fontWidth: 34, bold: true));
            y += 46;
        }

        elements.Add(new LabelText(20, y, $"Patient: {model.PatientName}", fontHeight: 32, fontWidth: 32, bold: true));
        elements.Add(new LabelText(20, y + 38, $"MRN: {model.MedicalRecordNumber}    DOB: {model.DateOfBirth:yyyy-MM-dd}"));
        elements.Add(new LabelText(20, y + 72, $"Patient ABO/Rh: {model.PatientBloodType}", fontHeight: 32, fontWidth: 32, bold: true));

        elements.Add(new LabelText(20, y + 124, $"Unit: {model.UnitNumber}", fontHeight: 32, fontWidth: 32, bold: true));
        elements.Add(new LabelBarcode(20, y + 162, model.UnitNumber, height: 70));
        elements.Add(new LabelText(20, y + 250, $"Unit ABO/Rh: {model.UnitBloodType}    Product: {model.ProductName}"));
        elements.Add(new LabelText(20, y + 284, $"Crossmatch: {model.CrossmatchMethod} / {model.CrossmatchResult}"));
        elements.Add(new LabelText(20, y + 318, $"Unit expires: {model.UnitExpiresUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC"));
        elements.Add(new LabelText(20, y + 352, $"Issued: {model.IssuedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC by {model.IssuedBy}"));

        return new LabelDocument(812, 609, elements);
    }
}
