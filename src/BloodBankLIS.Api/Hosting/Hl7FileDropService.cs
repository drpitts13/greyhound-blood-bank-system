using BloodBankLIS.HL7.Messaging;

namespace BloodBankLIS.Api.Hosting;

/// <summary>
/// Polls inbound file-drop folders. Interval is <c>Hl7:FileDrop:IntervalSeconds</c>
/// (default 15; set 0 to disable).
/// </summary>
public sealed class Hl7FileDropService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Hl7FileDropService> _logger;
    private readonly int _intervalSeconds;

    public Hl7FileDropService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<Hl7FileDropService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalSeconds = configuration.GetValue("Hl7:FileDrop:IntervalSeconds", 15);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_intervalSeconds <= 0)
        {
            _logger.LogInformation("HL7 file-drop poller disabled (Hl7:FileDrop:IntervalSeconds={Interval}).", _intervalSeconds);
            return;
        }

        _logger.LogInformation("HL7 file-drop poller running every {Seconds}s.", _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var poller = scope.ServiceProvider.GetRequiredService<Hl7FileDropInboundPoller>();
                var processed = await poller.PollAsync(ct: stoppingToken);
                if (processed > 0)
                    _logger.LogInformation("HL7 file-drop poller processed {Count} inbound file(s).", processed);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HL7 file-drop poll cycle failed.");
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
