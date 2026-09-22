using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Printing.Rendering;

namespace BloodBankLIS.Printing.Delivery;

public sealed record DeliverPrintRequest(long? PdfPrinterId = null, long? ZebraPrinterId = null);

/// <summary>
/// Sends an already rendered job to a saved printer. Preview generation stays in
/// <see cref="PrintService"/>; this only delivers the stored PDF or ZPL.
/// A failed send keeps the rendered file and records <see cref="PrintDeliveryStatus.Failed"/>.
/// </summary>
public sealed class PrintDeliveryService
{
    private readonly IRepository<PrintJob> _jobs;
    private readonly IRepository<PdfPrinter> _pdfPrinters;
    private readonly IRepository<ZebraPrinter> _zebraPrinters;
    private readonly IPdfDocumentPrinter _pdfPrinter;
    private readonly IZebraLabelSender _zebra;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly ICurrentUser _currentUser;

    public PrintDeliveryService(
        IRepository<PrintJob> jobs,
        IRepository<PdfPrinter> pdfPrinters,
        IRepository<ZebraPrinter> zebraPrinters,
        IPdfDocumentPrinter pdfPrinter,
        IZebraLabelSender zebra,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _jobs = jobs;
        _pdfPrinters = pdfPrinters;
        _zebraPrinters = zebraPrinters;
        _pdfPrinter = pdfPrinter;
        _zebra = zebra;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<OperationResult<PrintJob>> DeliverAsync(long jobId, DeliverPrintRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PdfPrinterId is null == request.ZebraPrinterId is null)
        {
            return OperationResult<PrintJob>.Fail("Choose one PDF printer or one Zebra printer.");
        }

        var denied = await RejectAsync(PermissionCodes.PrintLabel, PrintAuthorizationRule.EvaluateLabel, ct);
        if (denied is not null)
        {
            return denied;
        }

        var job = await _jobs.GetByIdAsync(jobId, ct);
        if (job is null)
        {
            return OperationResult<PrintJob>.Fail($"Print job {jobId} not found.");
        }

        string? error = null;
        try
        {
            if (request.PdfPrinterId is long pdfId)
            {
                await SendPdfAsync(job, pdfId, ct);
            }
            else
            {
                await SendZplAsync(job, request.ZebraPrinterId!.Value, ct);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException or IOException or ArgumentException)
        {
            error = ex.Message;
        }

        if (error is null)
        {
            job.DeliveryStatus = PrintDeliveryStatus.Sent;
            job.DeliveryError = null;
            job.DeliveredUtc = _clock.UtcNow;
        }
        else
        {
            job.DeliveryStatus = PrintDeliveryStatus.Failed;
            job.DeliveryError = error.Length > 1000 ? error[..1000] : error;
        }

        _jobs.Update(job);
        _audit.Record(
            AuditEventType.Print,
            nameof(PrintJob),
            job.Id,
            oldValue: null,
            newValue: new { job.DeliveryStatus, job.TargetPrinter, job.DeliveryError },
            reason: error is null ? $"Delivered to {job.TargetPrinter}." : $"Delivery failed: {job.DeliveryError}");
        await _unitOfWork.SaveChangesAsync(ct);
        return error is null
            ? OperationResult<PrintJob>.Ok(job)
            : OperationResult<PrintJob>.Fail(error);
    }

    public async Task<OperationResult<string>> ProbeZebraAsync(long zebraPrinterId, CancellationToken ct = default)
    {
        var denied = await RejectProbeAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var printer = await _zebraPrinters.GetByIdAsync(zebraPrinterId, ct);
        if (printer is null)
        {
            return OperationResult<string>.Fail($"Zebra printer {zebraPrinterId} not found.");
        }

        var result = await _zebra.SendAsync(ZebraLink.From(printer), "~HI", readResponse: true, ct);
        return result.Succeeded
            ? OperationResult<string>.Ok(string.IsNullOrWhiteSpace(result.Response) ? "Printer accepted the status request." : result.Response!)
            : OperationResult<string>.Fail(result.Error ?? "The printer did not respond.");
    }

    private async Task SendPdfAsync(PrintJob job, long pdfPrinterId, CancellationToken ct)
    {
        if (job.Format != LabelFormat.Pdf || job.RenderedPdf is not { Length: > 0 })
        {
            throw new InvalidOperationException("This job has no PDF to print.");
        }

        var printer = await _pdfPrinters.GetByIdAsync(pdfPrinterId, ct);
        if (printer is null || !printer.IsActive)
        {
            throw new InvalidOperationException("PDF printer is not available.");
        }

        await _pdfPrinter.SendAsync(printer.WindowsPrinterName, job.RenderedPdf, ct);
        job.PdfPrinterId = printer.Id;
        job.ZebraPrinterId = null;
        job.TargetPrinter = printer.Name;
    }

    private async Task SendZplAsync(PrintJob job, long zebraPrinterId, CancellationToken ct)
    {
        if (job.Format != LabelFormat.Zpl || string.IsNullOrEmpty(job.RenderedZpl))
        {
            throw new InvalidOperationException("This job has no ZPL to send.");
        }

        var printer = await _zebraPrinters.GetByIdAsync(zebraPrinterId, ct);
        if (printer is null || !printer.IsActive)
        {
            throw new InvalidOperationException("Zebra printer is not available.");
        }

        var zpl = ZplDpiScaler.Scale(job.RenderedZpl, printer.Dpi);
        var result = await _zebra.SendAsync(ZebraLink.From(printer), zpl, readResponse: false, ct);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Error ?? "Zebra send failed.");
        }

        job.ZebraPrinterId = printer.Id;
        job.PdfPrinterId = null;
        job.TargetPrinter = printer.Name;
    }

    private Task<OperationResult<PrintJob>?> RejectAsync(
        string permissionCode, Func<bool, RuleResult> evaluate, CancellationToken ct) =>
        RejectCoreAsync<PrintJob>(permissionCode, evaluate, ct);

    private async Task<OperationResult<string>?> RejectProbeAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.AdminConfigEdit, ct);
        var auth = PrinterAuthorizationRule.EvaluateUpdate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<string>.Fail(auth.Message)
            : null;
    }

    private async Task<OperationResult<T>?> RejectCoreAsync<T>(
        string permissionCode, Func<bool, RuleResult> evaluate, CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(_currentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<T>.Fail(auth.Message)
            : null;
    }
}
