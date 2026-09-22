#pragma warning disable CA1416
using System.Drawing;
using System.Drawing.Printing;
using BloodBankLIS.Printing.Delivery;
using PDFtoImage;
using SkiaSharp;

namespace BloodBankLIS.Api.Printing;

/// <summary>
/// Rasterizes a stored PDF with PDFium and draws it onto the named Windows queue
/// at the label's physical size. The preview file is unchanged.
/// </summary>
public sealed class WindowsPdfDocumentPrinter : IPdfDocumentPrinter
{
    private const int RenderDpi = 203;

    public Task SendAsync(string windowsPrinterName, byte[] pdf, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("PDF printing to a Windows queue requires Windows.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(windowsPrinterName);
        if (pdf is not { Length: > 0 })
        {
            throw new ArgumentException("PDF is empty.", nameof(pdf));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var rendered = Conversion.ToImage(pdf, page: 0, options: new RenderOptions(Dpi: RenderDpi));
        using var png = rendered.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(png.ToArray());
        using var bitmap = new Bitmap(stream);
        using var document = new PrintDocument();
        document.PrinterSettings.PrinterName = windowsPrinterName;
        if (!document.PrinterSettings.IsValid)
        {
            throw new InvalidOperationException($"Printer '{windowsPrinterName}' is not installed on this computer.");
        }

        var width = Math.Max(1, (int)Math.Round(bitmap.Width / (double)RenderDpi * 100));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height / (double)RenderDpi * 100));
        document.DefaultPageSettings.PaperSize = new PaperSize("Label", width, height);
        document.DefaultPageSettings.Landscape = width > height;
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        document.PrintPage += (_, e) =>
        {
            e.Graphics?.DrawImage(bitmap, e.PageBounds);
            e.HasMorePages = false;
        };
        document.Print();
        return Task.CompletedTask;
    }
}
