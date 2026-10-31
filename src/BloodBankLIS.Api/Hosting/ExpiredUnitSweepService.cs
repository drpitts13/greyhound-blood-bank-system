using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Inventory;

namespace BloodBankLIS.Api.Hosting;

/// <summary>
/// Moves units at or past expiration into Expired on a schedule, so stock status
/// does not depend on someone pressing the manual sweep. Interval is
/// <c>Inventory:ExpirationSweepSeconds</c> (default 300; set 0 to disable).
/// The issue gate still blocks expired units on its own.
/// </summary>
public sealed class ExpiredUnitSweepService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredUnitSweepService> _logger;
    private readonly int _intervalSeconds;

    public ExpiredUnitSweepService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ExpiredUnitSweepService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalSeconds = configuration.GetValue("Inventory:ExpirationSweepSeconds", 300);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_intervalSeconds <= 0)
        {
            _logger.LogInformation(
                "Expired-unit sweep disabled (Inventory:ExpirationSweepSeconds={Interval}).",
                _intervalSeconds);
            return;
        }

        _logger.LogInformation("Expired-unit sweep running every {Seconds}s.", _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var inventory = scope.ServiceProvider.GetRequiredService<InventoryService>();
                var result = await inventory.RunScheduledExpirationSweepAsync(stoppingToken);
                if (result.AffectedCount > 0)
                {
                    _logger.LogInformation("Expired-unit sweep expired {Count} unit(s).", result.AffectedCount);
                    var alerts = scope.ServiceProvider.GetRequiredService<AlertScanner>();
                    await alerts.RaiseUnitsExpiredAsync(result.AffectedCount, DateTime.UtcNow, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expired-unit sweep failed.");
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
