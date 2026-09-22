using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A Zebra label printer and the connection used to send ZPL.
/// Network uses raw TCP (port 9100). USB and Windows driver write raw ZPL to a Windows queue.
/// Serial and Bluetooth use a COM port (Bluetooth is the paired radio's virtual COM port).
/// </summary>
public class ZebraPrinter : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public ZebraConnectionType ConnectionType { get; set; } = ZebraConnectionType.NetworkTcp;

    public string? Host { get; set; }

    public int Port { get; set; } = 9100;

    public string? WindowsPrinterName { get; set; }

    public string? ComPort { get; set; }

    public int BaudRate { get; set; } = 9600;

    public int DataBits { get; set; } = 8;

    public ZebraParity Parity { get; set; } = ZebraParity.None;

    public ZebraStopBits StopBits { get; set; } = ZebraStopBits.One;

    /// <summary>Print-head resolution. Templates are laid out at 203 dpi and scaled when this differs.</summary>
    public int Dpi { get; set; } = 203;

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }

    public string? Notes { get; set; }
}
