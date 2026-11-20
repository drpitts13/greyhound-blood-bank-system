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
        Assert.NotNull(doc.CheckForm);
        Assert.Equal(CompatibilityTagTemplate.CheckAttestation, doc.CheckForm!.Attestation);
        Assert.Contains(doc.CheckForm.Fields, f => f.Label == "Product" && f.Value == "Red Blood Cells");
        Assert.Contains(doc.CheckForm.Fields, f => f.Label == "Patient" && f.Value == "Doe, John");
        Assert.DoesNotContain(doc.CheckForm.Fields, f => f.Label.Contains("barcode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CompatibilityTag_ShowsEmergencyBannerWhenFlagged()
    {
        var model = StandardTag() with { IsEmergency = true, CrossmatchMethod = "None", CrossmatchResult = "None" };

        var doc = CompatibilityTagTemplate.Build(model);

        Assert.Contains(doc.Elements.OfType<LabelText>(), t => t.Value.Contains("EMERGENCY"));
    }

    // TEST-BB-1341 (end-user testing pass 314): with the facility's time zone a label prints the local time with its UTC
    // offset; without one it prints UTC and says so.
    [Fact]
    public void LabelTimes_AreLocalWithTheirOffset()
    {
        var utc = new DateTime(2026, 12, 6, 14, 49, 0, DateTimeKind.Utc);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");
        Assert.Equal("2026-12-06 09:49 (UTC-05:00)", LabelTime.Format(utc, zone.Id));
        Assert.Equal("2026-12-06 14:49 UTC", LabelTime.Format(utc, null));
        Assert.Equal("2026-12-06 14:49 UTC", LabelTime.Format(utc, "No/SuchZone"));

        var model = new ComponentLabelModel("U-1", null, "E0206V00", null, "O+", "Red Blood Cells", utc, null, TimeZoneId: zone.Id);
        Assert.Contains(ComponentLabelTemplate.Build(model).Elements.OfType<LabelText>(), t => t.Value == "Expires: 2026-12-06 09:49 (UTC-05:00)");
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
