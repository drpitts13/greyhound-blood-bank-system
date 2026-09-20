using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Astm.Framing;
using BloodBankLIS.Astm.Messaging;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Api.Hosting;

/// <summary>Connects enabled client-mode instrument interfaces and waits for ASTM sessions.</summary>
public sealed class AstmClientService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInstrumentRuntimeStatus _status;
    private readonly ILogger<AstmClientService> _logger;

    public AstmClientService(
        IServiceScopeFactory scopeFactory,
        IInstrumentRuntimeStatus status,
        ILogger<AstmClientService> logger)
    {
        _scopeFactory = scopeFactory;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var targets = await ResolveAsync(stoppingToken);
            foreach (var target in targets)
            {
                try
                {
                    _status.Set(target.Id, "Connecting", $"{target.Host}:{target.Port}");
                    using var client = new System.Net.Sockets.TcpClient();
                    await client.ConnectAsync(target.Host, target.Port, stoppingToken);
                    _status.Set(target.Id, "Connected", $"{target.Host}:{target.Port}");
                    var received = await AstmSession.ReceiveFromTcpAsync(client, isServer: false, _logger, stoppingToken);
                    if (!string.IsNullOrWhiteSpace(received.Message))
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var processor = scope.ServiceProvider.GetRequiredService<AstmInboundProcessor>();
                        await processor.ProcessAsync(received.Message, target.Id, isReplay: false, stoppingToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _status.Set(target.Id, "Error", ex.Message);
                    _logger.LogWarning(ex, "ASTM client {Name} could not connect to {Host}:{Port}.", target.Name, target.Host, target.Port);
                    await WriteLogAsync(target.Id, "Warn", "Connect", ex.Message, stoppingToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task<IReadOnlyList<ClientTarget>> ResolveAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BloodBankDbContext>();
            var items = await db.InstrumentInterfaces
                .Include(i => i.Connection)
                .Where(i => i.IsEnabled && i.Connection != null && i.Connection.IpAddress != null)
                .ToListAsync(ct);
            return items
                .Select(i => new ClientTarget(i.Id, i.Name, i.Connection!.IpAddress!, i.Connection.Port))
                .ToList();
        }
        catch
        {
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
        catch
        {
            // best-effort
        }
    }

    private sealed record ClientTarget(long Id, string Name, string Host, int Port);
}
