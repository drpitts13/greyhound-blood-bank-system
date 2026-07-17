using System.Net;
using System.Net.Sockets;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.HL7.Messaging;
using BloodBankLIS.HL7.Mllp;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Api.Hosting;

/// <summary>
/// Inbound MLLP listener. A thin transport adapter: it frames/deframes MLLP and hands
/// each message to <see cref="Hl7InboundProcessor"/> (a new DI scope per message), then
/// writes back the framed ACK. Disabled by default; enable via <c>Hl7:Mllp:Enabled</c>
/// so dev runs and tests do not bind a port (see docs/hl7-design.md section 4).
/// </summary>
public sealed class MllpListenerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MllpListenerService> _logger;
    private readonly bool _enabled;
    private readonly int _port;

    public MllpListenerService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<MllpListenerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _enabled = configuration.GetValue("Hl7:Mllp:Enabled", false);
        _port = configuration.GetValue("Hl7:Mllp:Port", 2575);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("MLLP listener disabled (set Hl7:Mllp:Enabled=true to enable).");
            return;
        }

        // Prefer a configured, enabled inbound MLLP endpoint's port; fall back to appsettings.
        var port = await ResolvePortAsync(stoppingToken);

        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        _logger.LogInformation("MLLP listener started on port {Port}.", port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                _ = HandleClientAsync(client, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
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
                        break;
                    }

                    accumulated.AddRange(buffer[..read]);
                    var messages = MllpFraming.Extract(accumulated.ToArray(), out var consumed);
                    if (consumed > 0)
                    {
                        accumulated.RemoveRange(0, consumed);
                    }

                    foreach (var raw in messages)
                    {
                        var ack = await ProcessAsync(raw, ct);
                        await stream.WriteAsync(MllpFraming.Wrap(ack), ct);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "MLLP client handling failed.");
            }
        }
    }

    private async Task<int> ResolvePortAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BloodBankDbContext>();
            var endpoint = await db.InterfaceEndpoints
                .Where(e => e.IsEnabled && e.Direction == Hl7Direction.Inbound && e.Transport == InterfaceTransport.Mllp && e.Port != null)
                .OrderBy(e => e.Id)
                .FirstOrDefaultAsync(ct);

            if (endpoint?.Port is int configured)
            {
                _logger.LogInformation("Using MLLP port {Port} from configured endpoint '{Name}'.", configured, endpoint.Name);
                return configured;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read MLLP endpoint configuration; falling back to appsettings port {Port}.", _port);
        }

        return _port;
    }

    private async Task<string> ProcessAsync(string raw, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<Hl7InboundProcessor>();
        var outcome = await processor.ProcessAsync(raw, endpointId: null, isReplay: false, ct);
        return outcome.AckMessage;
    }
}
