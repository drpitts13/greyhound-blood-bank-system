using System.Net;
using System.Net.Sockets;
using BloodBankLIS.HL7.Mllp;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Tests;

public class MllpClientTests
{
    private const string Oru =
        "MSH|^~\\&|BBLIS|LAB|EHR|HOSP|20260530120000||ORU^R01|OUT1|P|2.5\r" +
        "PID|1||MRN1\rOBR|1|||ABORH\rOBX|1|ST|ABORH||A Positive";

    [Fact]
    public async Task SendAsync_ReceivesAcceptAck()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var accept = AcceptAndAckAsync(listener, AckCode.Accept);
        var result = await MllpClient.SendAsync("127.0.0.1", port, Oru, TimeSpan.FromSeconds(5));
        await accept;
        listener.Stop();

        Assert.True(result.Connected);
        Assert.Equal(AckCode.Accept, result.AckCode);
        Assert.Contains("MSA|AA|", result.AckRaw);
    }

    [Fact]
    public async Task SendAsync_UnreachableHost_ReturnsError()
    {
        var result = await MllpClient.SendAsync("127.0.0.1", 1, Oru, TimeSpan.FromMilliseconds(400));
        Assert.False(result.Connected);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    private static async Task AcceptAndAckAsync(TcpListener listener, string ackCode)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        var buffer = new byte[8192];
        var read = await stream.ReadAsync(buffer);
        var frames = MllpFraming.Extract(buffer.AsSpan(0, read), out _);
        var inbound = Hl7Parser.Parse(frames[0]);
        var ack = Hl7AckBuilder.BuildAck(inbound, ackCode, "ok", "ACK1", new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));
        await stream.WriteAsync(MllpFraming.Wrap(ack));
    }
}
