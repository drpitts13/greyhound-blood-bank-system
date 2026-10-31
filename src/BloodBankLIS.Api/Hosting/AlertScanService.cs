using BloodBankLIS.Application.Compliance;

namespace BloodBankLIS.Api.Hosting;

/// <summary>
/// Keeps condition alerts (par shortage, near expiry, reagent QC) in step with the
/// worklists. Interval is <c>Alerts:ScanSeconds</c> (default 300; set 0 to disable).
/// </summary>
public sealed class AlertScanService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertScanService> _logger;
    private readonly int _intervalSeconds;

    public AlertScanService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AlertScanService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalSeconds = configuration.GetValue("Alerts:ScanSeconds", 300);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_intervalSeconds <= 0)
        {
            _logger.LogInformation("Alert scan disabled (Alerts:ScanSeconds={Interval}).", _intervalSeconds);
            return;
        }

        _logger.LogInformation("Alert scan running every {Seconds}s.", _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<AlertScanner>();
                var (raised, resolved) = await scanner.ScanAsync(stoppingToken);
                if (raised > 0 || resolved > 0)
                {
                    _logger.LogInformation("Alert scan raised {Raised} and resolved {Resolved} alert(s).", raised, resolved);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Alert scan failed.");
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
