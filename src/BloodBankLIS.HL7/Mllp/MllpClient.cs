using System.Net.Sockets;
using BloodBankLIS.HL7.Parsing;

namespace BloodBankLIS.HL7.Mllp;

public sealed record MllpSendResult(bool Connected, string? AckRaw, string? AckCode, string? Error);

/// <summary>
/// Sends one HL7 payload over MLLP and waits for a framed ACK. Used by the outbound
/// sender; SoftBank/SafeTrace transmit queued ORU/DFT the same way.
/// </summary>
public static class MllpClient
{
    public static async Task<MllpSendResult> SendAsync(
        string host,
        int port,
        string hl7,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(hl7);

        using var client = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout > TimeSpan.Zero)
            cts.CancelAfter(timeout);

        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(MllpFraming.Wrap(hl7), cts.Token);

            var accumulated = new List<byte>();
            var buffer = new byte[8192];
            while (!cts.Token.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token);
                if (read == 0)
                    break;

                accumulated.AddRange(buffer.AsSpan(0, read).ToArray());
                var frames = MllpFraming.Extract(accumulated.ToArray(), out var consumed);
                if (frames.Count > 0)
                {
                    var ack = frames[0];
                    return new MllpSendResult(true, ack, TryAckCode(ack), null);
                }

                if (consumed > 0)
                    accumulated.RemoveRange(0, consumed);
            }

            return new MllpSendResult(true, null, null, "Peer closed the connection before an MLLP ACK arrived.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new MllpSendResult(false, null, null, "Timed out waiting for the MLLP ACK.");
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            return new MllpSendResult(false, null, null, ex.Message);
        }
    }

    private static string? TryAckCode(string ack)
    {
        try
        {
            var parsed = Hl7Parser.Parse(ack);
            var code = parsed.Get("MSA-1");
            return string.IsNullOrWhiteSpace(code) ? null : code;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
