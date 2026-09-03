using BloodBankLIS.Printing.Rendering;
using BloodBankLIS.Printing.Templates;

namespace BloodBankLIS.Printing.Tests;

public class TemplateTests
{
    [Fact]
    public void SpecimenLabel_IncludesAccessionBarcodeAndIdentifiers()
    {
        var model = new SpecimenLabelModel(
            "ACC-555", "Doe, John", "MRN-9", new DateOnly(1980, 1, 1),
            "EDTA Whole Blood", new DateTime(2026, 5, 30, 8, 0, 0, DateTimeKind.Utc), "ED");

        var doc = SpecimenLabelTemplate.Build(model);

        Assert.Contains(doc.Elements.OfType<LabelBarcode>(), b => b.Data == "ACC-555");
        Assert.Contains(doc.Elements.OfType<LabelText>(), t => t.Value.Contains("Doe, John"));
        Assert.Contains(doc.Elements.OfType<LabelText>(), t => t.Value.Contains("MRN-9"));
    }

    [Fact]
    public void CompatibilityTag_ShowsBothBloodTypesAndCrossmatch()
    {
        var model = StandardTag();

        var doc = CompatibilityTagTemplate.Build(model);
        var texts = doc.Elements.OfType<LabelText>().Select(t => t.Value).ToList();

        Assert.Contains(texts, t => t.Contains("Patient ABO/Rh: O+"));
        Assert.Contains(texts, t => t.Contains("Unit ABO/Rh: O-"));
        Assert.Contains(texts, t => t.Contains("Serologic"));
        Assert.Contains(texts, t => t.Contains("Compatible"));
        Assert.DoesNotContain(texts, t => t.Contains("EMERGENCY"));
        Assert.Contains(doc.Elements.OfType<LabelBarcode>(), b => b.Data == "W12345");
    }

    [Fact]
    public void CompatibilityTag_ShowsEmergencyBannerWhenFlagged()
    {
        var model = StandardTag() with { IsEmergency = true, CrossmatchMethod = "None", CrossmatchResult = "None" };

        var doc = CompatibilityTagTemplate.Build(model);

        Assert.Contains(doc.Elements.OfType<LabelText>(), t => t.Value.Contains("EMERGENCY"));
    }

    [Fact]
    public void ComponentLabel_IncludesDinBarcodeAboAndExpiration()
    {
        var model = new ComponentLabelModel(
            UnitNumber: "U-COMP-1",
            Din: "W000011234567",
            ProductCodeData: "E0206V00",
            AboRhdCode: "62",
            UnitBloodType: "O+",
            ProductName: "Red Blood Cells",
            ExpiresUtc: new DateTime(2026, 6, 30, 23, 59, 0, DateTimeKind.Utc),
            CollectionFacility: "W0000");

        var doc = ComponentLabelTemplate.Build(model);
        var texts = doc.Elements.OfType<LabelText>().Select(t => t.Value).ToList();

        Assert.Contains(doc.Elements.OfType<LabelBarcode>(), b => b.Data == "W000011234567");
        Assert.Contains(texts, t => t.Contains("O+"));
        Assert.Contains(texts, t => t.Contains("E0206V00"));
        Assert.Contains(texts, t => t.Contains("2026-06-30"));
        Assert.Contains(texts, t => t.Contains("Red Blood Cells"));
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
