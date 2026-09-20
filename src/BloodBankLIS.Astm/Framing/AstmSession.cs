using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace BloodBankLIS.Astm.Framing;

public sealed record AstmSessionResult(bool Completed, string? Message, string? Error);

/// <summary>
/// LIS1-A establishment / transfer / termination over a bidirectional stream.
/// The LIS is the server and wins ENQ contention.
/// </summary>
public sealed class AstmSession
{
    private readonly Stream _stream;
    private readonly bool _isServer;
    private readonly ILogger? _logger;
    private readonly TimeSpan _idleTimeout;

    public AstmSession(Stream stream, bool isServer, ILogger? logger = null, TimeSpan? idleTimeout = null)
    {
        _stream = stream;
        _isServer = isServer;
        _logger = logger;
        _idleTimeout = idleTimeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task<AstmSessionResult> ReceiveAsync(CancellationToken ct, bool alreadyEstablished = false)
    {
        var buffer = new List<byte>();
        var assembled = new System.Text.StringBuilder();
        var established = alreadyEstablished;

        while (!ct.IsCancellationRequested)
        {
            var b = await ReadByteAsync(ct);
            if (b is null)
            {
                return assembled.Length == 0
                    ? new AstmSessionResult(false, null, "Peer closed the connection.")
                    : new AstmSessionResult(true, assembled.ToString(), null);
            }

            if (!established)
            {
                if (b.Value == AstmControlChars.Enq)
                {
                    await WriteByteAsync(AstmControlChars.Ack, ct);
                    established = true;
                    _logger?.LogDebug("ASTM session established (received ENQ).");
                    continue;
                }

                if (b.Value == AstmControlChars.Eot)
                {
                    return new AstmSessionResult(false, null, "Received EOT before establishment.");
                }

                continue;
            }

            if (b.Value == AstmControlChars.Eot)
            {
                return new AstmSessionResult(true, assembled.ToString(), null);
            }

            if (b.Value == AstmControlChars.Enq)
            {
                await WriteByteAsync(AstmControlChars.Ack, ct);
                continue;
            }

            buffer.Add(b.Value);
            if (!AstmFraming.TryExtract(buffer, out var frame, out var error))
            {
                continue;
            }

            if (error is not null || frame is null)
            {
                await WriteByteAsync(AstmControlChars.Nak, ct);
                continue;
            }

            assembled.Append(frame.Text);
            await WriteByteAsync(AstmControlChars.Ack, ct);
        }

        return new AstmSessionResult(false, null, "Cancelled.");
    }

    public async Task<AstmSessionResult> SendAsync(string message, CancellationToken ct)
    {
        await WriteByteAsync(AstmControlChars.Enq, ct);
        var reply = await ReadByteAsync(ct);
        if (reply == AstmControlChars.Enq && _isServer)
        {
            await WriteByteAsync(AstmControlChars.Ack, ct);
            var incoming = await ReceiveAfterContentionAsync(ct);
            return incoming;
        }

        if (reply != AstmControlChars.Ack)
        {
            return new AstmSessionResult(false, null, "Peer did not ACK ENQ.");
        }

        foreach (var frame in AstmFraming.Split(message))
        {
            var retries = 0;
            while (true)
            {
                await _stream.WriteAsync(AstmFraming.Encode(frame), ct);
                var ack = await ReadByteAsync(ct);
                if (ack == AstmControlChars.Ack)
                {
                    break;
                }

                retries++;
                if (retries >= AstmControlChars.MaxRetries)
                {
                    return new AstmSessionResult(false, null, "Frame was NAK'd too many times.");
                }
            }
        }

        await WriteByteAsync(AstmControlChars.Eot, ct);
        return new AstmSessionResult(true, message, null);
    }

    public static async Task<AstmSessionResult> ReceiveFromTcpAsync(TcpClient client, bool isServer, ILogger? logger, CancellationToken ct)
    {
        await using var stream = client.GetStream();
        var session = new AstmSession(stream, isServer, logger);
        return await session.ReceiveAsync(ct);
    }

    public static async Task<AstmSessionResult> SendToAsync(string host, int port, string message, ILogger? logger, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(TimeSpan.FromSeconds(15));
        await client.ConnectAsync(host, port, linked.Token);
        await using var stream = client.GetStream();
        var session = new AstmSession(stream, isServer: false, logger);
        return await session.SendAsync(message, linked.Token);
    }

    private async Task<AstmSessionResult> ReceiveAfterContentionAsync(CancellationToken ct)
    {
        _logger?.LogInformation("ASTM contention: LIS is receiving first.");
        return await ReceiveAsync(ct, alreadyEstablished: true);
    }

    private async Task WriteByteAsync(byte value, CancellationToken ct)
    {
        await _stream.WriteAsync(new[] { value }, ct);
        await _stream.FlushAsync(ct);
    }

    private async Task<byte?> ReadByteAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(_idleTimeout);
        var buffer = new byte[1];
        try
        {
            var read = await _stream.ReadAsync(buffer.AsMemory(0, 1), linked.Token);
            return read == 0 ? null : buffer[0];
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
