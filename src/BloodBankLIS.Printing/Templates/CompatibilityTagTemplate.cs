using System.Globalization;
using BloodBankLIS.Printing.Rendering;

namespace BloodBankLIS.Printing.Templates;

/// <summary>
/// Standard compatibility tag (P-tag) layout: patient, unit, both blood types,
/// crossmatch status, expiration, and an emergency banner when the unit was released
/// uncrossmatched (docs A.4). Layout only; the data comes pre-decided from the issue
/// record. A 4x3 inch tag at 203 dpi (~812 x 609 dots). PDF places that tag on the
/// top half of a portrait letter page and a signature form on the bottom half.
/// </summary>
public static class CompatibilityTagTemplate
{
    public const string TemplateCode = "PTAG-STD";

    public const string CheckAttestation =
        "I attest that this unit was physically checked with the tag.";

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

        if (model.TestsIncomplete)
        {
            elements.Add(new LabelText(20, y, "*** TESTING INCOMPLETE — 21 CFR 606.151 ***", fontHeight: 28, fontWidth: 28, bold: true));
            y += 40;
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

        return new LabelDocument(812, 609, elements, BuildCheckForm(model));
    }

    private static LabelCheckForm BuildCheckForm(CompatibilityTagModel model)
    {
        var fields = new List<LabelCheckField>();
        if (model.IsEmergency)
        {
            fields.Add(new LabelCheckField("Release", "Emergency / uncrossmatched"));
        }

        if (model.TestsIncomplete)
        {
            fields.Add(new LabelCheckField("Testing", "Incomplete — 21 CFR 606.151"));
        }

        fields.Add(new LabelCheckField("Patient", model.PatientName));
        fields.Add(new LabelCheckField("MRN", model.MedicalRecordNumber));
        fields.Add(new LabelCheckField("Date of birth", model.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        fields.Add(new LabelCheckField("Patient ABO/Rh", model.PatientBloodType));
        fields.Add(new LabelCheckField("Unit", model.UnitNumber));
        fields.Add(new LabelCheckField("Unit ABO/Rh", model.UnitBloodType));
        fields.Add(new LabelCheckField("Product", model.ProductName));
        fields.Add(new LabelCheckField("Crossmatch", $"{model.CrossmatchMethod} / {model.CrossmatchResult}"));
        fields.Add(new LabelCheckField(
            "Unit expires",
            model.UnitExpiresUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC"));
        fields.Add(new LabelCheckField(
            "Issued",
            model.IssuedUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC by " + model.IssuedBy));

        return new LabelCheckForm(CheckAttestation, fields);
    }
}
