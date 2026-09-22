namespace BloodBankLIS.Printing.Delivery;

/// <summary>Sends stored PDF bytes to a Windows print queue. The API host supplies the Windows implementation.</summary>
public interface IPdfDocumentPrinter
{
    Task SendAsync(string windowsPrinterName, byte[] pdf, CancellationToken cancellationToken = default);
}

/// <summary>Registered when the process cannot reach a Windows spooler. Delivery records the failure.</summary>
public sealed class UnavailablePdfDocumentPrinter : IPdfDocumentPrinter
{
    public Task SendAsync(string windowsPrinterName, byte[] pdf, CancellationToken cancellationToken = default) =>
        throw new PlatformNotSupportedException(
            "PDF printing to a Windows queue is only available when the API is running on Windows.");
}
