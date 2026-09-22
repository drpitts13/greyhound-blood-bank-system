using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Printing.Delivery;

/// <summary>
/// Sends ZPL (or a status command such as <c>~HI</c>) over the connection stored on a Zebra printer.
/// TCP and serial/Bluetooth run here. Windows queue writes go through <see cref="IRawSpooler"/> when the host provides one.
/// </summary>
public sealed class ZebraLabelSender : IZebraLabelSender
{
    private readonly IRawSpooler? _spooler;

    public ZebraLabelSender(IRawSpooler? spooler = null) => _spooler = spooler;

    public Task<ZebraSendResult> SendAsync(
        ZebraLink link, string payload, bool readResponse, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(payload);

        return link.ConnectionType switch
        {
            ZebraConnectionType.NetworkTcp => SendTcpAsync(link, payload, readResponse, cancellationToken),
            ZebraConnectionType.Serial or ZebraConnectionType.Bluetooth =>
                Task.FromResult(SendSerial(link, payload, readResponse)),
            ZebraConnectionType.WindowsDriver or ZebraConnectionType.Usb =>
                Task.FromResult(SendRaw(link, payload)),
            _ => Task.FromResult(ZebraSendResult.Fail($"Connection type {link.ConnectionType} is not supported."))
        };
    }

    private static async Task<ZebraSendResult> SendTcpAsync(
        ZebraLink link, string payload, bool readResponse, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(link.Host))
        {
            return ZebraSendResult.Fail("Host is required for a network Zebra printer.");
        }

        if (link.Port is < 1 or > 65535)
        {
            return ZebraSendResult.Fail("Port must be between 1 and 65535.");
        }

        try
        {
            using var client = new TcpClient();
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(link.Host, link.Port, connectTimeout.Token);
            await using var stream = client.GetStream();
            var bytes = Encoding.ASCII.GetBytes(WithTerminator(payload));
            await stream.WriteAsync(bytes, connectTimeout.Token);
            if (!readResponse)
            {
                return ZebraSendResult.Ok(null);
            }

            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTimeout.CancelAfter(TimeSpan.FromSeconds(2));
            var buffer = new byte[512];
            var read = await stream.ReadAsync(buffer, readTimeout.Token);
            return ZebraSendResult.Ok(Encoding.ASCII.GetString(buffer, 0, read).Trim());
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or TimeoutException)
        {
            return ZebraSendResult.Fail(ex.Message);
        }
    }

    private static ZebraSendResult SendSerial(ZebraLink link, string payload, bool readResponse)
    {
        if (string.IsNullOrWhiteSpace(link.ComPort))
        {
            return ZebraSendResult.Fail("COM port is required for a serial or Bluetooth Zebra printer.");
        }

        try
        {
            using var port = new SerialPort(link.ComPort.Trim(), link.BaudRate, MapParity(link.Parity), link.DataBits, MapStopBits(link.StopBits))
            {
                ReadTimeout = 2000,
                WriteTimeout = 2000,
                Encoding = Encoding.ASCII
            };
            port.Open();
            port.Write(WithTerminator(payload));
            if (!readResponse)
            {
                return ZebraSendResult.Ok(null);
            }

            var buffer = new byte[512];
            var read = port.Read(buffer, 0, buffer.Length);
            return ZebraSendResult.Ok(Encoding.ASCII.GetString(buffer, 0, Math.Max(read, 0)).Trim());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException or InvalidOperationException or ArgumentException)
        {
            return ZebraSendResult.Fail(ex.Message);
        }
    }

    private ZebraSendResult SendRaw(ZebraLink link, string payload)
    {
        if (_spooler is null)
        {
            return ZebraSendResult.Fail("This host cannot write raw ZPL to a Windows printer queue.");
        }

        if (string.IsNullOrWhiteSpace(link.WindowsPrinterName))
        {
            return ZebraSendResult.Fail("Windows printer name is required for a USB or driver-connected Zebra.");
        }

        try
        {
            _spooler.Write(link.WindowsPrinterName.Trim(), Encoding.ASCII.GetBytes(payload));
            return ZebraSendResult.Ok(null);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or PlatformNotSupportedException or ArgumentException)
        {
            return ZebraSendResult.Fail(ex.Message);
        }
    }

    private static string WithTerminator(string payload) =>
        payload.EndsWith('\n') ? payload : payload + "\r\n";

    private static Parity MapParity(ZebraParity parity) => parity switch
    {
        ZebraParity.Odd => Parity.Odd,
        ZebraParity.Even => Parity.Even,
        ZebraParity.Mark => Parity.Mark,
        ZebraParity.Space => Parity.Space,
        _ => Parity.None
    };

    private static StopBits MapStopBits(ZebraStopBits stopBits) => stopBits switch
    {
        ZebraStopBits.OnePointFive => StopBits.OnePointFive,
        ZebraStopBits.Two => StopBits.Two,
        _ => StopBits.One
    };
}
