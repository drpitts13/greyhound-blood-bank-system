using System.Net;
using System.Net.Sockets;
using System.Text;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.HL7.Mllp;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Api.Hosting;

/// <summary>
/// Inbound MLLP listener. Binds each enabled inbound MLLP <c>InterfaceEndpoint</c>
/// port (and optionally <c>Hl7:Mllp:Port</c> when <c>Hl7:Mllp:Enabled=true</c>).
/// Each complete frame is handed to <see cref="Hl7InboundProcessor"/> and the ACK
/// is written back on the same connection.
/// </summary>
public sealed class MllpListenerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MllpListenerService> _logger;
    private readonly bool _forceEnabled;
    private readonly int _fallbackPort;

    public MllpListenerService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<MllpListenerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _forceEnabled = configuration.GetValue("Hl7:Mllp:Enabled", false);
        _fallbackPort = configuration.GetValue("Hl7:Mllp:Port", 2575);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var targets = await ResolveListenTargetsAsync(stoppingToken);
        if (targets.Count == 0)
        {
            _logger.LogInformation(
                "MLLP listener idle: no enabled inbound MLLP endpoints. Enable an ADT/ORM interface in Admin, or set Hl7:Mllp:Enabled=true.");
            return;
        }

        var acceptLoops = new List<Task>();
        var listeners = new List<TcpListener>();
        try
        {
            foreach (var target in targets)
            {
                try
                {
                    var listener = new TcpListener(IPAddress.Any, target.Port);
                    listener.Start();
                    listeners.Add(listener);
                    _logger.LogInformation(
                        "MLLP listener started on port {Port} ({Name}).",
                        target.Port,
                        target.Name);
                    acceptLoops.Add(AcceptLoopAsync(listener, target, stoppingToken));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to bind MLLP listener on port {Port} ({Name}).", target.Port, target.Name);
                }
            }

            if (acceptLoops.Count == 0)
            {
                return;
            }

            await Task.WhenAll(acceptLoops);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            foreach (var listener in listeners)
            {
                listener.Stop();
            }
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, ListenTarget target, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stoppingToken);
            _logger.LogInformation("MLLP connection accepted on port {Port} from {Remote}.",
                target.Port, client.Client.RemoteEndPoint);
            _ = HandleClientAsync(client, target, stoppingToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, ListenTarget target, CancellationToken ct)
    {
        using (client)
        await using (var stream = client.GetStream())
        {
            var buffer = new byte[8192];
            var accumulated = new List<byte>();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (read == 0)
                    {
                        await TryProcessUnframedAsync(accumulated, target, stream, ct);
                        break;
                    }

                    accumulated.AddRange(buffer[..read]);
                    var messages = MllpFraming.Extract(accumulated.ToArray(), out var consumed);
                    if (consumed > 0)
                    {
                        accumulated.RemoveRange(0, consumed);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "MLLP port {Port}: received {Bytes} bytes; waiting for a complete 0x0B...0x1C 0x0D frame.",
                            target.Port, read);
                    }

                    foreach (var raw in messages)
                    {
                        _logger.LogInformation("MLLP port {Port}: received {Length}-character message.", target.Port, raw.Length);
                        var ack = await ProcessAsync(raw, target.EndpointId, ct);
                        await stream.WriteAsync(MllpFraming.Wrap(ack), ct);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "MLLP client handling failed on port {Port}.", target.Port);
            }
        }
    }

    /// <summary>
    /// Some senders write raw HL7 without MLLP start/end bytes. When the connection
    /// closes with leftover bytes that look like a message, process them anyway.
    /// </summary>
    private async Task TryProcessUnframedAsync(List<byte> accumulated, ListenTarget target, NetworkStream stream, CancellationToken ct)
    {
        if (accumulated.Count == 0)
        {
            return;
        }

        var leftover = Encoding.UTF8.GetString(accumulated.ToArray())
            .TrimStart('\u000b')
            .TrimEnd('\u001c', '\r', '\n', ' ');
        if (!leftover.StartsWith("MSH", StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "MLLP port {Port}: connection closed with {Bytes} bytes and no complete MLLP frame.",
                target.Port, accumulated.Count);
            return;
        }

        _logger.LogWarning("MLLP port {Port}: connection closed with unframed HL7; processing as a raw message.", target.Port);
        var ack = await ProcessAsync(leftover, target.EndpointId, ct);
        try
        {
            if (stream.CanWrite)
            {
                await stream.WriteAsync(MllpFraming.Wrap(ack), ct);
            }
        }
        catch (IOException)
        {
            // Peer already closed the socket.
        }
    }

    private async Task<IReadOnlyList<ListenTarget>> ResolveListenTargetsAsync(CancellationToken ct)
    {
        var targets = new List<ListenTarget>();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BloodBankDbContext>();
            var endpoints = await db.InterfaceEndpoints
                .Where(e => e.IsEnabled && e.Direction == Hl7Direction.Inbound && e.Transport == InterfaceTransport.Mllp && e.Port != null)
                .OrderBy(e => e.Id)
                .ToListAsync(ct);

            foreach (var endpoint in endpoints)
            {
                if (endpoint.Port is not int port || targets.Any(t => t.Port == port))
                {
                    continue;
                }

                targets.Add(new ListenTarget(port, endpoint.Id, endpoint.Name));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read MLLP endpoint configuration.");
        }

        if (_forceEnabled && targets.All(t => t.Port != _fallbackPort))
        {
            targets.Add(new ListenTarget(_fallbackPort, null, "Hl7:Mllp:Port"));
        }

        return targets;
    }

    private async Task<string> ProcessAsync(string raw, long? endpointId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<Hl7InboundProcessor>();
        var outcome = await processor.ProcessAsync(raw, endpointId, isReplay: false, ct);
        return outcome.AckMessage;
    }

    private sealed record ListenTarget(int Port, long? EndpointId, string Name);
}
