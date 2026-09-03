using BloodBankLIS.HL7.Messaging;

namespace BloodBankLIS.Api.Hosting;

/// <summary>
/// Polls queued outbound HL7 messages and transmits them over MLLP. Complements
/// <see cref="MllpListenerService"/> (inbound). Interval is <c>Hl7:Mllp:OutboundIntervalSeconds</c>
/// (default 15; set 0 to disable the poller).
/// </summary>
public sealed class MllpSenderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MllpSenderService> _logger;
    private readonly int _intervalSeconds;

    public MllpSenderService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<MllpSenderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalSeconds = configuration.GetValue("Hl7:Mllp:OutboundIntervalSeconds", 15);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_intervalSeconds <= 0)
        {
            _logger.LogInformation("MLLP outbound sender disabled (Hl7:Mllp:OutboundIntervalSeconds={Interval}).", _intervalSeconds);
            return;
        }

        _logger.LogInformation("MLLP outbound sender polling every {Seconds}s.", _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<Hl7OutboundSender>();
                var sent = await sender.SendPendingAsync(ct: stoppingToken);
                if (sent > 0)
                    _logger.LogInformation("MLLP outbound sender transmitted {Count} message(s).", sent);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MLLP outbound send cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
