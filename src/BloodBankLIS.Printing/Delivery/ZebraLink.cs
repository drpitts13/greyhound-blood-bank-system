using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Printing.Delivery;

/// <summary>Connection fields copied from a <see cref="ZebraPrinter"/> so the sender stays free of EF.</summary>
public sealed record ZebraLink(
    ZebraConnectionType ConnectionType,
    string? Host,
    int Port,
    string? WindowsPrinterName,
    string? ComPort,
    int BaudRate,
    int DataBits,
    ZebraParity Parity,
    ZebraStopBits StopBits)
{
    public static ZebraLink From(ZebraPrinter printer) => new(
        printer.ConnectionType,
        printer.Host,
        printer.Port,
        printer.WindowsPrinterName,
        printer.ComPort,
        printer.BaudRate,
        printer.DataBits,
        printer.Parity,
        printer.StopBits);
}

public sealed record ZebraSendResult(bool Succeeded, string? Response, string? Error)
{
    public static ZebraSendResult Ok(string? response) => new(true, response, null);

    public static ZebraSendResult Fail(string error) => new(false, null, error);
}

/// <summary>Writes raw bytes to a Windows print queue (ZPL must not be reinterpreted by the driver).</summary>
public interface IRawSpooler
{
    void Write(string printerName, byte[] bytes);
}

public interface IZebraLabelSender
{
    Task<ZebraSendResult> SendAsync(ZebraLink link, string payload, bool readResponse, CancellationToken cancellationToken = default);
}
