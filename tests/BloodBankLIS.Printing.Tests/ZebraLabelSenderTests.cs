using System.Net;
using System.Net.Sockets;
using System.Text;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Printing.Delivery;

namespace BloodBankLIS.Printing.Tests;

public class ZebraLabelSenderTests
{
    [Fact]
    public async Task Send_NetworkTcp_WritesPayloadToLoopback()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = listener.AcceptTcpClientAsync();

        var sender = new ZebraLabelSender();
        var link = new ZebraLink(
            ZebraConnectionType.NetworkTcp, "127.0.0.1", port, null, null, 9600, 8, ZebraParity.None, ZebraStopBits.One);

        var send = sender.SendAsync(link, "^XA^FDACC^FS^XZ", readResponse: false);
        using var client = await accepted;
        await using var stream = client.GetStream();
        var buffer = new byte[128];
        var read = await stream.ReadAsync(buffer);
        var text = Encoding.ASCII.GetString(buffer, 0, read);
        var result = await send;

        Assert.True(result.Succeeded, result.Error);
        Assert.Contains("^FDACC^FS", text);
        listener.Stop();
    }

    [Fact]
    public async Task Send_WindowsQueueWithoutSpooler_Fails()
    {
        var sender = new ZebraLabelSender();
        var link = new ZebraLink(
            ZebraConnectionType.Usb, null, 9100, "ZDesigner", null, 9600, 8, ZebraParity.None, ZebraStopBits.One);

        var result = await sender.SendAsync(link, "^XA^XZ", readResponse: false);

        Assert.False(result.Succeeded);
        Assert.Contains("Windows", result.Error);
    }
}
