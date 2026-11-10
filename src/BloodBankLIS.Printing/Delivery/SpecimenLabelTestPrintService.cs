using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Printing;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Printing.Rendering;
using BloodBankLIS.Printing.Templates;

namespace BloodBankLIS.Printing.Delivery;

/// <summary>A test print of a label design as it stands on the designer (saved or not), optionally sent to a printer.</summary>
public sealed record SpecimenLabelTestPrintRequest(
    LabelLayoutDocument Document,
    LabelFormat Format = LabelFormat.Pdf,
    long? PdfPrinterId = null,
    long? ZebraPrinterId = null);

/// <summary>What a test print produced: the ZPL or the PDF, and the printer it went to, if any.</summary>
public sealed record SpecimenLabelTestPrintResult(LabelFormat Format, string? Zpl, string? PdfBase64, string? SentTo);

/// <summary>
/// Test prints from the specimen label designer (Cycle 658). The label carries sample data that cannot pass for a real
/// specimen: the patient reads "TEST LABEL, NOT FOR USE", the patient ID "TEST-ONLY", and the barcode "TESTLABEL01",
/// which matches no specimen. No print job is created (there is no patient); a test print sent to a printer is audited
/// against the design.
/// </summary>
public sealed class SpecimenLabelTestPrintService
{
    public const string SampleAccession = "TESTLABEL01";

    private readonly IRepository<SpecimenLabelDesign> _designs;
    private readonly IRepository<PdfPrinter> _pdfPrinters;
    private readonly IRepository<ZebraPrinter> _zebraPrinters;
    private readonly IPdfDocumentPrinter _pdfPrinter;
    private readonly IZebraLabelSender _zebra;
    private readonly IReadOnlyDictionary<LabelFormat, ILabelRenderer> _renderers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public SpecimenLabelTestPrintService(
        IRepository<SpecimenLabelDesign> designs,
        IRepository<PdfPrinter> pdfPrinters,
        IRepository<ZebraPrinter> zebraPrinters,
        IPdfDocumentPrinter pdfPrinter,
        IZebraLabelSender zebra,
        IEnumerable<ILabelRenderer> renderers,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _designs = designs;
        _pdfPrinters = pdfPrinters;
        _zebraPrinters = zebraPrinters;
        _pdfPrinter = pdfPrinter;
        _zebra = zebra;
        _renderers = renderers.ToDictionary(r => r.Format);
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    /// <summary>The sample a test label prints: plainly not a patient.</summary>
    public static SpecimenLabelModel SampleModel(SpecimenLabelPurpose purpose) => new(
        SampleAccession,
        "TEST LABEL, NOT FOR USE",
        "TEST-ONLY",
        new DateOnly(1900, 1, 1),
        "TEST",
        new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        "TEST",
        "TEST",
        "U",
        purpose == SpecimenLabelPurpose.Aliquot ? 1 : null,
        purpose == SpecimenLabelPurpose.Aliquot ? 2 : null);

    public async Task<OperationResult<SpecimenLabelTestPrintResult>> TestPrintAsync(
        long designId, SpecimenLabelTestPrintRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        if (_permissions is not null
            && PrinterAuthorizationRule.EvaluateUpdate(await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.AdminConfigEdit, ct))
                is { Severity: RuleSeverity.HardStop } denied)
        {
            return OperationResult<SpecimenLabelTestPrintResult>.Fail(denied.Message);
        }

        if (request.PdfPrinterId is not null && request.ZebraPrinterId is not null)
        {
            return OperationResult<SpecimenLabelTestPrintResult>.Fail("Choose one PDF printer or one Zebra printer.");
        }

        var design = await _designs.GetByIdAsync(designId, ct);
        if (design is null)
        {
            return OperationResult<SpecimenLabelTestPrintResult>.Fail("Label design not found.");
        }

        // A Zebra printer takes ZPL and a PDF queue takes the PDF, whatever format was asked for.
        var format = request.ZebraPrinterId is not null ? LabelFormat.Zpl : request.PdfPrinterId is not null ? LabelFormat.Pdf : request.Format;
        if (format is not (LabelFormat.Zpl or LabelFormat.Pdf) || !_renderers.TryGetValue(format, out var renderer))
        {
            return OperationResult<SpecimenLabelTestPrintResult>.Fail("Test prints are rendered as ZPL or PDF.");
        }

        // The layout as it stands on the designer, at the design's size and kept on the label.
        var layout = new LabelLayoutDocument { WidthDots = design.WidthDots, HeightDots = design.HeightDots, Page1 = request.Document.Page1.ToList() };
        SpecimenLabelDesignRules.Normalize(design.WidthDots, design.HeightDots, layout.Page1);
        var rendered = renderer.Render(LabelLayoutComposer.ComposeSpecimen(layout, SampleModel(design.Purpose)));

        string? sentTo = null;
        try
        {
            if (request.ZebraPrinterId is long zebraId)
            {
                var printer = await _zebraPrinters.GetByIdAsync(zebraId, ct);
                if (printer is null || !printer.IsActive)
                {
                    return OperationResult<SpecimenLabelTestPrintResult>.Fail("Zebra printer is not available.");
                }

                var sent = await _zebra.SendAsync(ZebraLink.From(printer), ZplDpiScaler.Scale(rendered.Text ?? string.Empty, printer.Dpi), readResponse: false, ct);
                if (!sent.Succeeded)
                {
                    return OperationResult<SpecimenLabelTestPrintResult>.Fail(sent.Error ?? "Zebra send failed.");
                }

                sentTo = printer.Name;
            }
            else if (request.PdfPrinterId is long pdfId)
            {
                var printer = await _pdfPrinters.GetByIdAsync(pdfId, ct);
                if (printer is null || !printer.IsActive)
                {
                    return OperationResult<SpecimenLabelTestPrintResult>.Fail("PDF printer is not available.");
                }

                await _pdfPrinter.SendAsync(printer.WindowsPrinterName, rendered.Pdf ?? [], ct);
                sentTo = printer.Name;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException or IOException or ArgumentException)
        {
            return OperationResult<SpecimenLabelTestPrintResult>.Fail(ex.Message);
        }

        if (sentTo is not null)
        {
            _audit.Record(AuditEventType.Print, nameof(SpecimenLabelDesign), design.Id, oldValue: null,
                newValue: new { design.Code, design.Version, Printer = sentTo, Format = format, Sample = SampleAccession },
                reason: $"Test label for design {design.Code} sent to {sentTo}.");
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return OperationResult<SpecimenLabelTestPrintResult>.Ok(new SpecimenLabelTestPrintResult(
            format, rendered.Text, rendered.Pdf is { } pdf ? Convert.ToBase64String(pdf) : null, sentTo));
    }
}
