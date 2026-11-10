using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Printing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Audit;
using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Printing.Delivery;
using BloodBankLIS.Printing.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Integration.Tests;

/// <summary>
/// Test prints from the specimen label designer (Cycle 658): the layout on the canvas, saved or not, with sample data
/// that cannot pass for a patient, shown or sent to a printer.
/// </summary>
public class SpecimenLabelTestPrintTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private SpecimenLabelTestPrintService Service(BloodBankDbContext c, IZebraLabelSender? zebra = null, IPdfDocumentPrinter? pdf = null,
        IPermissionEvaluator? permissions = null) =>
        new(new EfRepository<SpecimenLabelDesign>(c), new EfRepository<PdfPrinter>(c), new EfRepository<ZebraPrinter>(c),
            pdf ?? new RecordingPdfPrinter(), zebra ?? new RecordingZebraSender(),
            new ILabelRenderer[] { new ZplLabelRenderer(), new PdfLabelRenderer(), new PreviewLabelRenderer() },
            c, _factory.CurrentUser, new AuditWriter(c, _factory.Clock, _factory.CurrentUser), permissions);

    private static async Task<SpecimenLabelDesign> DesignAsync(BloodBankDbContext c, SpecimenLabelPurpose purpose)
    {
        var width = purpose == SpecimenLabelPurpose.Aliquot ? 305 : 406;
        var height = purpose == SpecimenLabelPurpose.Aliquot ? 102 : 203;
        var design = new SpecimenLabelDesign
        {
            Code = purpose == SpecimenLabelPurpose.Aliquot ? "TP-ALQ" : "TP-TUBE",
            Name = "Test print",
            Purpose = purpose,
            WidthDots = width,
            HeightDots = height,
            DocumentJson = System.Text.Json.JsonSerializer.Serialize(SpecimenLabelDesignRules.Starter(purpose, width, height), LabelFields.JsonOptions),
            IsDefault = true
        };
        c.SpecimenLabelDesigns.Add(design);
        await c.SaveChangesAsync();
        return design;
    }

    private static LabelLayoutDocument Unsaved(SpecimenLabelPurpose purpose, int width, int height)
    {
        var document = SpecimenLabelDesignRules.Starter(purpose, width, height);
        document.Page1.Add(new PlacedLabelField { Key = LabelFields.StaticText, StaticText = "UNSAVED EDIT", X = 2000, Y = 4, FontHeight = 14 });
        return document;
    }

    // TEST-BB-832: a test print renders the layout as it stands on the designer (an unsaved element included, kept on
    // the label) with sample data plainly not a patient: the TESTLABEL01 barcode, "TEST LABEL, NOT FOR USE", and on an
    // aliquot design "Aliquot 1 of 2". Shown here only, it creates no print job and records nothing.
    [Fact]
    public async Task TestPrint_RendersTheLayoutWithSampleData()
    {
        await using var c = _factory.Create();
        var aliquot = await DesignAsync(c, SpecimenLabelPurpose.Aliquot);
        var auditBefore = await c.AuditEvents.CountAsync();

        var zpl = await Service(c).TestPrintAsync(aliquot.Id, new SpecimenLabelTestPrintRequest(Unsaved(SpecimenLabelPurpose.Aliquot, 305, 102), LabelFormat.Zpl));
        Assert.True(zpl.Succeeded, zpl.Error);
        Assert.Equal((LabelFormat.Zpl, (string?)null), (zpl.Value!.Format, zpl.Value.SentTo));
        Assert.Contains("^PW305", zpl.Value.Zpl);
        Assert.Contains(SpecimenLabelTestPrintService.SampleAccession, zpl.Value.Zpl);
        Assert.Contains("TEST LABEL, NOT FOR USE", zpl.Value.Zpl);
        Assert.Contains("Aliquot 1 of 2", zpl.Value.Zpl);
        Assert.Contains("^FO297,4", zpl.Value.Zpl);
        Assert.Contains("UNSAVED EDIT", zpl.Value.Zpl);

        var pdf = await Service(c).TestPrintAsync(aliquot.Id, new SpecimenLabelTestPrintRequest(Unsaved(SpecimenLabelPurpose.Aliquot, 305, 102)));
        Assert.True(pdf.Succeeded, pdf.Error);
        Assert.StartsWith("JVBER", pdf.Value!.PdfBase64);

        Assert.False(await c.PrintJobs.AnyAsync());
        Assert.Equal(auditBefore, await c.AuditEvents.CountAsync());
    }

    // TEST-BB-833: sent to a Zebra printer the ZPL is scaled to its dots per inch and the test print is audited against
    // the design; a PDF printer gets the PDF; an inactive printer, two printers at once, an unknown design, and a user
    // who cannot edit configuration are refused.
    [Fact]
    public async Task TestPrint_ToAPrinter_IsAuditedAndChecked()
    {
        await using var c = _factory.Create();
        var tube = await DesignAsync(c, SpecimenLabelPurpose.Tube);
        var zebraPrinter = new ZebraPrinter { Name = "Bench Zebra", Host = "127.0.0.1", Dpi = 300, IsActive = true };
        var offline = new ZebraPrinter { Name = "Old Zebra", Host = "127.0.0.2", IsActive = false };
        var pdfPrinter = new PdfPrinter { Name = "Office PDF", WindowsPrinterName = "Microsoft Print to PDF", IsActive = true };
        c.AddRange(zebraPrinter, offline, pdfPrinter);
        await c.SaveChangesAsync();
        var document = SpecimenLabelDesignRules.Starter(SpecimenLabelPurpose.Tube, 406, 203);

        var sender = new RecordingZebraSender();
        var sent = await Service(c, zebra: sender).TestPrintAsync(tube.Id, new SpecimenLabelTestPrintRequest(document, LabelFormat.Pdf, ZebraPrinterId: zebraPrinter.Id));
        Assert.True(sent.Succeeded, sent.Error);
        Assert.Equal((LabelFormat.Zpl, "Bench Zebra"), (sent.Value!.Format, sent.Value.SentTo));
        Assert.Contains("^PW600", sender.Payload);
        Assert.True(await c.AuditEvents.AnyAsync(a => a.EntityType == nameof(SpecimenLabelDesign) && a.EntityId == tube.Id
            && a.EventType == AuditEventType.Print && a.Reason == "Test label for design TP-TUBE sent to Bench Zebra."));

        var pdf = new RecordingPdfPrinter();
        var toPdf = await Service(c, pdf: pdf).TestPrintAsync(tube.Id, new SpecimenLabelTestPrintRequest(document, LabelFormat.Zpl, PdfPrinterId: pdfPrinter.Id));
        Assert.Equal(("Office PDF", "Microsoft Print to PDF"), (toPdf.Value!.SentTo, pdf.PrinterName));

        Assert.Equal("Zebra printer is not available.", (await Service(c).TestPrintAsync(tube.Id, new SpecimenLabelTestPrintRequest(document, ZebraPrinterId: offline.Id))).Error);
        Assert.False((await Service(c).TestPrintAsync(tube.Id, new SpecimenLabelTestPrintRequest(document, PdfPrinterId: pdfPrinter.Id, ZebraPrinterId: zebraPrinter.Id))).Succeeded);
        Assert.Equal("Label design not found.", (await Service(c).TestPrintAsync(-1, new SpecimenLabelTestPrintRequest(document))).Error);
        Assert.False((await Service(c, permissions: new FixedPermissionEvaluator(0, PermissionCodes.AdminConfigView))
            .TestPrintAsync(tube.Id, new SpecimenLabelTestPrintRequest(document))).Succeeded);
        Assert.False(await c.PrintJobs.AnyAsync());
    }

    private sealed class RecordingPdfPrinter : IPdfDocumentPrinter
    {
        public string? PrinterName { get; private set; }

        public Task SendAsync(string windowsPrinterName, byte[] pdf, CancellationToken cancellationToken = default)
        {
            PrinterName = windowsPrinterName;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingZebraSender : IZebraLabelSender
    {
        public string? Payload { get; private set; }

        public Task<ZebraSendResult> SendAsync(ZebraLink link, string payload, bool readResponse, CancellationToken cancellationToken = default)
        {
            Payload = payload;
            return Task.FromResult(ZebraSendResult.Ok(null));
        }
    }
}
