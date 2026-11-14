using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Printing;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Components.Pages;
using BloodBankLIS.Web.Components.Pages.Admin;
using BloodBankLIS.Web.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BloodBankLIS.Help.Tests;

/// <summary>The specimen label designer (Cycle 657): designs listed, a design laid out on its canvas, and printing on one.</summary>
public class SpecimenLabelDesignerTests : BunitContext
{
    private static SpecimenLabelDesignListItemDto Item(long id, string code, SpecimenLabelPurpose purpose, bool isDefault, bool active = true) =>
        new(id, code, code + " design", purpose, purpose == SpecimenLabelPurpose.Aliquot ? 1.5m : 2m, purpose == SpecimenLabelPurpose.Aliquot ? 0.5m : 1m,
            isDefault, active, 1, 5);

    private static SpecimenLabelDesignDto Design(SpecimenLabelPurpose purpose, List<PlacedLabelField> fields) =>
        new(7, "CRYO", "Cryovial", purpose, 305, 102, false, true, 3,
            new LabelLayoutDocument { WidthDots = 305, HeightDots = 102, Page1 = fields }, []);

    private IRenderedComponent<AdminSpecimenLabels> OpenDesign(SpecimenLabelDesignDto design)
    {
        CannedApi.Register(this, new Dictionary<string, object> { ["/api/admin/specimen-label-designs/7"] = design });
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/specimen-labels?id=7");
        var page = Render<AdminSpecimenLabels>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-label-canvas]")));
        return page;
    }

    // TEST-BB-827: the list shows each design with its purpose, size and status; Make default is offered on an active
    // design that is not the default, and neither Make default nor Deactivate on the default.
    [Fact]
    public void List_ShowsDesignsAndTheirActions()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/specimen-label-designs"] = new List<SpecimenLabelDesignListItemDto>
            {
                Item(1, "TUBE-STD", SpecimenLabelPurpose.Tube, true),
                Item(2, "TUBE-WIDE", SpecimenLabelPurpose.Tube, false),
                Item(3, "ALIQ-STD", SpecimenLabelPurpose.Aliquot, true)
            }
        });
        var page = Render<AdminSpecimenLabels>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("[data-design]").Count));

        var standard = page.Find("[data-design='TUBE-STD']");
        Assert.Contains("Default", standard.TextContent);
        Assert.Contains("2 × 1 in", standard.TextContent);
        Assert.DoesNotContain(standard.QuerySelectorAll("button"), b => b.TextContent.Trim() is "Make default" or "Deactivate");
        var wide = page.Find("[data-design='TUBE-WIDE']");
        Assert.Contains(wide.QuerySelectorAll("button"), b => b.TextContent.Trim() == "Make default");
        Assert.Contains("Aliquot", page.Find("[data-design='ALIQ-STD']").TextContent);
        Assert.NotNull(page.Find("[data-new-design]"));
    }

    // TEST-BB-828: a design opens on its canvas at scale with sample data; a barcode is drawn as wide as it prints; the
    // aliquot number is offered on an aliquot design; adding an element places it and selects it.
    [Fact]
    public void Design_OpensOnItsCanvas()
    {
        var page = OpenDesign(Design(SpecimenLabelPurpose.Aliquot,
        [
            new() { Key = LabelFields.Accession, X = 6, Y = 42, FontHeight = 40, Barcode = true, ModuleWidth = 1, BarcodeText = false },
            new() { Key = LabelFields.PatientName, X = 6, Y = 2, FontHeight = 17 },
            new() { Key = LabelFields.Mrn, X = 6, Y = 21, FontHeight = 17 }
        ]));

        var canvas = page.Find("[data-label-canvas]");
        Assert.Contains("width:560px", canvas.GetAttribute("style"));
        var barcode = page.Find("[data-placed='Accession']");
        Assert.Contains("barcode", barcode.ClassName);
        Assert.Contains("width:306.6px", barcode.GetAttribute("style"));
        Assert.Contains("DOE, JANE", page.Find("[data-placed='PatientName']").TextContent);
        Assert.NotNull(page.Find("[data-palette='AliquotNumber']"));

        page.Find("[data-palette='AliquotNumber']").Click();
        Assert.Contains("Aliquot 2 of 3", page.Find("[data-placed='AliquotNumber']").TextContent);
        Assert.Equal("Aliquot number", page.Find("[data-selected-caption]").TextContent.Trim());
    }

    // TEST-BB-829: the arrow keys move the selected element (Shift: 10 dots) and stay on the label; X/Y typed are kept on
    // the label; Delete removes it.
    [Fact]
    public void Keyboard_MovesAndRemovesTheSelectedElement()
    {
        var page = OpenDesign(Design(SpecimenLabelPurpose.Tube,
        [
            new() { Key = LabelFields.Accession, X = 6, Y = 42, FontHeight = 40, Barcode = true, ModuleWidth = 1 },
            new() { Key = LabelFields.PatientName, X = 6, Y = 2, FontHeight = 17 }
        ]));

        page.Find("[data-placed='PatientName']").PointerDown(new Microsoft.AspNetCore.Components.Web.PointerEventArgs { ClientX = 0, ClientY = 0 });
        page.Find("[data-label-canvas]").PointerUp();
        var canvas = page.Find("[data-label-canvas]");
        canvas.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowRight" });
        page.Find("[data-label-canvas]").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowDown", ShiftKey = true });
        page.Find("[data-label-canvas]").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowUp", ShiftKey = true });
        page.Find("[data-label-canvas]").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowUp", ShiftKey = true });
        Assert.Equal(("7", "0"), (page.Find("[data-selected-x]").GetAttribute("value"), page.Find("[data-selected-y]").GetAttribute("value")));

        page.Find("[data-selected-x]").Change("5000");
        Assert.Equal("297", page.Find("[data-selected-x]").GetAttribute("value"));

        page.Find("[data-label-canvas]").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Delete" });
        Assert.Empty(page.FindAll("[data-placed='PatientName']"));
    }

    // TEST-BB-830: the design is checked as it is laid out: without the specimen ID barcode Save is disabled and the error
    // shown; turning the barcode on clears that error, but a tube label with one patient identifier still cannot be saved
    // (OCD-080, decided 2026-11-10).
    [Fact]
    public void Design_IsCheckedAsItIsLaidOut()
    {
        var page = OpenDesign(Design(SpecimenLabelPurpose.Tube,
            [new() { Key = LabelFields.Accession, X = 6, Y = 42, FontHeight = 24 }, new() { Key = LabelFields.PatientName, X = 6, Y = 2, FontHeight = 17 }]));
        Assert.True(page.Find("[data-save-design]").HasAttribute("disabled"));
        Assert.Contains("Place the specimen ID as a barcode", page.Find("[data-design-check]").TextContent);

        page.Find("[data-placed='Accession']").PointerDown(new Microsoft.AspNetCore.Components.Web.PointerEventArgs());
        page.Find("[data-label-canvas]").PointerUp();
        page.Find("[data-selected-barcode]").Change(true);
        Assert.DoesNotContain("Place the specimen ID as a barcode", page.Find("[data-design-check]").TextContent);
        Assert.Contains(SpecimenLabelDesignRules.TwoIdentifiersError, page.Find("[data-design-check]").TextContent);
        Assert.True(page.Find("[data-save-design]").HasAttribute("disabled"));
    }

    // TEST-BB-834: the designer offers a test print of the layout on the canvas: shown as a PDF here by default, or as
    // the ZPL; it is unavailable while the design has errors.
    [Fact]
    public void TestPrint_ShowsTheLabel()
    {
        var design = Design(SpecimenLabelPurpose.Aliquot,
        [
            new() { Key = LabelFields.Accession, X = 6, Y = 42, FontHeight = 40, Barcode = true, ModuleWidth = 1, BarcodeText = false },
            new() { Key = LabelFields.PatientName, X = 6, Y = 2, FontHeight = 17 },
            new() { Key = LabelFields.Mrn, X = 6, Y = 21, FontHeight = 17 },
            new() { Key = LabelFields.AliquotNumber, X = 150, Y = 21, FontHeight = 17 }
        ]);
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/admin/specimen-label-designs/7"] = design,
            ["/api/admin/specimen-label-designs/7/test-print"] = new SpecimenLabelTestPrintResultVm(LabelFormat.Pdf, null, "JVBERi0xLjQK", null)
        });
        Services.GetRequiredService<NavigationManager>().NavigateTo("/admin/specimen-labels?id=7");
        var page = Render<AdminSpecimenLabels>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-test-print]")));
        Assert.Contains("TESTLABEL01", page.Find("[data-test-print]").TextContent);

        page.Find("[data-test-print-button]").Click();
        page.WaitForAssertion(() => Assert.Equal("data:application/pdf;base64,JVBERi0xLjQK", page.Find("[data-test-pdf]").GetAttribute("src")));
        Assert.Contains("Test label rendered.", page.Markup);

        // Without the specimen ID barcode the design has an error, and there is nothing to test.
        page.Find("[data-placed='Accession']").PointerDown(new Microsoft.AspNetCore.Components.Web.PointerEventArgs());
        page.Find("[data-label-canvas]").PointerUp();
        page.Find("[data-selected-barcode]").Change(false);
        Assert.True(page.Find("[data-test-print-button]").HasAttribute("disabled"));
    }

    // TEST-BB-836: overlapping elements are outlined on the canvas and named in the check; moving one clear removes both.
    [Fact]
    public void Overlap_IsOutlinedUntilMovedClear()
    {
        var page = OpenDesign(Design(SpecimenLabelPurpose.Tube,
        [
            new() { Key = LabelFields.Accession, X = 6, Y = 50, FontHeight = 40, Barcode = true, ModuleWidth = 1, BarcodeText = false },
            new() { Key = LabelFields.PatientName, X = 6, Y = 2, FontHeight = 17 },
            new() { Key = LabelFields.Mrn, X = 10, Y = 6, FontHeight = 17 }
        ]));
        Assert.Contains("overlap", page.Find("[data-placed='PatientName']").ClassName);
        Assert.Contains("overlap", page.Find("[data-placed='Mrn']").ClassName);
        Assert.DoesNotContain("overlap", page.Find("[data-placed='Accession']").ClassName);
        Assert.Contains("Patient name and Patient ID overlap", page.Find("[data-design-check]").TextContent);

        page.Find("[data-placed='Mrn']").PointerDown(new Microsoft.AspNetCore.Components.Web.PointerEventArgs());
        page.Find("[data-label-canvas]").PointerUp();
        page.Find("[data-selected-y]").Change("24");
        Assert.DoesNotContain("overlap", page.Find("[data-placed='Mrn']").ClassName);
        Assert.DoesNotContain("overlap", page.FindAll("[data-design-check]").FirstOrDefault()?.TextContent ?? string.Empty);
    }

    // TEST-BB-831: the Printing page offers the active designs, the default tube design first chosen; choosing an aliquot
    // design asks how many and says so on the button.
    [Fact]
    public void Printing_OffersDesignsAndAliquotCount()
    {
        CannedApi.Register(this, new Dictionary<string, object>
        {
            ["/api/print/specimen-label-designs"] = new List<SpecimenLabelDesignListItemDto>
            {
                Item(1, "TUBE-STD", SpecimenLabelPurpose.Tube, true),
                Item(3, "ALIQ-STD", SpecimenLabelPurpose.Aliquot, true)
            }
        });
        var page = Render<Printing>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-spec-design]")));
        Assert.Equal("TUBE-STD", page.Find("[data-spec-design]").GetAttribute("value"));
        Assert.Empty(page.FindAll("[data-aliquot-count]"));

        page.Find("[data-spec-design]").Change("ALIQ-STD");
        Assert.NotNull(page.Find("[data-aliquot-count]"));
        page.Find("[data-aliquot-count]").Change("3");
        Assert.Contains(page.FindAll("button"), b => b.TextContent.Trim() == "Print 3 aliquot labels");
    }
}
