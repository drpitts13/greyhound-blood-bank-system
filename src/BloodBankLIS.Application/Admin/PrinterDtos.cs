using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Admin;

public sealed record PdfPrinterDto(
    long Id,
    string Name,
    string WindowsPrinterName,
    bool IsActive,
    bool IsDefault,
    string? Notes)
{
    public static PdfPrinterDto From(PdfPrinter printer) => new(
        printer.Id, printer.Name, printer.WindowsPrinterName, printer.IsActive, printer.IsDefault, printer.Notes);
}

public sealed record SavePdfPrinterRequest(string Name, string WindowsPrinterName, bool IsDefault, string? Notes);

public sealed record ZebraPrinterDto(
    long Id,
    string Name,
    ZebraConnectionType ConnectionType,
    string? Host,
    int Port,
    string? WindowsPrinterName,
    string? ComPort,
    int BaudRate,
    int DataBits,
    ZebraParity Parity,
    ZebraStopBits StopBits,
    int Dpi,
    bool IsActive,
    bool IsDefault,
    string? Notes)
{
    public static ZebraPrinterDto From(ZebraPrinter printer) => new(
        printer.Id,
        printer.Name,
        printer.ConnectionType,
        printer.Host,
        printer.Port,
        printer.WindowsPrinterName,
        printer.ComPort,
        printer.BaudRate,
        printer.DataBits,
        printer.Parity,
        printer.StopBits,
        printer.Dpi,
        printer.IsActive,
        printer.IsDefault,
        printer.Notes);
}

public sealed record SaveZebraPrinterRequest(
    string Name,
    ZebraConnectionType ConnectionType,
    string? Host,
    int Port,
    string? WindowsPrinterName,
    string? ComPort,
    int BaudRate,
    int DataBits,
    ZebraParity Parity,
    ZebraStopBits StopBits,
    int Dpi,
    bool IsDefault,
    string? Notes);

public sealed record PrinterChoiceDto(long Id, string Name, bool IsDefault);
