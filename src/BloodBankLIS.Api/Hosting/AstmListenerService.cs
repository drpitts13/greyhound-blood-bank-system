using System.Net;
using System.Net.Sockets;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Astm.Framing;
using BloodBankLIS.Astm.Messaging;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Api.Hosting;

/// <summary>Binds enabled server-mode instrument interfaces and runs LIS1-A sessions.</summary>
public sealed class AstmListenerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInstrumentRuntimeStatus _status;
    private readonly ILogger<AstmListenerService> _logger;

    public AstmListenerService(
        IServiceScopeFactory scopeFactory,
        IInstrumentRuntimeStatus status,
        ILogger<AstmListenerService> logger)
    {
        _scopeFactory = scopeFactory;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var targets = await ResolveAsync(stoppingToken);
        if (targets.Count == 0)
        {
            _logger.LogInformation("ASTM listener idle: no enabled server-mode instrument interfaces.");
            return;
        }

        var loops = new List<Task>();
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
                    _status.Set(target.Id, "Listening", $"port {target.Port}");
                    await WriteLogAsync(target.Id, "Info", "Bind", $"Listening on port {target.Port}.", stoppingToken);
                    loops.Add(AcceptLoopAsync(listener, target, stoppingToken));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _status.Set(target.Id, "Error", ex.Message);
                    _logger.LogError(ex, "Failed to bind ASTM listener on port {Port} ({Name}).", target.Port, target.Name);
                }
            }

            if (loops.Count > 0)
            {
                await Task.WhenAll(loops);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        finally
        {
            foreach (var listener in listeners)
            {
                listener.Stop();
            }
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, ListenTarget target, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(ct);
            _status.Set(target.Id, "Connected", client.Client.RemoteEndPoint?.ToString());
            await WriteLogAsync(target.Id, "Info", "Accept", $"Connection from {client.Client.RemoteEndPoint}.", ct);
            _ = HandleClientAsync(client, target, ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, ListenTarget target, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                var session = new AstmSession(client.GetStream(), isServer: true, _logger);
                var received = await session.ReceiveAsync(ct);
                if (string.IsNullOrWhiteSpace(received.Message))
                {
                    await WriteLogAsync(target.Id, "Warn", "Session", received.Error ?? "Empty ASTM session.", ct);
                    return;
                }

                await WriteLogAsync(target.Id, "Info", "Receive", $"Received {received.Message.Length} characters.", ct);
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<AstmInboundProcessor>();
                var outcome = await processor.ProcessAsync(received.Message, target.Id, isReplay: false, ct);
                if (!string.IsNullOrWhiteSpace(outcome.ReplyMessage))
                {
                    var replySession = new AstmSession(client.GetStream(), isServer: true, _logger);
                    await replySession.SendAsync(outcome.ReplyMessage, ct);
                    await WriteLogAsync(target.Id, "Info", "Send", "Order download sent.", ct);
                }

                _status.Set(target.Id, "Listening", $"port {target.Port}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _status.Set(target.Id, "Error", ex.Message);
                _logger.LogError(ex, "ASTM client handling failed on port {Port}.", target.Port);
            }
        }
    }

    private async Task<IReadOnlyList<ListenTarget>> ResolveAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BloodBankDbContext>();
            var items = await db.InstrumentInterfaces
                .Include(i => i.Connection)
                .Where(i => i.IsEnabled && i.Connection != null && i.Connection.IpAddress == null)
                .ToListAsync(ct);
            return items
                .Where(i => i.Connection!.Port is > 0 and <= 65535)
                .Select(i => new ListenTarget(i.Id, i.Name, i.Connection!.Port))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read ASTM instrument interfaces.");
            return [];
        }
    }

    private async Task WriteLogAsync(long interfaceId, string level, string eventType, string message, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BloodBankDbContext>();
            db.InstrumentInterfaceLogs.Add(new InstrumentInterfaceLog
            {
                InterfaceId = interfaceId,
                OccurredUtc = DateTime.UtcNow,
                Level = level,
                EventType = eventType,
                Message = message
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not persist ASTM session log.");
        }
    }

    private sealed record ListenTarget(long Id, string Name, int Port);
}
